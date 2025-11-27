using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using API_NFC.Data;
using API___NFC.Models;
using BCrypt.Net;
using System.Text.RegularExpressions;
using API___NFC.Services;
using Microsoft.AspNetCore.WebUtilities;

namespace API___NFC.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _config;
        private readonly IEmailSender _emailSender;

        public AuthController(ApplicationDbContext context, IConfiguration config, IEmailSender emailSender)
        {
            _context = context;
            _config = config;
            _emailSender = emailSender;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Correo) || string.IsNullOrWhiteSpace(request.Contraseña))
                return BadRequest(new { error = "Correo y contraseña requeridos." });

            var correo = request.Correo.Trim().ToLower();
            var usuario = await _context.Usuario.FirstOrDefaultAsync(u => u.Correo.ToLower() == correo && u.Estado == true);
            if (usuario == null)
                return Unauthorized(new { error = "Usuario no encontrado o inactivo." });

            if (usuario.Rol != "Administrador" && usuario.Rol != "Guardia")
                return Unauthorized(new { error = "Sin permisos (rol)." });

            var ver = VerificarContraseñaYPosibleMigracion(request.Contraseña, usuario.Contraseña);
            if (!ver.EsValida)
                return Unauthorized(new { error = "Contraseña incorrecta." });

            if (ver.NecesitaUpgrade)
            {
                usuario.Contraseña = BCrypt.Net.BCrypt.HashPassword(request.Contraseña);
                usuario.FechaActualizacion = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }

            var token = GenerateJwtToken(usuario);

            return Ok(new
            {
                message = "OK",
                token,
                usuario = new
                {
                    usuario.IdUsuario,
                    usuario.Nombre,
                    usuario.Apellido,
                    usuario.Rol,
                    usuario.Correo
                }
            });
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] Usuario usuario)
        {
            if (usuario == null)
                return BadRequest(new { error = "Datos inválidos." });

            if (string.IsNullOrWhiteSpace(usuario.Correo) || string.IsNullOrWhiteSpace(usuario.Contraseña) ||
                string.IsNullOrWhiteSpace(usuario.Nombre) || string.IsNullOrWhiteSpace(usuario.Apellido) ||
                string.IsNullOrWhiteSpace(usuario.Rol) || string.IsNullOrWhiteSpace(usuario.NumeroDocumento))
                return BadRequest(new { error = "Campos requeridos faltantes." });

            var correo = usuario.Correo.Trim().ToLower();
            if (await _context.Usuario.AnyAsync(u => u.Correo.ToLower() == correo))
                return Conflict(new { error = "Correo ya existe." });

            usuario.Correo = correo;
            usuario.Contraseña = BCrypt.Net.BCrypt.HashPassword(usuario.Contraseña);
            usuario.Estado = true;

            if (string.IsNullOrWhiteSpace(usuario.CodigoBarras))
            {
                usuario.CodigoBarras = null;
            }
            else
            {
                usuario.CodigoBarras = usuario.CodigoBarras.Trim();
            }

            usuario.FechaCreacion = DateTime.UtcNow;
            usuario.FechaActualizacion = DateTime.UtcNow;

            _context.Usuario.Add(usuario);
            await _context.SaveChangesAsync();
            return Ok(new { message = "Registrado" });
        }

        [HttpPost("RECUPERAR-password")]
        public async Task<IActionResult> Forgot([FromBody] ForgotPasswordRequest req)
        {
            if (req == null || string.IsNullOrWhiteSpace(req.Correo))
                return BadRequest(new { error = "Correo requerido." });

            var correo = req.Correo.Trim().ToLower();
            var usuario = await _context.Usuario.FirstOrDefaultAsync(u => u.Correo.ToLower() == correo);

            var genericMsg = new { message = "Si existe una cuenta con ese correo, recibirás un email con instrucciones." };

            if (usuario == null)
                return Ok(genericMsg);

            // Generar token seguro URL-safe
            var rawToken = GenerateResetToken();
            var hashed = HashToken(rawToken);
            usuario.TokenRecuperacion = hashed;
            usuario.FechaTokenExpira = DateTime.UtcNow.AddMinutes(30);
            await _context.SaveChangesAsync();

            var frontendBase = _config["FrontendUrl"]?.TrimEnd('/') ?? "http://localhost:3000";
            var resetUrl = $"{frontendBase}/reset-password?token={Uri.EscapeDataString(rawToken)}&id={usuario.IdUsuario}";

            var html = $@"
        <p>Hola {usuario.Nombre},</p>
        <p>Has solicitado recuperar tu contraseña. Haz click en el siguiente enlace para establecer una nueva contraseña. El enlace expirará en 30 minutos.</p>
        <p><a href=""{resetUrl}"">Resetear contraseña</a></p>
        <p>Si el enlace no abre, copia y pega esta URL en tu navegador:</p>
        <p>{resetUrl}</p>
        <hr/>
        <p>Si no solicitaste este cambio, ignora este correo.</p>
    ";

            // Disparar envío en background para no bloquear la petición
            _ = Task.Run(async () =>
            {
                try
                {
                    await _emailSender.SendEmailAsync(usuario.Correo, "Recuperación de contraseña - NFC", html, $"Visita {resetUrl} para resetear tu contraseña.");
                }
                catch (Exception ex)
                {
                    // Log con detalle
                    Console.Error.WriteLine("[Forgot->Background] Error enviando correo: " + ex);

                    // Limpiar token si fallo el envío (porque no queremos que quede token sin que usuario reciba nada)
                    try
                    {
                        var u = await _context.Usuario.FirstOrDefaultAsync(x => x.IdUsuario == usuario.IdUsuario);
                        if (u != null)
                        {
                            u.TokenRecuperacion = null;
                            u.FechaTokenExpira = null;
                            await _context.SaveChangesAsync();
                        }
                    }
                    catch (Exception e2)
                    {
                        Console.Error.WriteLine("[Forgot->Background] Error limpiando token tras fallo de envío: " + e2);
                    }
                }
            });

            // Responder inmediatamente (no exponemos si el usuario existe)
            return Ok(genericMsg);
        }

        [HttpPost("reset-password")]
        public async Task<IActionResult> Reset([FromBody] ResetPasswordRequest req)
        {
            if (req == null || string.IsNullOrWhiteSpace(req.Token) || string.IsNullOrWhiteSpace(req.NuevaContraseña))
                return BadRequest(new { error = "Datos incompletos." });

            var hashed = HashToken(req.Token);
            var usuario = await _context.Usuario.FirstOrDefaultAsync(u =>
                u.TokenRecuperacion == hashed && u.FechaTokenExpira > DateTime.UtcNow);

            if (usuario == null)
                return BadRequest(new { error = "Token inválido o expirado." });

            if (req.NuevaContraseña.Length < 6)
                return BadRequest(new { error = "La contraseña debe tener al menos 6 caracteres." });

            usuario.Contraseña = BCrypt.Net.BCrypt.HashPassword(req.NuevaContraseña);
            usuario.TokenRecuperacion = null;
            usuario.FechaTokenExpira = null;
            usuario.FechaActualizacion = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            // enviar confirmación (no bloquear si falla)
            try
            {
                var html = $"<p>Hola {usuario.Nombre},</p><p>Tu contraseña ha sido actualizada correctamente. Si no fuiste tú, contacta soporte.</p>";
                await _emailSender.SendEmailAsync(usuario.Correo, "Contraseña actualizada - NFC", html, $"Tu contraseña ha sido actualizada.");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Error enviando confirmación: " + ex);
            }

            return Ok(new { message = "Contraseña actualizada correctamente." });
        }

        // --------- helpers & métodos existentes ---------

        private string GenerateJwtToken(Usuario usuario)
        {
            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, usuario.IdUsuario.ToString()),
                new Claim("idUsuario", usuario.IdUsuario.ToString()),
                new Claim(ClaimTypes.Role, usuario.Rol),
                new Claim("nombre", usuario.Nombre),
                new Claim(JwtRegisteredClaimNames.Email, usuario.Correo),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            var keyStr = _config["Jwt:Key"] ?? throw new Exception("Falta Jwt:Key");
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(keyStr));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var token = new JwtSecurityToken(
                issuer: _config["Jwt:Issuer"],
                audience: _config["Jwt:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddHours(6),
                signingCredentials: creds
            );
            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        private PasswordCheckResult VerificarContraseñaYPosibleMigracion(string input, string stored)
        {
            if (string.IsNullOrEmpty(stored)) return new(false, false, "vacío");
            if (stored.StartsWith("$2a$") || stored.StartsWith("$2b$") || stored.StartsWith("$2y$"))
                return new(BCrypt.Net.BCrypt.Verify(input, stored), false, "bcrypt");

            bool isBase64 = stored.Length == 44 && Regex.IsMatch(stored, @"^[A-Za-z0-9+/=]+$");
            if (isBase64)
            {
                using var sha = SHA256.Create();
                var b64 = Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(input)));
                return new(stored == b64, stored == b64, "sha256-b64");
            }

            bool isHex = stored.Length == 64 && Regex.IsMatch(stored, "^[0-9a-fA-F]+$");
            if (isHex)
            {
                using var sha = SHA256.Create();
                var hex = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(input))).Replace("-", "").ToLower();
                return new(stored.Equals(hex, StringComparison.OrdinalIgnoreCase), stored.Equals(hex, StringComparison.OrdinalIgnoreCase), "sha256-hex");
            }

            if (stored == input) return new(true, true, "plaintext");
            return new(false, false, "mismatch");
        }

        private static string GenerateResetToken()
        {
            var bytes = new byte[32];
            RandomNumberGenerator.Fill(bytes);
            return WebEncoders.Base64UrlEncode(bytes);
        }

        private static string HashToken(string token)
        {
            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(token));
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }

        private record PasswordCheckResult(bool EsValida, bool NecesitaUpgrade, string Info);

        // DTOs
        public class LoginRequest { public string Correo { get; set; } = ""; public string Contraseña { get; set; } = ""; }
        public class ForgotPasswordRequest { public string Correo { get; set; } = ""; }
        public class ResetPasswordRequest { public string Token { get; set; } = ""; public string NuevaContraseña { get; set; } = ""; }
    }
}
