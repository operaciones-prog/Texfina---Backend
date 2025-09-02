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
    public class AlmacenesController : ControllerBase
    {
        private readonly TexfinaDbContext _context;
        private readonly ILogger<AlmacenesController> _logger;

        public AlmacenesController(TexfinaDbContext context, ILogger<AlmacenesController> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Obtener todos los almacenes con sus estadísticas
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<IEnumerable<object>>> GetAlmacenes([FromQuery] string? buscar = null)
        {
            try
            {
                var query = _context.Almacenes
                    .Include(a => a.Stocks)
                        .ThenInclude(s => s.Insumo)
                    .AsQueryable();

                if (!string.IsNullOrEmpty(buscar))
                {
                    query = query.Where(a => a.Nombre!.Contains(buscar) || 
                                           a.Ubicacion!.Contains(buscar));
                }

                var almacenes = await query
                    .Select(a => new
                    {
                        a.IdAlmacen,
                        a.Nombre,
                        a.Ubicacion,
                        TotalItems = a.Stocks.Count(s => s.Cantidad > 0),
                        StockTotal = a.Stocks.Sum(s => s.Cantidad ?? 0),
                        ValorEstimado = a.Stocks.Sum(s => (s.Cantidad ?? 0) * (s.Insumo!.PrecioUnitario ?? 0)),
                        TiposInsumos = a.Stocks.Select(s => s.Insumo!.IdClase).Distinct().Count(),
                        UltimaActividad = a.Stocks.Max(s => s.FechaEntrada)
                    })
                    .OrderBy(a => a.Nombre)
                    .ToListAsync();

                return Ok(almacenes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener almacenes");
                return StatusCode(500, "Error interno del servidor");
            }
        }

        /// <summary>
        /// Obtener un almacén específico con su inventario detallado
        /// </summary>
        [HttpGet("{id}")]
        public async Task<ActionResult<object>> GetAlmacen(int id)
        {
            try
            {
                var almacen = await _context.Almacenes
                    .Include(a => a.Stocks)
                        .ThenInclude(s => s.Insumo)
                            .ThenInclude(i => i!.Clase)
                    .Include(a => a.Stocks)
                        .ThenInclude(s => s.Lote)
                    .Where(a => a.IdAlmacen == id)
                    .Select(a => new
                    {
                        a.IdAlmacen,
                        a.Nombre,
                        a.Ubicacion,
                        Inventario = a.Stocks.Where(s => s.Cantidad > 0).Select(s => new
                        {
                            s.IdStock,
                            s.Cantidad,
                            s.Presentacion,
                            s.FechaEntrada,
                            Insumo = new
                            {
                                s.Insumo!.IdInsumo,
                                s.Insumo.IdFox,
                                s.Insumo.Nombre,
                                s.Insumo.PrecioUnitario,
                                Clase = s.Insumo.Clase != null ? s.Insumo.Clase.Familia : null
                            },
                            Lote = s.Lote != null ? new
                            {
                                s.Lote.IdLote,
                                Numero = s.Lote.Numero,
                                FechaExpiracion = s.Lote.FechaExpiracion.HasValue ? 
                                    s.Lote.FechaExpiracion.Value.ToDateTime(TimeOnly.MinValue) : (DateTime?)null,
                                s.Lote.EstadoLote
                            } : null,
                            ValorTotal = (s.Cantidad ?? 0) * (s.Insumo.PrecioUnitario ?? 0)
                        }).OrderBy(s => s.Insumo.Nombre),
                        Resumen = new
                        {
                            TotalItems = a.Stocks.Count(s => s.Cantidad > 0),
                            StockTotal = a.Stocks.Sum(s => s.Cantidad ?? 0),
                            ValorTotal = a.Stocks.Sum(s => (s.Cantidad ?? 0) * (s.Insumo!.PrecioUnitario ?? 0)),
                            TiposInsumos = a.Stocks.Select(s => s.Insumo!.IdClase).Distinct().Count()
                        }
                    })
                    .FirstOrDefaultAsync();

                if (almacen == null)
                {
                    return NotFound("Almacén no encontrado");
                }

                return Ok(almacen);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener almacén {Id}", id);
                return StatusCode(500, "Error interno del servidor");
            }
        }

        /// <summary>
        /// Crear un nuevo almacén
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<Almacen>> CreateAlmacen([FromBody] AlmacenCreateDto almacenDto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                // Verificar si ya existe un almacén con el mismo nombre
                var existeAlmacen = await _context.Almacenes
                    .AnyAsync(a => a.Nombre == almacenDto.Nombre);

                if (existeAlmacen)
                {
                    return BadRequest("Ya existe un almacén con ese nombre");
                }

                var almacen = new Almacen
                {
                    Nombre = almacenDto.Nombre,
                    Ubicacion = almacenDto.Ubicacion
                };

                _context.Almacenes.Add(almacen);
                await _context.SaveChangesAsync();

                return CreatedAtAction(nameof(GetAlmacen), new { id = almacen.IdAlmacen }, almacen);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al crear almacén");
                return StatusCode(500, "Error interno del servidor");
            }
        }

        /// <summary>
        /// Actualizar un almacén existente
        /// </summary>
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateAlmacen(int id, [FromBody] AlmacenUpdateDto almacenDto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var almacen = await _context.Almacenes.FindAsync(id);
                if (almacen == null)
                {
                    return NotFound("Almacén no encontrado");
                }

                // Verificar nombre duplicado si se está cambiando
                if (!string.IsNullOrEmpty(almacenDto.Nombre) && almacenDto.Nombre != almacen.Nombre)
                {
                    var existeNombre = await _context.Almacenes
                        .AnyAsync(a => a.Nombre == almacenDto.Nombre && a.IdAlmacen != id);

                    if (existeNombre)
                    {
                        return BadRequest("Ya existe un almacén con ese nombre");
                    }
                }

                almacen.Nombre = almacenDto.Nombre ?? almacen.Nombre;
                almacen.Ubicacion = almacenDto.Ubicacion ?? almacen.Ubicacion;

                await _context.SaveChangesAsync();

                return Ok(almacen);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al actualizar almacén {Id}", id);
                return StatusCode(500, "Error interno del servidor");
            }
        }

        /// <summary>
        /// Eliminar un almacén
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteAlmacen(int id)
        {
            try
            {
                var almacen = await _context.Almacenes.FindAsync(id);
                if (almacen == null)
                {
                    return NotFound("Almacén no encontrado");
                }

                // Verificar si tiene stock
                var tieneStock = await _context.Stocks.AnyAsync(s => s.IdAlmacen == id && s.Cantidad > 0);
                if (tieneStock)
                {
                    return BadRequest("No se puede eliminar el almacén porque tiene stock activo");
                }

                _context.Almacenes.Remove(almacen);
                await _context.SaveChangesAsync();

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al eliminar almacén {Id}", id);
                return StatusCode(500, "Error interno del servidor");
            }
        }

        /// <summary>
        /// Obtener estadísticas de ocupación de almacenes
        /// </summary>
        [HttpGet("estadisticas")]
        public async Task<ActionResult<object>> GetEstadisticas()
        {
            try
            {
                var estadisticas = await _context.Almacenes
                    .Include(a => a.Stocks)
                        .ThenInclude(s => s.Insumo)
                    .Select(a => new
                    {
                        a.IdAlmacen,
                        a.Nombre,
                        a.Ubicacion,
                        ItemsActivos = a.Stocks.Count(s => s.Cantidad > 0),
                        StockTotal = a.Stocks.Sum(s => s.Cantidad ?? 0),
                        ValorInventario = a.Stocks.Sum(s => (s.Cantidad ?? 0) * (s.Insumo!.PrecioUnitario ?? 0)),
                        ClasesInsumos = a.Stocks.Select(s => s.Insumo!.IdClase).Distinct().Count(),
                        PorcentajeUso = a.Stocks.Any() ? 
                            (double)a.Stocks.Count(s => s.Cantidad > 0) / a.Stocks.Count() * 100 : 0
                    })
                    .ToListAsync();

                var resumenGeneral = new
                {
                    TotalAlmacenes = estadisticas.Count,
                    ItemsTotales = estadisticas.Sum(e => e.ItemsActivos),
                    StockTotalSistema = estadisticas.Sum(e => e.StockTotal),
                    ValorTotalInventario = estadisticas.Sum(e => e.ValorInventario),
                    AlmacenMasGrande = estadisticas.OrderByDescending(e => e.ItemsActivos).FirstOrDefault()?.Nombre,
                    AlmacenMasValioso = estadisticas.OrderByDescending(e => e.ValorInventario).FirstOrDefault()?.Nombre
                };

                return Ok(new
                {
                    ResumenGeneral = resumenGeneral,
                    DetalleAlmacenes = estadisticas
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener estadísticas de almacenes");
                return StatusCode(500, "Error interno del servidor");
            }
        }

        /// <summary>
        /// Transferir stock entre almacenes
        /// </summary>
        [HttpPost("{idOrigen}/transferir/{idDestino}")]
        public async Task<IActionResult> TransferirStock(int idOrigen, int idDestino, [FromBody] TransferenciaStockDto dto)
        {
            try
            {
                using var transaction = await _context.Database.BeginTransactionAsync();

                // Verificar almacenes existen
                var almacenOrigen = await _context.Almacenes.FindAsync(idOrigen);
                var almacenDestino = await _context.Almacenes.FindAsync(idDestino);

                if (almacenOrigen == null || almacenDestino == null)
                {
                    return BadRequest("Uno o ambos almacenes no existen");
                }

                // Verificar stock disponible
                var stockOrigen = await _context.Stocks
                    .FirstOrDefaultAsync(s => s.IdStock == dto.IdStock && s.IdAlmacen == idOrigen);

                if (stockOrigen == null || stockOrigen.Cantidad < dto.Cantidad)
                {
                    return BadRequest("Stock insuficiente en el almacén origen");
                }

                // Reducir stock en origen
                stockOrigen.Cantidad -= dto.Cantidad;

                // Buscar o crear stock en destino
                var stockDestino = await _context.Stocks
                    .FirstOrDefaultAsync(s => s.IdInsumo == stockOrigen.IdInsumo && 
                                            s.IdLote == stockOrigen.IdLote && 
                                            s.IdAlmacen == idDestino);

                if (stockDestino != null)
                {
                    stockDestino.Cantidad += dto.Cantidad;
                }
                else
                {
                    stockDestino = new Stock
                    {
                        IdInsumo = stockOrigen.IdInsumo,
                        IdLote = stockOrigen.IdLote,
                        IdAlmacen = idDestino,
                        IdUnidad = stockOrigen.IdUnidad,
                        Presentacion = stockOrigen.Presentacion,
                        Cantidad = dto.Cantidad,
                        FechaEntrada = DateTime.Now
                    };
                    _context.Stocks.Add(stockDestino);
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new 
                { 
                    mensaje = "Transferencia realizada exitosamente",
                    stockOrigenActual = stockOrigen.Cantidad,
                    stockDestinoActual = stockDestino.Cantidad
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al transferir stock entre almacenes");
                return StatusCode(500, "Error interno del servidor");
            }
        }
    }

    // DTOs para Almacenes
    public class AlmacenCreateDto
    {
        public string? Nombre { get; set; }
        public string? Ubicacion { get; set; }
    }

    public class AlmacenUpdateDto
    {
        public string? Nombre { get; set; }
        public string? Ubicacion { get; set; }
    }

    public class TransferenciaStockDto
    {
        public int IdStock { get; set; }
        public float Cantidad { get; set; }
    }
} 