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
    public class ElementoProcesoesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public ElementoProcesoesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // ✅ GET: api/ElementoProcesoes
        [HttpGet]
        public async Task<ActionResult<IEnumerable<object>>> GetElementoProcesos()
        {
            var elementoProcesos = await _context.ElementoProceso
                .Include(e => e.Elemento)
                    .ThenInclude(e => e.TipoElemento)
                .Include(e => e.Proceso)
                .AsNoTracking()
                .Select(ep => new
                {
                    ep.IdElementoProceso,
                    ep.IdElemento,
                    ep.IdProceso,
                    ep.Validado,
                    Elemento = ep.Elemento != null ? new
                    {
                        ep.Elemento.IdElemento,
                        ep.Elemento.Marca,
                        ep.Elemento.Modelo,
                        ep.Elemento.Serial,
                        ep.Elemento.Descripcion,
                        ep.Elemento.ImagenUrl,
                        ep.Elemento.CodigoNFC,
                        TipoElemento = ep.Elemento.TipoElemento != null ? new
                        {
                            ep.Elemento.TipoElemento.IdTipoElemento,
                            ep.Elemento.TipoElemento.Tipo
                        } : null
                    } : null
                })
                .ToListAsync();

            return Ok(elementoProcesos);
        }

        // ✅ GET: api/ElementoProcesoes/5
        [HttpGet("{id}")]
        public async Task<ActionResult<object>> GetElementoProceso(int id)
        {
            var elementoProceso = await _context.ElementoProceso
                .Include(e => e.Elemento)
                    .ThenInclude(e => e.TipoElemento)
                .Include(e => e.Proceso)
                .AsNoTracking()
                .Where(ep => ep.IdElementoProceso == id)
                .Select(ep => new
                {
                    ep.IdElementoProceso,
                    ep.IdElemento,
                    ep.IdProceso,
                    ep.Validado,
                    Elemento = ep.Elemento != null ? new
                    {
                        ep.Elemento.IdElemento,
                        ep.Elemento.Marca,
                        ep.Elemento.Modelo,
                        ep.Elemento.Serial,
                        ep.Elemento.Descripcion,
                        ep.Elemento.ImagenUrl,
                        ep.Elemento.CodigoNFC,
                        TipoElemento = ep.Elemento.TipoElemento != null ? new
                        {
                            ep.Elemento.TipoElemento.IdTipoElemento,
                            ep.Elemento.TipoElemento.Tipo
                        } : null
                    } : null
                })
                .FirstOrDefaultAsync();

            if (elementoProceso == null)
                return NotFound(new { Message = "ElementoProceso no encontrado." });

            return Ok(elementoProceso);
        }

        // 🔥 GET: api/ElementoProcesoes/byProceso/106 (MEJORADO)
        [HttpGet("byProceso/{idProceso}")]
        public async Task<ActionResult<IEnumerable<object>>> GetByProceso(int idProceso)
        {
            Console.WriteLine($"🔍 Buscando dispositivos para proceso: {idProceso}");

            var relaciones = await _context.ElementoProceso
                .Where(e => e.IdProceso == idProceso)
                .Include(e => e.Elemento)
                    .ThenInclude(e => e.TipoElemento)
                .AsNoTracking()
                .Select(ep => new
                {
                    ep.IdElementoProceso,
                    ep.IdElemento,
                    ep.IdProceso,
                    ep.Validado,
                    Elemento = ep.Elemento != null ? new
                    {
                        IdElemento = ep.Elemento.IdElemento,
                        Marca = ep.Elemento.Marca,
                        Modelo = ep.Elemento.Modelo,
                        Serial = ep.Elemento.Serial,
                        Descripcion = ep.Elemento.Descripcion,
                        ImagenUrl = ep.Elemento.ImagenUrl,
                        CodigoNFC = ep.Elemento.CodigoNFC,
                        Estado = ep.Elemento.Estado,
                        TipoElemento = ep.Elemento.TipoElemento != null ? new
                        {
                            IdTipoElemento = ep.Elemento.TipoElemento.IdTipoElemento,
                            Tipo = ep.Elemento.TipoElemento.Tipo,
                            RequiereNFC = ep.Elemento.TipoElemento.RequiereNFC
                        } : null
                    } : null
                })
                .ToListAsync();

            Console.WriteLine($"✅ Encontrados {relaciones.Count} dispositivos");
            return Ok(relaciones);
        }

        // 🆕 POST: api/ElementoProcesoes/autoRegister
        /// <summary>
        /// Auto-registra todos los dispositivos del propietario en el proceso
        /// </summary>
        [HttpPost("autoRegister")]
        public async Task<ActionResult<object>> AutoRegisterDevices([FromBody] AutoRegisterRequest request)
        {
            Console.WriteLine($"🤖 Auto-registro iniciado para Proceso: {request.IdProceso}, Tipo: {request.TipoPropietario}, ID: {request.IdPropietario}");

            // Validar que el proceso exista
            var proceso = await _context.Proceso.FindAsync(request.IdProceso);
            if (proceso == null)
                return NotFound(new { Message = "El proceso no existe." });

            // Obtener dispositivos del propietario
            var dispositivos = await _context.Elemento
                .Where(e => e.IdPropietario == request.IdPropietario
                         && e.TipoPropietario == request.TipoPropietario
                         && e.Estado == true)
                .ToListAsync();

            Console.WriteLine($"📦 Dispositivos encontrados: {dispositivos.Count}");

            int registrados = 0;
            int omitidos = 0;

            foreach (var dispositivo in dispositivos)
            {
                // Evitar duplicados
                bool yaExiste = await _context.ElementoProceso
                    .AnyAsync(ep => ep.IdElemento == dispositivo.IdElemento
                                 && ep.IdProceso == request.IdProceso);

                if (yaExiste)
                {
                    omitidos++;
                    Console.WriteLine($"⚠️ Dispositivo {dispositivo.Serial} ya está registrado");
                    continue;
                }

                // Crear relación
                var elementoProceso = new ElementoProceso
                {
                    IdElemento = dispositivo.IdElemento,
                    IdProceso = request.IdProceso,
                    Validado = true
                };

                _context.ElementoProceso.Add(elementoProceso);
                registrados++;
            }

            await _context.SaveChangesAsync();

            Console.WriteLine($"✅ Auto-registro completado: {registrados} registrados, {omitidos} omitidos");

            return Ok(new
            {
                Message = "Auto-registro completado",
                Registrados = registrados,
                Omitidos = omitidos,
                Total = dispositivos.Count
            });
        }

        // ✅ PUT: api/ElementoProcesoes/5
        [HttpPut("{id}")]
        public async Task<IActionResult> PutElementoProceso(int id, [FromBody] ElementoProceso elementoProceso)
        {
            if (id != elementoProceso.IdElementoProceso)
                return BadRequest(new { Message = "El ID del elemento proceso no coincide." });

            var existing = await _context.ElementoProceso.FindAsync(id);
            if (existing == null)
                return NotFound(new { Message = "ElementoProceso no encontrado." });

            existing.IdElemento = elementoProceso.IdElemento;
            existing.IdProceso = elementoProceso.IdProceso;
            existing.Validado = elementoProceso.Validado;

            await _context.SaveChangesAsync();
            return NoContent();
        }

        // ✅ POST: api/ElementoProcesoes
        [HttpPost]
        public async Task<ActionResult<ElementoProceso>> PostElementoProceso([FromBody] ElementoProceso elementoProceso)
        {
            Console.WriteLine($"📥 Creando ElementoProceso: IdElemento={elementoProceso.IdElemento}, IdProceso={elementoProceso.IdProceso}");

            // Validar existencia de FKs
            if (!await _context.Elemento.AnyAsync(e => e.IdElemento == elementoProceso.IdElemento))
            {
                Console.WriteLine($"❌ Elemento {elementoProceso.IdElemento} no existe");
                return BadRequest(new { Message = "El elemento asociado no existe." });
            }

            if (!await _context.Proceso.AnyAsync(p => p.IdProceso == elementoProceso.IdProceso))
            {
                Console.WriteLine($"❌ Proceso {elementoProceso.IdProceso} no existe");
                return BadRequest(new { Message = "El proceso asociado no existe." });
            }

            // Evitar duplicados
            if (await _context.ElementoProceso.AnyAsync(ep =>
                ep.IdElemento == elementoProceso.IdElemento &&
                ep.IdProceso == elementoProceso.IdProceso))
            {
                Console.WriteLine($"⚠️ Ya existe la relación");
                return Conflict(new { Message = "Este elemento ya está asociado a este proceso." });
            }

            elementoProceso.Validado ??= false;

            _context.ElementoProceso.Add(elementoProceso);
            await _context.SaveChangesAsync();

            Console.WriteLine($"✅ ElementoProceso creado con ID: {elementoProceso.IdElementoProceso}");

            return CreatedAtAction(nameof(GetElementoProceso),
                new { id = elementoProceso.IdElementoProceso },
                elementoProceso);
        }

        // ✅ DELETE: api/ElementoProcesoes/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteElementoProceso(int id)
        {
            var elementoProceso = await _context.ElementoProceso.FindAsync(id);
            if (elementoProceso == null)
                return NotFound(new { Message = "ElementoProceso no encontrado." });

            _context.ElementoProceso.Remove(elementoProceso);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool ElementoProcesoExists(int id)
        {
            return _context.ElementoProceso.Any(e => e.IdElementoProceso == id);
        }

        // 🆕 DTO para auto-registro
        public class AutoRegisterRequest
        {
            public int IdProceso { get; set; }
            public string TipoPropietario { get; set; } = string.Empty;
            public int IdPropietario { get; set; }
        }
    }
}