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
    public class InsumosController : ControllerBase
    {
        private readonly TexfinaDbContext _context;
        private readonly ILogger<InsumosController> _logger;

        public InsumosController(TexfinaDbContext context, ILogger<InsumosController> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Obtener todos los insumos con filtros opcionales
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<IEnumerable<object>>> GetInsumos(
            [FromQuery] string? buscar = null,
            [FromQuery] string? idClase = null,
            [FromQuery] int pagina = 1,
            [FromQuery] int tamaño = 10)
        {
            try
            {
                var query = _context.Insumos
                    .Include(i => i.Clase)
                    .Include(i => i.Unidad)
                    .Include(i => i.InsumoProveedores)
                        .ThenInclude(ip => ip.Proveedor)
                    .AsQueryable();

                // Filtros
                if (!string.IsNullOrEmpty(buscar))
                {
                    query = query.Where(i => i.Nombre!.Contains(buscar) || 
                                           i.IdFox!.Contains(buscar));
                }

                if (!string.IsNullOrEmpty(idClase))
                {
                    query = query.Where(i => i.IdClase == idClase);
                }

                // Paginación
                var total = await query.CountAsync();
                var insumos = await query
                    .Skip((pagina - 1) * tamaño)
                    .Take(tamaño)
                    .Select(i => new
                    {
                        i.IdInsumo,
                        i.IdFox,
                        i.Nombre,
                        i.PesoUnitario,
                        i.Presentacion,
                        i.PrecioUnitario,
                        i.CreatedAt,
                        i.UpdatedAt,
                        Clase = new { i.Clase!.IdClase, i.Clase.Familia, i.Clase.SubFamilia },
                        Unidad = new { i.Unidad!.IdUnidad, i.Unidad.Nombre },
                        Proveedores = i.InsumoProveedores.Select(ip => new
                        {
                            ip.Id,
                            ip.PrecioUnitario,
                            Proveedor = new { ip.Proveedor!.IdProveedor, ip.Proveedor.Empresa }
                        })
                    })
                    .ToListAsync();

                return Ok(new
                {
                    Data = insumos,
                    Total = total,
                    Pagina = pagina,
                    TotalPaginas = (int)Math.Ceiling(total / (double)tamaño)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener insumos");
                return StatusCode(500, "Error interno del servidor");
            }
        }

        /// <summary>
        /// Obtener un insumo específico por ID
        /// </summary>
        [HttpGet("{id}")]
        public async Task<ActionResult<object>> GetInsumo(int id)
        {
            try
            {
                var insumo = await _context.Insumos
                    .Include(i => i.Clase)
                    .Include(i => i.Unidad)
                    .Include(i => i.InsumoProveedores)
                        .ThenInclude(ip => ip.Proveedor)
                    .Include(i => i.Lotes)
                    .Include(i => i.Stocks)
                    .Where(i => i.IdInsumo == id)
                    .Select(i => new
                    {
                        i.IdInsumo,
                        i.IdFox,
                        i.Nombre,
                        i.IdClase,
                        i.PesoUnitario,
                        i.IdUnidad,
                        i.Presentacion,
                        i.PrecioUnitario,
                        i.CreatedAt,
                        i.UpdatedAt,
                        Clase = new { i.Clase!.IdClase, i.Clase.Familia, i.Clase.SubFamilia },
                        Unidad = new { i.Unidad!.IdUnidad, i.Unidad.Nombre },
                        Proveedores = i.InsumoProveedores.Select(ip => new
                        {
                            ip.Id,
                            ip.PrecioUnitario,
                            Proveedor = new 
                            { 
                                ip.Proveedor!.IdProveedor, 
                                ip.Proveedor.Empresa,
                                ip.Proveedor.Ruc,
                                ip.Proveedor.Contacto
                            }
                        }),
                        TotalStock = i.Stocks.Sum(s => s.Cantidad ?? 0),
                        LotesActivos = i.Lotes.Count(l => l.EstadoLote == "ACTIVO")
                    })
                    .FirstOrDefaultAsync();

                if (insumo == null)
                {
                    return NotFound("Insumo no encontrado");
                }

                return Ok(insumo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener insumo {Id}", id);
                return StatusCode(500, "Error interno del servidor");
            }
        }

        /// <summary>
        /// Crear un nuevo insumo
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<Insumo>> CreateInsumo([FromBody] InsumoCreateDto insumoDto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                // Verificar que la clase existe
                if (!string.IsNullOrEmpty(insumoDto.IdClase))
                {
                    var claseExists = await _context.Clases.AnyAsync(c => c.IdClase == insumoDto.IdClase);
                    if (!claseExists)
                    {
                        return BadRequest("La clase especificada no existe");
                    }
                }

                // Verificar que la unidad existe
                if (!string.IsNullOrEmpty(insumoDto.IdUnidad))
                {
                    var unidadExists = await _context.Unidades.AnyAsync(u => u.IdUnidad == insumoDto.IdUnidad);
                    if (!unidadExists)
                    {
                        return BadRequest("La unidad especificada no existe");
                    }
                }

                var insumo = new Insumo
                {
                    IdFox = insumoDto.IdFox,
                    Nombre = insumoDto.Nombre,
                    IdClase = insumoDto.IdClase,
                    PesoUnitario = insumoDto.PesoUnitario,
                    IdUnidad = insumoDto.IdUnidad,
                    Presentacion = insumoDto.Presentacion,
                    PrecioUnitario = insumoDto.PrecioUnitario,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                };

                _context.Insumos.Add(insumo);
                await _context.SaveChangesAsync();

                return CreatedAtAction(nameof(GetInsumo), new { id = insumo.IdInsumo }, insumo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al crear insumo");
                return StatusCode(500, "Error interno del servidor");
            }
        }

        /// <summary>
        /// Actualizar un insumo existente
        /// </summary>
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateInsumo(int id, [FromBody] InsumoUpdateDto insumoDto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var insumo = await _context.Insumos.FindAsync(id);
                if (insumo == null)
                {
                    return NotFound("Insumo no encontrado");
                }

                // Actualizar propiedades
                insumo.IdFox = insumoDto.IdFox ?? insumo.IdFox;
                insumo.Nombre = insumoDto.Nombre ?? insumo.Nombre;
                insumo.IdClase = insumoDto.IdClase ?? insumo.IdClase;
                insumo.PesoUnitario = insumoDto.PesoUnitario ?? insumo.PesoUnitario;
                insumo.IdUnidad = insumoDto.IdUnidad ?? insumo.IdUnidad;
                insumo.Presentacion = insumoDto.Presentacion ?? insumo.Presentacion;
                insumo.PrecioUnitario = insumoDto.PrecioUnitario ?? insumo.PrecioUnitario;
                insumo.UpdatedAt = DateTime.Now;

                await _context.SaveChangesAsync();

                return Ok(insumo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al actualizar insumo {Id}", id);
                return StatusCode(500, "Error interno del servidor");
            }
        }

        /// <summary>
        /// Eliminar un insumo
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteInsumo(int id)
        {
            try
            {
                var insumo = await _context.Insumos.FindAsync(id);
                if (insumo == null)
                {
                    return NotFound("Insumo no encontrado");
                }

                // Verificar si tiene movimientos
                var tieneMovimientos = await _context.Stocks.AnyAsync(s => s.IdInsumo == id) ||
                                     await _context.Ingresos.AnyAsync(i => i.IdInsumo == id) ||
                                     await _context.Consumos.AnyAsync(c => c.IdInsumo == id);

                if (tieneMovimientos)
                {
                    return BadRequest("No se puede eliminar el insumo porque tiene movimientos asociados");
                }

                _context.Insumos.Remove(insumo);
                await _context.SaveChangesAsync();

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al eliminar insumo {Id}", id);
                return StatusCode(500, "Error interno del servidor");
            }
        }

        /// <summary>
        /// Buscar insumos por nombre o código FOX
        /// </summary>
        [HttpGet("buscar")]
        public async Task<ActionResult<IEnumerable<object>>> BuscarInsumos([FromQuery] string termino)
        {
            try
            {
                if (string.IsNullOrEmpty(termino))
                {
                    return BadRequest("Debe proporcionar un término de búsqueda");
                }

                var insumos = await _context.Insumos
                    .Include(i => i.Clase)
                    .Include(i => i.Unidad)
                    .Where(i => i.Nombre!.Contains(termino) || 
                              i.IdFox!.Contains(termino))
                    .Take(20)
                    .Select(i => new
                    {
                        i.IdInsumo,
                        i.IdFox,
                        i.Nombre,
                        i.PrecioUnitario,
                        Clase = i.Clase!.Familia,
                        Unidad = i.Unidad!.Nombre
                    })
                    .ToListAsync();

                return Ok(insumos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al buscar insumos");
                return StatusCode(500, "Error interno del servidor");
            }
        }

        /// <summary>
        /// Obtener estadísticas generales de insumos
        /// </summary>
        [HttpGet("estadisticas")]
        public async Task<ActionResult<object>> GetEstadisticasInsumos()
        {
            try
            {
                var totalInsumos = await _context.Insumos.CountAsync();
                var insumosConStock = await _context.Insumos
                    .Where(i => i.Stocks.Any(s => s.Cantidad > 0))
                    .CountAsync();
                
                var totalStock = await _context.Stocks
                    .SumAsync(s => s.Cantidad ?? 0);
                
                var valorTotalInventario = await _context.Stocks
                    .Include(s => s.Insumo)
                    .SumAsync(s => (s.Cantidad ?? 0) * (s.Insumo!.PrecioUnitario ?? 0));

                var insumosPorClase = await _context.Insumos
                    .Include(i => i.Clase)
                    .GroupBy(i => i.Clase!.Familia)
                    .Select(g => new
                    {
                        Clase = g.Key,
                        Cantidad = g.Count()
                    })
                    .ToListAsync();

                return Ok(new
                {
                    TotalInsumos = totalInsumos,
                    InsumosConStock = insumosConStock,
                    InsumosSinStock = totalInsumos - insumosConStock,
                    TotalStock = totalStock,
                    ValorTotalInventario = valorTotalInventario,
                    InsumosPorClase = insumosPorClase,
                    FechaConsulta = DateTime.Now
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener estadísticas de insumos");
                return StatusCode(500, "Error interno del servidor");
            }
        }

        /// <summary>
        /// Obtener insumos con stock bajo (criterio configurable)
        /// </summary>
        [HttpGet("bajo-stock")]
        public async Task<ActionResult<IEnumerable<object>>> GetInsumosBajoStock([FromQuery] float umbral = 10)
        {
            try
            {
                var insumosBajoStock = await _context.Insumos
                    .Include(i => i.Clase)
                    .Include(i => i.Unidad)
                    .Include(i => i.Stocks)
                        .ThenInclude(s => s.Almacen)
                    .Where(i => i.Stocks.Sum(s => s.Cantidad ?? 0) <= umbral)
                    .Select(i => new
                    {
                        i.IdInsumo,
                        i.IdFox,
                        i.Nombre,
                        i.PrecioUnitario,
                        Clase = new { i.Clase!.IdClase, i.Clase.Familia },
                        Unidad = new { i.Unidad!.IdUnidad, i.Unidad.Nombre },
                        StockActual = i.Stocks.Sum(s => s.Cantidad ?? 0),
                        UmbralMinimo = umbral,
                        Deficit = umbral - i.Stocks.Sum(s => s.Cantidad ?? 0),
                        Almacenes = i.Stocks.Where(s => s.Cantidad > 0).Select(s => new
                        {
                            s.Almacen!.IdAlmacen,
                            s.Almacen.Nombre,
                            Cantidad = s.Cantidad ?? 0
                        })
                    })
                    .OrderBy(i => i.StockActual)
                    .ToListAsync();

                return Ok(new
                {
                    UmbralConsultado = umbral,
                    TotalInsumosBajoStock = insumosBajoStock.Count,
                    Insumos = insumosBajoStock,
                    FechaConsulta = DateTime.Now
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener insumos con bajo stock");
                return StatusCode(500, "Error interno del servidor");
            }
        }
    }

    // DTOs para Insumos
    public class InsumoCreateDto
    {
        public string? IdFox { get; set; }
        public string? Nombre { get; set; }
        public string? IdClase { get; set; }
        public float? PesoUnitario { get; set; }
        public string? IdUnidad { get; set; }
        public string? Presentacion { get; set; }
        public float? PrecioUnitario { get; set; }
    }

    public class InsumoUpdateDto
    {
        public string? IdFox { get; set; }
        public string? Nombre { get; set; }
        public string? IdClase { get; set; }
        public float? PesoUnitario { get; set; }
        public string? IdUnidad { get; set; }
        public string? Presentacion { get; set; }
        public float? PrecioUnitario { get; set; }
    }
} 