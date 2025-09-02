using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TexfinaApi.Data;
using TexfinaApi.Models;

namespace TexfinaApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class ClasesController : ControllerBase
    {
        private readonly TexfinaDbContext _context;
        private readonly ILogger<ClasesController> _logger;

        public ClasesController(TexfinaDbContext context, ILogger<ClasesController> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Obtener todas las clases con sus insumos asociados
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<IEnumerable<object>>> GetClases([FromQuery] string? buscar = null)
        {
            try
            {
                var query = _context.Clases
                    .Include(c => c.Insumos)
                    .AsQueryable();

                if (!string.IsNullOrEmpty(buscar))
                {
                    query = query.Where(c => c.Familia!.Contains(buscar) || 
                                           c.SubFamilia!.Contains(buscar) ||
                                           c.IdClase!.Contains(buscar));
                }

                var clases = await query
                    .Select(c => new
                    {
                        c.IdClase,
                        c.Familia,
                        c.SubFamilia,
                        TotalInsumos = c.Insumos.Count(),
                        InsumosActivos = c.Insumos.Count(i => i.CreatedAt != null)
                    })
                    .OrderBy(c => c.Familia)
                    .ThenBy(c => c.SubFamilia)
                    .ToListAsync();

                return Ok(clases);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener clases");
                return StatusCode(500, "Error interno del servidor");
            }
        }

        /// <summary>
        /// Obtener una clase específica con todos sus insumos
        /// </summary>
        [HttpGet("{id}")]
        public async Task<ActionResult<object>> GetClase(string id)
        {
            try
            {
                var clase = await _context.Clases
                    .Include(c => c.Insumos)
                        .ThenInclude(i => i.Unidad)
                    .Where(c => c.IdClase == id)
                    .Select(c => new
                    {
                        c.IdClase,
                        c.Familia,
                        c.SubFamilia,
                        Insumos = c.Insumos.Select(i => new
                        {
                            i.IdInsumo,
                            i.IdFox,
                            i.Nombre,
                            i.Presentacion,
                            i.PrecioUnitario,
                            Unidad = i.Unidad != null ? i.Unidad.Nombre : null,
                            i.CreatedAt
                        }).OrderBy(i => i.Nombre).ToList()
                    })
                    .FirstOrDefaultAsync();

                if (clase == null)
                {
                    return NotFound("Clase no encontrada");
                }

                return Ok(clase);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener clase {Id}", id);
                return StatusCode(500, "Error interno del servidor");
            }
        }

        /// <summary>
        /// Crear una nueva clase
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<Clase>> CreateClase([FromBody] ClaseCreateDto claseDto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                // Verificar si ya existe una clase con el mismo ID
                var existeClase = await _context.Clases.AnyAsync(c => c.IdClase == claseDto.IdClase);
                if (existeClase)
                {
                    return BadRequest("Ya existe una clase con ese código");
                }

                var clase = new Clase
                {
                    IdClase = claseDto.IdClase,
                    Familia = claseDto.Familia,
                    SubFamilia = claseDto.SubFamilia
                };

                _context.Clases.Add(clase);
                await _context.SaveChangesAsync();

                return CreatedAtAction(nameof(GetClase), new { id = clase.IdClase }, clase);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al crear clase");
                return StatusCode(500, "Error interno del servidor");
            }
        }

        /// <summary>
        /// Actualizar una clase existente
        /// </summary>
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateClase(string id, [FromBody] ClaseUpdateDto claseDto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var clase = await _context.Clases.FindAsync(id);
                if (clase == null)
                {
                    return NotFound("Clase no encontrada");
                }

                clase.Familia = claseDto.Familia ?? clase.Familia;
                clase.SubFamilia = claseDto.SubFamilia ?? clase.SubFamilia;

                await _context.SaveChangesAsync();

                return Ok(clase);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al actualizar clase {Id}", id);
                return StatusCode(500, "Error interno del servidor");
            }
        }

        /// <summary>
        /// Eliminar una clase
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteClase(string id)
        {
            try
            {
                var clase = await _context.Clases.FindAsync(id);
                if (clase == null)
                {
                    return NotFound("Clase no encontrada");
                }

                // Verificar si tiene insumos asociados
                var tieneInsumos = await _context.Insumos.AnyAsync(i => i.IdClase == id);
                if (tieneInsumos)
                {
                    return BadRequest("No se puede eliminar la clase porque tiene insumos asociados");
                }

                _context.Clases.Remove(clase);
                await _context.SaveChangesAsync();

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al eliminar clase {Id}", id);
                return StatusCode(500, "Error interno del servidor");
            }
        }

        /// <summary>
        /// Obtener jerarquía de familias y subfamilias
        /// </summary>
        [HttpGet("jerarquia")]
        public async Task<ActionResult<object>> GetJerarquia()
        {
            try
            {
                // Obtener datos básicos sin agregaciones complejas
                var clases = await _context.Clases
                    .Include(c => c.Insumos)
                    .ToListAsync();

                // Procesar jerarquía en memoria
                var jerarquia = clases
                    .GroupBy(c => c.Familia)
                    .Select(g => new
                    {
                        Familia = g.Key,
                        SubFamilias = g.Select(c => new
                        {
                            c.IdClase,
                            c.SubFamilia,
                            TotalInsumos = c.Insumos.Count()
                        }).ToList(),
                        TotalInsumos = g.Sum(c => c.Insumos.Count())
                    })
                    .OrderBy(f => f.Familia)
                    .ToList();

                return Ok(jerarquia);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener jerarquía de clases");
                return StatusCode(500, "Error interno del servidor");
            }
        }
    }

    // DTOs para Clases
    public class ClaseCreateDto
    {
        public string IdClase { get; set; } = string.Empty;
        public string? Familia { get; set; }
        public string? SubFamilia { get; set; }
    }

    public class ClaseUpdateDto
    {
        public string? Familia { get; set; }
        public string? SubFamilia { get; set; }
    }
} 