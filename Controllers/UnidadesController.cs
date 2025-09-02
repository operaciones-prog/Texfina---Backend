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
    public class UnidadesController : ControllerBase
    {
        private readonly TexfinaDbContext _context;
        private readonly ILogger<UnidadesController> _logger;

        public UnidadesController(TexfinaDbContext context, ILogger<UnidadesController> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Obtener todas las unidades con sus insumos asociados
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<IEnumerable<object>>> GetUnidades([FromQuery] string? buscar = null)
        {
            try
            {
                var query = _context.Unidades
                    .Include(u => u.Insumos)
                    .Include(u => u.Stocks)
                    .AsQueryable();

                if (!string.IsNullOrEmpty(buscar))
                {
                    query = query.Where(u => u.Nombre!.Contains(buscar) || 
                                           u.IdUnidad!.Contains(buscar));
                }

                var unidades = await query
                    .Select(u => new
                    {
                        u.IdUnidad,
                        u.Nombre,
                        TotalInsumos = u.Insumos.Count(),
                        TotalMovimientos = u.Stocks.Count(),
                        EsUsada = u.Insumos.Any() || u.Stocks.Any()
                    })
                    .OrderBy(u => u.Nombre)
                    .ToListAsync();

                return Ok(unidades);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener unidades");
                return StatusCode(500, "Error interno del servidor");
            }
        }

        /// <summary>
        /// Obtener una unidad específica con sus insumos asociados
        /// </summary>
        [HttpGet("{id}")]
        public async Task<ActionResult<object>> GetUnidad(string id)
        {
            try
            {
                var unidad = await _context.Unidades
                    .Include(u => u.Insumos)
                        .ThenInclude(i => i.Clase)
                    .Where(u => u.IdUnidad == id)
                    .Select(u => new
                    {
                        u.IdUnidad,
                        u.Nombre,
                        Insumos = u.Insumos.Select(i => new
                        {
                            i.IdInsumo,
                            i.IdFox,
                            i.Nombre,
                            i.Presentacion,
                            i.PrecioUnitario,
                            Clase = i.Clase != null ? new
                            {
                                i.Clase.IdClase,
                                i.Clase.Familia
                            } : null
                        }).OrderBy(i => i.Nombre).ToList()
                    })
                    .FirstOrDefaultAsync();

                if (unidad == null)
                {
                    return NotFound("Unidad no encontrada");
                }

                return Ok(unidad);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener unidad {Id}", id);
                return StatusCode(500, "Error interno del servidor");
            }
        }

        /// <summary>
        /// Crear una nueva unidad
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<Unidad>> CreateUnidad([FromBody] UnidadCreateDto unidadDto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                // Verificar si ya existe una unidad con el mismo ID
                var existeUnidad = await _context.Unidades.AnyAsync(u => u.IdUnidad == unidadDto.IdUnidad);
                if (existeUnidad)
                {
                    return BadRequest("Ya existe una unidad con ese código");
                }

                var unidad = new Unidad
                {
                    IdUnidad = unidadDto.IdUnidad,
                    Nombre = unidadDto.Nombre
                };

                _context.Unidades.Add(unidad);
                await _context.SaveChangesAsync();

                return CreatedAtAction(nameof(GetUnidad), new { id = unidad.IdUnidad }, unidad);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al crear unidad");
                return StatusCode(500, "Error interno del servidor");
            }
        }

        /// <summary>
        /// Actualizar una unidad existente
        /// </summary>
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateUnidad(string id, [FromBody] UnidadUpdateDto unidadDto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var unidad = await _context.Unidades.FindAsync(id);
                if (unidad == null)
                {
                    return NotFound("Unidad no encontrada");
                }

                unidad.Nombre = unidadDto.Nombre ?? unidad.Nombre;

                await _context.SaveChangesAsync();

                return Ok(unidad);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al actualizar unidad {Id}", id);
                return StatusCode(500, "Error interno del servidor");
            }
        }

        /// <summary>
        /// Eliminar una unidad
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteUnidad(string id)
        {
            try
            {
                var unidad = await _context.Unidades.FindAsync(id);
                if (unidad == null)
                {
                    return NotFound("Unidad no encontrada");
                }

                // Verificar si tiene insumos o movimientos asociados
                var tieneInsumos = await _context.Insumos.AnyAsync(i => i.IdUnidad == id);
                var tieneMovimientos = await _context.Stocks.AnyAsync(s => s.IdUnidad == id) ||
                                     await _context.Ingresos.AnyAsync(i => i.IdUnidad == id);

                if (tieneInsumos || tieneMovimientos)
                {
                    return BadRequest("No se puede eliminar la unidad porque tiene insumos o movimientos asociados");
                }

                _context.Unidades.Remove(unidad);
                await _context.SaveChangesAsync();

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al eliminar unidad {Id}", id);
                return StatusCode(500, "Error interno del servidor");
            }
        }

        /// <summary>
        /// Obtener unidades más utilizadas
        /// </summary>
        [HttpGet("mas-utilizadas")]
        public async Task<ActionResult<IEnumerable<object>>> GetUnidadesMasUtilizadas()
        {
            try
            {
                var unidades = await _context.Unidades
                    .Include(u => u.Insumos)
                    .Include(u => u.Stocks)
                    .Select(u => new
                    {
                        u.IdUnidad,
                        u.Nombre,
                        TotalInsumos = u.Insumos.Count(),
                        TotalMovimientos = u.Stocks.Count(),
                        TotalUso = u.Insumos.Count() + u.Stocks.Count()
                    })
                    .Where(u => u.TotalUso > 0)
                    .OrderByDescending(u => u.TotalUso)
                    .Take(10)
                    .ToListAsync();

                return Ok(unidades);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener unidades más utilizadas");
                return StatusCode(500, "Error interno del servidor");
            }
        }

        /// <summary>
        /// Buscar unidades por nombre o código
        /// </summary>
        [HttpGet("buscar")]
        public async Task<ActionResult<IEnumerable<object>>> BuscarUnidades([FromQuery] string termino)
        {
            try
            {
                if (string.IsNullOrEmpty(termino))
                {
                    return BadRequest("Debe proporcionar un término de búsqueda");
                }

                var unidades = await _context.Unidades
                    .Where(u => u.Nombre!.Contains(termino) || 
                              u.IdUnidad!.Contains(termino))
                    .Take(20)
                    .Select(u => new
                    {
                        u.IdUnidad,
                        u.Nombre
                    })
                    .ToListAsync();

                return Ok(unidades);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al buscar unidades");
                return StatusCode(500, "Error interno del servidor");
            }
        }
    }

    // DTOs para Unidades
    public class UnidadCreateDto
    {
        public string IdUnidad { get; set; } = string.Empty;
        public string? Nombre { get; set; }
    }

    public class UnidadUpdateDto
    {
        public string? Nombre { get; set; }
    }
} 