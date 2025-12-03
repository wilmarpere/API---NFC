using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using API_NFC.Data;
using API___NFC.Models;

namespace API___NFC.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AprendizController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public AprendizController(ApplicationDbContext context)
        {
            _context = context;
        }

        // ✅ GET: api/Aprendiz (CON Include de Ficha y Programa)
        [HttpGet]
        public async Task<ActionResult<IEnumerable<object>>> GetAprendiz()
        {
            var aprendices = await _context.Aprendiz
                .Include(a => a.Ficha)
                    .ThenInclude(f => f.Programa)
                .Where(a => a.Estado == true)
                .AsNoTracking()
                .Select(a => new
                {
                    a.IdAprendiz,
                    a.Nombre,
                    a.Apellido,
                    a.TipoDocumento,
                    a.NumeroDocumento,
                    a.Correo,
                    a.CodigoBarras,
                    a.IdFicha,
                    a.Telefono,
                    a.FotoUrl,
                    a.Estado,
                    a.FechaCreacion,
                    a.FechaActualizacion,
                    Ficha = a.Ficha != null ? new
                    {
                        a.Ficha.IdFicha,
                        a.Ficha.Codigo,
                        a.Ficha.FechaInicio,
                        a.Ficha.FechaFinal,
                        Programa = a.Ficha.Programa != null ? new
                        {
                            a.Ficha.Programa.IdPrograma,
                            a.Ficha.Programa.NombrePrograma,
                            a.Ficha.Programa.Codigo,
                            a.Ficha.Programa.NivelFormacion
                        } : null
                    } : null
                })
                .ToListAsync();

            return Ok(aprendices);
        }

        // ✅ GET: api/Aprendiz/5 (CON Include de Ficha y Programa)
        [HttpGet("{id}")]
        public async Task<ActionResult<object>> GetAprendiz(int id)
        {
            var aprendiz = await _context.Aprendiz
                .Include(a => a.Ficha)
                    .ThenInclude(f => f.Programa)
                .AsNoTracking()
                .Where(a => a.IdAprendiz == id)
                .Select(a => new
                {
                    a.IdAprendiz,
                    a.Nombre,
                    a.Apellido,
                    a.TipoDocumento,
                    a.NumeroDocumento,
                    a.Correo,
                    a.CodigoBarras,
                    a.IdFicha,
                    a.Telefono,
                    a.FotoUrl,
                    a.Estado,
                    a.FechaCreacion,
                    a.FechaActualizacion,
                    Ficha = a.Ficha != null ? new
                    {
                        a.Ficha.IdFicha,
                        a.Ficha.Codigo,
                        a.Ficha.FechaInicio,
                        a.Ficha.FechaFinal,
                        a.Ficha.Estado,
                        Programa = a.Ficha.Programa != null ? new
                        {
                            a.Ficha.Programa.IdPrograma,
                            a.Ficha.Programa.NombrePrograma,
                            a.Ficha.Programa.Codigo,
                            a.Ficha.Programa.NivelFormacion
                        } : null
                    } : null
                })
                .FirstOrDefaultAsync();

            if (aprendiz == null)
            {
                return NotFound(new { Message = "Aprendiz no encontrado." });
            }

            return Ok(aprendiz);
        }

        // ✅ PUT: api/Aprendiz/5
        [HttpPut("{id}")]
        public async Task<IActionResult> PutAprendiz(int id, Aprendiz aprendiz)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var existing = await _context.Aprendiz.FindAsync(id);
            if (existing == null)
                return NotFound(new { Message = "Aprendiz no encontrado." });

            // Validar duplicados
            if (await _context.Aprendiz.AnyAsync(a =>
                (a.NumeroDocumento == aprendiz.NumeroDocumento ||
                 a.Correo == aprendiz.Correo ||
                 (!string.IsNullOrEmpty(aprendiz.CodigoBarras) && a.CodigoBarras == aprendiz.CodigoBarras)) &&
                a.IdAprendiz != id))
            {
                return Conflict(new { Message = "Ya existe un aprendiz con ese documento, correo o código de barras." });
            }

            existing.Nombre = aprendiz.Nombre;
            existing.Apellido = aprendiz.Apellido;
            existing.TipoDocumento = aprendiz.TipoDocumento;
            existing.NumeroDocumento = aprendiz.NumeroDocumento;
            existing.Correo = aprendiz.Correo;
            existing.CodigoBarras = aprendiz.CodigoBarras;
            existing.IdFicha = aprendiz.IdFicha;
            existing.Telefono = aprendiz.Telefono;
            existing.FotoUrl = aprendiz.FotoUrl;
            existing.Estado = aprendiz.Estado;
            existing.FechaActualizacion = DateTime.Now;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!AprendizExists(id))
                    return NotFound();
                throw;
            }

            return NoContent();
        }

        // ✅ POST: api/Aprendiz
        [HttpPost]
        public async Task<ActionResult<Aprendiz>> PostAprendiz(Aprendiz aprendiz)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            // Validar duplicados
            if (await _context.Aprendiz.AnyAsync(a =>
                a.NumeroDocumento == aprendiz.NumeroDocumento ||
                a.Correo == aprendiz.Correo))
            {
                return Conflict(new { Message = "Ya existe un aprendiz con ese número de documento o correo." });
            }

            aprendiz.Estado ??= true;
            aprendiz.FechaCreacion = DateTime.Now;
            aprendiz.FechaActualizacion = DateTime.Now;

            _context.Aprendiz.Add(aprendiz);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetAprendiz", new { id = aprendiz.IdAprendiz }, aprendiz);
        }

        // ✅ DELETE: api/Aprendiz/5 (Soft Delete)
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteAprendiz(int id)
        {
            var aprendiz = await _context.Aprendiz.FindAsync(id);
            if (aprendiz == null)
            {
                return NotFound(new { Message = "Aprendiz no encontrado." });
            }

            aprendiz.Estado = false;
            aprendiz.FechaActualizacion = DateTime.Now;

            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool AprendizExists(int id)
        {
            return _context.Aprendiz.Any(e => e.IdAprendiz == id);
        }

        // ✅ GET: api/Aprendiz/paged (CON Include)
        [HttpGet("paged")]
        public async Task<ActionResult> GetAprendizPaged([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
        {
            if (pageNumber < 1) pageNumber = 1;
            if (pageSize < 1) pageSize = 10;
            const int maxPageSize = 100;
            if (pageSize > maxPageSize) pageSize = maxPageSize;

            var query = _context.Aprendiz
                .Include(a => a.Ficha)
                    .ThenInclude(f => f.Programa)
                .Where(a => a.Estado == true)
                .AsNoTracking();

            var totalCount = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

            var items = await query
                .OrderBy(a => a.IdAprendiz)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(a => new
                {
                    a.IdAprendiz,
                    a.Nombre,
                    a.Apellido,
                    a.TipoDocumento,
                    a.NumeroDocumento,
                    a.Correo,
                    a.CodigoBarras,
                    a.IdFicha,
                    a.Telefono,
                    a.FotoUrl,
                    a.Estado,
                    Ficha = a.Ficha != null ? new
                    {
                        a.Ficha.IdFicha,
                        a.Ficha.Codigo,
                        Programa = a.Ficha.Programa != null ? new
                        {
                            a.Ficha.Programa.NombrePrograma
                        } : null
                    } : null
                })
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
    }
}