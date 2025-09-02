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
    public class StocksController : ControllerBase
    {
        private readonly TexfinaDbContext _context;
        private readonly ILogger<StocksController> _logger;

        public StocksController(TexfinaDbContext context, ILogger<StocksController> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Obtener lista de stocks con filtros
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<object>> GetStocks(
            [FromQuery] string? buscar = null,
            [FromQuery] int? idInsumo = null,
            [FromQuery] int? idAlmacen = null,
            [FromQuery] bool? stockBajo = null,
            [FromQuery] int pagina = 1,
            [FromQuery] int tamaño = 10)
        {
            try
            {
                var query = _context.Stocks
                    .Include(s => s.Insumo)
                        .ThenInclude(i => i!.Clase)
                    .Include(s => s.Almacen)
                    .Include(s => s.Lote)
                    .AsQueryable();

                // Filtros
                if (!string.IsNullOrEmpty(buscar))
                {
                    query = query.Where(s => s.Insumo!.Nombre!.Contains(buscar) ||
                                           s.Insumo.IdFox!.Contains(buscar) ||
                                           s.Almacen!.Nombre!.Contains(buscar));
                }

                if (idInsumo.HasValue)
                {
                    query = query.Where(s => s.IdInsumo == idInsumo);
                }

                if (idAlmacen.HasValue)
                {
                    query = query.Where(s => s.IdAlmacen == idAlmacen);
                }

                if (stockBajo.HasValue && stockBajo.Value)
                {
                    query = query.Where(s => s.Cantidad <= 10 && s.Cantidad > 0);
                }

                // Paginación
                var total = await query.CountAsync();
                
                var stocks = await query
                    .OrderByDescending(s => s.Cantidad)
                    .Skip((pagina - 1) * tamaño)
                    .Take(tamaño)
                    .Select(s => new
                    {
                        s.IdStock,
                        s.Cantidad,
                        s.FechaEntrada,
                        s.FechaSalida,
                        Insumo = new 
                        { 
                            s.Insumo!.IdInsumo, 
                            s.Insumo.IdFox, 
                            s.Insumo.Nombre, 
                            s.Insumo.PrecioUnitario,
                            Clase = s.Insumo.Clase != null ? s.Insumo.Clase.Familia : null
                        },
                        Almacen = new { s.Almacen!.IdAlmacen, s.Almacen.Nombre },
                        Lote = s.Lote != null ? new 
                        { 
                            s.Lote.IdLote, 
                            s.Lote.Numero, 
                            s.Lote.FechaExpiracion 
                        } : null,
                        ValorTotal = (s.Cantidad ?? 0) * (s.Insumo.PrecioUnitario ?? 0),
                        EstadoStock = s.Cantidad <= 0 ? "SIN_STOCK" :
                                     s.Cantidad <= 10 ? "STOCK_BAJO" : "STOCK_NORMAL"
                    })
                    .ToListAsync();

                return Ok(new
                {
                    Data = stocks,
                    Total = total,
                    Pagina = pagina,
                    TotalPaginas = (int)Math.Ceiling(total / (double)tamaño),
                    Filtros = new { buscar, idInsumo, idAlmacen, stockBajo }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener stocks");
                return StatusCode(500, "Error interno del servidor");
            }
        }

        /// <summary>
        /// Obtener resumen de stocks por almacén
        /// </summary>
        [HttpGet("resumen")]
        public async Task<ActionResult<object>> GetResumenStocks()
        {
            try
            {
                var resumen = await _context.Stocks
                    .Include(s => s.Insumo)
                    .Include(s => s.Almacen)
                    .GroupBy(s => 1)
                    .Select(g => new
                    {
                        TotalItems = g.Count(),
                        TotalConStock = g.Count(s => s.Cantidad > 0),
                        TotalSinStock = g.Count(s => s.Cantidad <= 0),
                        StockBajo = g.Count(s => s.Cantidad <= 10 && s.Cantidad > 0),
                        ValorTotalInventario = g.Sum(s => (s.Cantidad ?? 0) * (s.Insumo!.PrecioUnitario ?? 0)),
                        CantidadTotalStock = g.Sum(s => s.Cantidad ?? 0)
                    })
                    .FirstOrDefaultAsync();

                var estadisticasPorAlmacen = await _context.Stocks
                    .Include(s => s.Insumo)
                    .Include(s => s.Almacen)
                    .Where(s => s.Cantidad > 0)
                    .GroupBy(s => new { s.IdAlmacen, s.Almacen!.Nombre })
                    .Select(g => new
                    {
                        g.Key.IdAlmacen,
                        NombreAlmacen = g.Key.Nombre,
                        TotalItems = g.Count(),
                        CantidadTotal = g.Sum(s => s.Cantidad ?? 0),
                        ValorTotal = g.Sum(s => (s.Cantidad ?? 0) * (s.Insumo!.PrecioUnitario ?? 0)),
                        StockBajo = g.Count(s => s.Cantidad <= 10)
                    })
                    .OrderByDescending(a => a.ValorTotal)
                    .ToListAsync();

                return Ok(new
                {
                    ResumenGeneral = resumen,
                    EstadisticasPorAlmacen = estadisticasPorAlmacen,
                    FechaConsulta = DateTime.Now
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener resumen de stocks");
                return StatusCode(500, "Error interno del servidor");
            }
        }

        /// <summary>
        /// Obtener stocks agrupados por almacén
        /// </summary>
        [HttpGet("por-almacen")]
        public async Task<ActionResult<object>> GetStocksPorAlmacen([FromQuery] int? idAlmacen = null)
        {
            try
            {
                var query = _context.Stocks
                    .Include(s => s.Insumo)
                        .ThenInclude(i => i!.Clase)
                    .Include(s => s.Almacen)
                    .Where(s => s.Cantidad > 0)
                    .AsQueryable();

                if (idAlmacen.HasValue)
                {
                    query = query.Where(s => s.IdAlmacen == idAlmacen);
                }

                var stocksPorAlmacen = await query
                    .GroupBy(s => new { s.IdAlmacen, s.Almacen!.Nombre })
                    .Select(g => new
                    {
                        g.Key.IdAlmacen,
                        NombreAlmacen = g.Key.Nombre,
                        TotalItems = g.Count(),
                        CantidadTotal = g.Sum(s => s.Cantidad ?? 0),
                        ValorTotal = g.Sum(s => (s.Cantidad ?? 0) * (s.Insumo!.PrecioUnitario ?? 0)),
                        Insumos = g.Select(s => new
                        {
                            s.IdStock,
                            s.Cantidad,
                            Insumo = new 
                            { 
                                s.Insumo!.IdInsumo, 
                                s.Insumo.IdFox, 
                                s.Insumo.Nombre,
                                Clase = s.Insumo.Clase != null ? s.Insumo.Clase.Familia : null
                            },
                            ValorLinea = (s.Cantidad ?? 0) * (s.Insumo.PrecioUnitario ?? 0)
                        }).OrderBy(i => i.Insumo.Nombre).ToList()
                    })
                    .OrderBy(a => a.NombreAlmacen)
                    .ToListAsync();

                return Ok(stocksPorAlmacen);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener stocks por almacén");
                return StatusCode(500, "Error interno del servidor");
            }
        }

        /// <summary>
        /// Obtener stocks bajo mínimo establecido
        /// </summary>
        [HttpGet("bajo-minimo")]
        public async Task<ActionResult<object>> GetStocksBajoMinimo([FromQuery] int minimo = 10)
        {
            try
            {
                var stocksBajos = await _context.Stocks
                    .Include(s => s.Insumo)
                        .ThenInclude(i => i!.Clase)
                    .Include(s => s.Almacen)
                    .Where(s => s.Cantidad <= minimo && s.Cantidad > 0)
                    .Select(s => new
                    {
                        s.IdStock,
                        s.Cantidad,
                        s.FechaEntrada,
                        Insumo = new 
                        { 
                            s.Insumo!.IdInsumo, 
                            s.Insumo.IdFox, 
                            s.Insumo.Nombre,
                            s.Insumo.PrecioUnitario,
                            Clase = s.Insumo.Clase != null ? s.Insumo.Clase.Familia : null
                        },
                        Almacen = new { s.Almacen!.IdAlmacen, s.Almacen.Nombre },
                        ValorStock = (s.Cantidad ?? 0) * (s.Insumo.PrecioUnitario ?? 0),
                        Criticidad = s.Cantidad <= 3 ? "CRÍTICO" :
                                   s.Cantidad <= 5 ? "ALTO" : 
                                   s.Cantidad <= 10 ? "MEDIO" : "BAJO"
                    })
                    .OrderBy(s => s.Cantidad)
                    .ToListAsync();

                var resumen = new
                {
                    TotalItemsBajoMinimo = stocksBajos.Count,
                    ItemsCriticos = stocksBajos.Count(s => s.Criticidad == "CRÍTICO"),
                    ItemsAltos = stocksBajos.Count(s => s.Criticidad == "ALTO"),
                    ValorTotalEnRiesgo = stocksBajos.Sum(s => s.ValorStock),
                    MinimoEstablecido = minimo
                };

                return Ok(new
                {
                    Resumen = resumen,
                    Detalle = stocksBajos,
                    FechaConsulta = DateTime.Now
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener stocks bajo mínimo");
                return StatusCode(500, "Error interno del servidor");
            }
        }

        /// <summary>
        /// Obtener stock específico por ID
        /// </summary>
        [HttpGet("{id}")]
        public async Task<ActionResult<object>> GetStock(int id)
        {
            try
            {
                var stock = await _context.Stocks
                    .Include(s => s.Insumo)
                        .ThenInclude(i => i!.Clase)
                    .Include(s => s.Insumo)
                        .ThenInclude(i => i!.Unidad)
                    .Include(s => s.Almacen)
                    .Include(s => s.Lote)
                    .Where(s => s.IdStock == id)
                    .Select(s => new
                    {
                        s.IdStock,
                        s.Cantidad,
                        s.FechaEntrada,
                        s.FechaSalida,
                        Insumo = new 
                        { 
                            s.Insumo!.IdInsumo, 
                            s.Insumo.IdFox, 
                            s.Insumo.Nombre,
                            s.Insumo.Presentacion,
                            s.Insumo.PrecioUnitario,
                            Clase = s.Insumo.Clase != null ? new 
                            { 
                                s.Insumo.Clase.IdClase, 
                                s.Insumo.Clase.Familia 
                            } : null,
                            Unidad = s.Insumo.Unidad != null ? new 
                            { 
                                s.Insumo.Unidad.IdUnidad, 
                                s.Insumo.Unidad.Nombre 
                            } : null
                        },
                        Almacen = new 
                        { 
                            s.Almacen!.IdAlmacen, 
                            s.Almacen.Nombre, 
                            s.Almacen.Ubicacion 
                        },
                        Lote = s.Lote != null ? new 
                        { 
                            s.Lote.IdLote, 
                            s.Lote.Numero, 
                            s.Lote.FechaExpiracion,
                            s.Lote.EstadoLote
                        } : null,
                        ValorTotal = (s.Cantidad ?? 0) * (s.Insumo.PrecioUnitario ?? 0)
                    })
                    .FirstOrDefaultAsync();

                if (stock == null)
                {
                    return NotFound("Stock no encontrado");
                }

                return Ok(stock);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener stock {Id}", id);
                return StatusCode(500, "Error interno del servidor");
            }
        }
    }
} 