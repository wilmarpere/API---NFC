using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using API_NFC.Data;
using API___NFC.Models;

namespace API___NFC.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TagAsignadoController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        public TagAsignadoController(ApplicationDbContext context) => _context = context;

        [HttpPost]
        public async Task<IActionResult> PostTag([FromBody] TagAsignado tag)
        {
            if (string.IsNullOrWhiteSpace(tag.CodigoTag))
                return BadRequest("El código del tag es obligatorio.");

            // Evitar duplicado
            if (await _context.TagAsignado.AnyAsync(t => t.CodigoTag == tag.CodigoTag))
                return Conflict("Este tag ya está asignado.");

            tag.FechaAsignacion = DateTime.Now;
            _context.TagAsignado.Add(tag);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Tag registrado correctamente.", tag });
        }
        [HttpDelete("cleanup/{codigo}")]
        public async Task<IActionResult> CleanupTag(string codigo)
        {
            try
            {
                Console.WriteLine($"🧹 Limpiando tag: {codigo}");

                // 1. Buscar el tag
                var tag = await _context.TagAsignado.FirstOrDefaultAsync(t => t.CodigoTag == codigo);

                // 2.  Buscar el elemento asociado
                var elemento = await _context.Elemento.FirstOrDefaultAsync(e => e.CodigoNFC == codigo);

                if (tag == null && elemento == null)
                {
                    Console.WriteLine($"⚠️ Tag {codigo} no encontrado en el sistema");
                    return NotFound(new { message = "Tag no encontrado en el sistema." });
                }

                // 3. Liberar el elemento (soft delete)
                if (elemento != null)
                {
                    elemento.CodigoNFC = null;
                    elemento.Estado = false;
                    elemento.FechaActualizacion = DateTime.Now;
                    Console.WriteLine($"🔓 Elemento ID {elemento.IdElemento} liberado");
                }

                // 4. Eliminar TagAsignado
                if (tag != null)
                {
                    _context.TagAsignado.Remove(tag);
                    Console.WriteLine($"🗑️ TagAsignado eliminado");
                }

                await _context.SaveChangesAsync();
                Console.WriteLine($"✅ Tag {codigo} limpiado completamente de la BD");

                return Ok(new
                {
                    message = "Tag limpiado correctamente y listo para reutilizar",
                    codigoTag = codigo,
                    elementoLiberado = elemento?.IdElemento
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error al limpiar tag {codigo}: {ex.Message}");
                return StatusCode(500, new { message = $"Error al limpiar tag: {ex.Message}" });
            }
        }
    }
}
