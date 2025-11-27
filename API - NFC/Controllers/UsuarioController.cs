using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using API_NFC.Data;
using API___NFC.Models;
using BCryptNet = BCrypt.Net.BCrypt;

namespace API___NFC.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UsuarioController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public UsuarioController(ApplicationDbContext context)
        {
            _context = context;
        }

        // ✅ GET: api/Usuario (solo activos)
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Usuario>>> GetUsuarios()
        {
            return await _context.Usuario
                .Where(u => u.Estado == true)
                .AsNoTracking()
                .OrderBy(u => u.Nombre)
                .ToListAsync();
        }

        // ✅ GET: api/Usuario/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Usuario>> GetUsuario(int id)
        {
            var usuario = await _context.Usuario
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.IdUsuario == id);

            if (usuario == null)
                return NotFound("Usuario no encontrado.");

            return Ok(usuario);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> PutUsuario(int id, [FromBody] Usuario usuario)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var existing = await _context.Usuario.FindAsync(id);
            if (existing == null)
                return NotFound();

            // ✅ Validar TipoDocumento
            var tiposValidos = new[] { "CC", "TI", "CE", "PA" };
            if (!tiposValidos.Contains(usuario.TipoDocumento))
            {
                return BadRequest("El tipo de documento debe ser CC, TI, CE o PA");
            }

            // ✅ Validar Rol
            var rolesValidos = new[] { "Administrador", "Guardia", "Funcionario" };
            if (!rolesValidos.Contains(usuario.Rol))
            {
                return BadRequest("El rol debe ser Administrador, Guardia o Funcionario");
            }

            // ✅ Validar duplicados (excepto el mismo usuario)
            if (await _context.Usuario.AnyAsync(u =>
                    (u.NumeroDocumento == usuario.NumeroDocumento ||
                     u.Correo == usuario.Correo ||
                     u.CodigoBarras == usuario.CodigoBarras) &&
                    u.IdUsuario != id))
            {
                return Conflict("Ya existe un usuario con ese documento, correo o código de barras.");
            }

            // ✅ Actualizar campos
            existing.Nombre = usuario.Nombre;
            existing.Apellido = usuario.Apellido;
            existing.TipoDocumento = usuario.TipoDocumento;
            existing.NumeroDocumento = usuario.NumeroDocumento;
            existing.Correo = usuario.Correo;
            existing.Rol = usuario.Rol;
            existing.CodigoBarras = usuario.CodigoBarras;
            existing.Cargo = usuario.Cargo;
            existing.Telefono = usuario.Telefono;
            existing.FotoUrl = usuario.FotoUrl;
            existing.Estado = usuario.Estado ?? true;
            existing.FechaActualizacion = DateTime.Now;

            // ⚠️ SOLO ACTUALIZAR CONTRASEÑA SI SE ENVIÓ UNA NUEVA
            if (!string.IsNullOrWhiteSpace(usuario.Contraseña))
            {
                existing.Contraseña = HashPasswordIfNeeded(usuario.Contraseña);
            }

            await _context.SaveChangesAsync();
            return NoContent();
        }

        // ✅ POST: api/Usuario
        [HttpPost]
        public async Task<ActionResult<Usuario>> PostUsuario(Usuario usuario)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            if (string.IsNullOrWhiteSpace(usuario.Contraseña))
            {
                return BadRequest("La contraseña es requerida y no puede estar vacía.");
            }

            // 🔹 Validar duplicados
            if (await _context.Usuario.AnyAsync(u =>
                    u.NumeroDocumento == usuario.NumeroDocumento ||
                    u.Correo == usuario.Correo))
            {
                return Conflict("Ya existe un usuario con ese número de documento o correo electrónico.");
            }

            usuario.Estado ??= true;
            usuario.FechaCreacion = DateTime.Now;
            usuario.FechaActualizacion = DateTime.Now;

            // 🔐 Hashear contraseña antes de guardar
            usuario.Contraseña = HashPasswordIfNeeded(usuario.Contraseña);

            _context.Usuario.Add(usuario);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetUsuario), new { id = usuario.IdUsuario }, usuario);
        }

        // ✅ DELETE (Soft Delete): api/Usuario/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteUsuario(int id)
        {
            var usuario = await _context.Usuario.FindAsync(id);
            if (usuario == null)
                return NotFound();

            usuario.Estado = false;
            usuario.FechaActualizacion = DateTime.Now;

            await _context.SaveChangesAsync();

            return NoContent();
        }

        // ✅ GET Paginado
        [HttpGet("paged")]
        public async Task<ActionResult> GetUsuariosPaged([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
        {
            if (pageNumber < 1) pageNumber = 1;
            if (pageSize < 1) pageSize = 10;
            const int maxPageSize = 100;
            if (pageSize > maxPageSize) pageSize = maxPageSize;

            var query = _context.Usuario
                .Where(u => u.Estado == true)
                .AsNoTracking()
                .OrderBy(u => u.Nombre);

            var totalCount = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

            var items = await query
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var metadata = new
            {
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalCount = totalCount,
                TotalPages = totalPages
            };

            Response.Headers["X-Pagination"] = System.Text.Json.JsonSerializer.Serialize(metadata);

            return Ok(new
            {
                Items = items,
                metadata.PageNumber,
                metadata.PageSize,
                metadata.TotalCount,
                metadata.TotalPages
            });
        }

        private bool UsuarioExists(int id)
        {
            return _context.Usuario.Any(e => e.IdUsuario == id);
        }

        private static string HashPasswordIfNeeded(string password)
        {
            if (string.IsNullOrWhiteSpace(password))
            {
                throw new ArgumentException("La contraseña no puede estar vacía.", nameof(password));
            }

            // Evitar doble hash si ya viene en formato BCrypt
            return IsBcryptHash(password)
                ? password
                : BCryptNet.HashPassword(password);
        }

        private static bool IsBcryptHash(string value)
        {
            return !string.IsNullOrWhiteSpace(value) &&
                   (value.StartsWith("$2a$") || value.StartsWith("$2b$") || value.StartsWith("$2y$"));
        }
    }
}
