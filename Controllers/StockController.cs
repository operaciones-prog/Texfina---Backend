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
    public class StockController : ControllerBase
    {
        private readonly TexfinaDbContext _context;
        private readonly ILogger<StockController> _logger;

        public StockController(TexfinaDbContext context, ILogger<StockController> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Obtener resumen de stock por insumo
        /// </summary>
        [HttpGet("resumen")]
        public async Task<ActionResult<IEnumerable<object>>> GetResumenStock(
            [FromQuery] string? buscar = null,
            [FromQuery] string? idClase = null,
            [FromQuery] string? idAlmacen = null,
            [FromQuery] bool? stockBajo = null,
            [FromQuery] int pagina = 1,
            [FromQuery] int tamaño = 10)
        {
            try
            {
                var query = from i in _context.Insumos
                           join s in _context.Stocks on i.IdInsumo equals s.IdInsumo into stockGroup
                           from stock in stockGroup.DefaultIfEmpty()
                           group stock by new 
                           { 
                               i.IdInsumo, 
                               i.Nombre, 
                               i.IdFox, 
                               i.Presentacion,
                               i.IdClase,
                               ClaseFamilia = i.Clase!.Familia,
                               UnidadNombre = i.Unidad!.Nombre
                           } into g
                           select new
                           {
                               g.Key.IdInsumo,
                               g.Key.Nombre,
                               g.Key.IdFox,
                               g.Key.Presentacion,
                               g.Key.IdClase,
                               g.Key.ClaseFamilia,
                               g.Key.UnidadNombre,
                               StockTotal = g.Sum(s => s.Cantidad ?? 0),
                               LotesActivos = g.Count(s => s != null && s.Cantidad > 0)
                           };

                // Filtros
                if (!string.IsNullOrEmpty(buscar))
                {
                    query = query.Where(x => x.Nombre!.Contains(buscar) || x.IdFox!.Contains(buscar));
                }

                if (!string.IsNullOrEmpty(idClase))
                {
                    query = query.Where(x => x.IdClase == idClase);
                }

                if (stockBajo.HasValue && stockBajo.Value)
                {
                    query = query.Where(x => x.StockTotal <= 10); // Consideramos stock bajo <= 10
                }

                // Paginación
                var total = await query.CountAsync();
                var stocks = await query
                    .Skip((pagina - 1) * tamaño)
                    .Take(tamaño)
                    .ToListAsync();

                return Ok(new
                {
                    Data = stocks,
                    Total = total,
                    Pagina = pagina,
                    TotalPaginas = (int)Math.Ceiling(total / (double)tamaño)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener resumen de stock");
                return StatusCode(500, "Error interno del servidor");
            }
        }

        /// <summary>
        /// Obtener stock detallado de un insumo específico
        /// </summary>
        [HttpGet("insumo/{idInsumo}")]
        public async Task<ActionResult<object>> GetStockInsumo(int idInsumo)
        {
            try
            {
                var stocks = await _context.Stocks
                    .Include(s => s.Insumo)
                        .ThenInclude(i => i!.Clase)
                    .Include(s => s.Unidad)
                    .Include(s => s.Lote)
                    .Include(s => s.Almacen)
                    .Where(s => s.IdInsumo == idInsumo)
                    .Select(s => new
                    {
                        s.IdStock,
                        s.Presentacion,
                        s.Cantidad,
                        s.FechaEntrada,
                        s.FechaSalida,
                        Insumo = new 
                        {
                            s.Insumo!.IdInsumo,
                            s.Insumo.Nombre,
                            s.Insumo.IdFox,
                            Clase = s.Insumo.Clase!.Familia
                        },
                        Unidad = new { s.Unidad!.IdUnidad, s.Unidad.Nombre },
                        Lote = s.Lote != null ? new 
                        {
                            s.Lote.IdLote,
                            Numero = s.Lote.Numero,
                            FechaExpiracion = s.Lote.FechaExpiracion.HasValue ? s.Lote.FechaExpiracion.Value.ToDateTime(TimeOnly.MinValue) : (DateTime?)null,
                            s.Lote.EstadoLote
                        } : null,
                        Almacen = s.Almacen != null ? new 
                        {
                            s.Almacen.IdAlmacen,
                            s.Almacen.Nombre,
                            s.Almacen.Ubicacion
                        } : null
                    })
                    .ToListAsync();

                if (!stocks.Any())
                {
                    return NotFound("No se encontró stock para el insumo especificado");
                }

                var resumen = new
                {
                    IdInsumo = idInsumo,
                    StockTotal = stocks.Sum(s => s.Cantidad ?? 0),
                    LotesActivos = stocks.Count(s => s.Cantidad > 0),
                    Detalles = stocks
                };

                return Ok(resumen);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener stock del insumo {IdInsumo}", idInsumo);
                return StatusCode(500, "Error interno del servidor");
            }
        }

        /// <summary>
        /// Obtener movimientos de stock (entradas y salidas)
        /// </summary>
        [HttpGet("movimientos")]
        public async Task<ActionResult<object>> GetMovimientosStock(
            [FromQuery] int? idInsumo = null,
            [FromQuery] DateTime? fechaDesde = null,
            [FromQuery] DateTime? fechaHasta = null,
            [FromQuery] string? tipoMovimiento = null, // INGRESO, CONSUMO
            [FromQuery] int pagina = 1,
            [FromQuery] int tamaño = 10)
        {
            try
            {
                // Convertir fechas a DateOnly para comparaciones
                DateOnly? fechaDesdeOnly = fechaDesde.HasValue ? DateOnly.FromDateTime(fechaDesde.Value) : null;
                DateOnly? fechaHastaOnly = fechaHasta.HasValue ? DateOnly.FromDateTime(fechaHasta.Value) : null;

                // Obtener ingresos
                var ingresos = _context.Ingresos
                    .Include(i => i.Insumo)
                    .Include(i => i.Lote)
                    .Include(i => i.Unidad)
                    .Where(i => idInsumo == null || i.IdInsumo == idInsumo)
                    .Where(i => fechaDesdeOnly == null || i.Fecha >= fechaDesdeOnly)
                    .Where(i => fechaHastaOnly == null || i.Fecha <= fechaHastaOnly)
                    .Select(i => new
                    {
                        Id = i.IdIngreso,
                        TipoMovimiento = "INGRESO",
                        Fecha = i.Fecha.HasValue ? i.Fecha.Value.ToDateTime(TimeOnly.MinValue) : (DateTime?)null,
                        Cantidad = i.Cantidad,
                        Presentacion = i.Presentacion,
                        Insumo = new { i.Insumo!.IdInsumo, i.Insumo.Nombre, i.Insumo.IdFox },
                        Lote = i.Lote != null ? i.Lote.Numero : null,
                        Unidad = i.Unidad != null ? i.Unidad.Nombre : null,
                        Referencia = i.NumeroRemision,
                        Estado = i.Estado,
                        PrecioTotal = i.PrecioTotalFormula
                    });

                // Obtener consumos
                var consumos = _context.Consumos
                    .Include(c => c.Insumo)
                    .Include(c => c.Lote)
                    .Where(c => idInsumo == null || c.IdInsumo == idInsumo)
                    .Where(c => fechaDesdeOnly == null || c.Fecha >= fechaDesdeOnly)
                    .Where(c => fechaHastaOnly == null || c.Fecha <= fechaHastaOnly)
                    .Select(c => new
                    {
                        Id = c.IdConsumo,
                        TipoMovimiento = "CONSUMO",
                        Fecha = c.Fecha.HasValue ? c.Fecha.Value.ToDateTime(TimeOnly.MinValue) : (DateTime?)null,
                        Cantidad = c.Cantidad * -1, // Negativo para consumos
                        Presentacion = (string?)null,
                        Insumo = new { c.Insumo!.IdInsumo, c.Insumo.Nombre, c.Insumo.IdFox },
                        Lote = c.Lote != null ? c.Lote.Numero : null,
                        Unidad = (string?)null,
                        Referencia = c.Area,
                        Estado = c.Estado,
                        PrecioTotal = (float?)null
                    });

                // Combinar y filtrar por tipo si se especifica
                IQueryable<object> movimientos;
                if (tipoMovimiento?.ToUpper() == "INGRESO")
                {
                    movimientos = ingresos.Cast<object>();
                }
                else if (tipoMovimiento?.ToUpper() == "CONSUMO")
                {
                    movimientos = consumos.Cast<object>();
                }
                else
                {
                    // Unir ambos tipos (esto requiere materializarlos primero)
                    var ingresosLista = await ingresos.ToListAsync();
                    var consumosLista = await consumos.ToListAsync();
                    var todosMovimientos = ingresosLista.Cast<object>().Concat(consumosLista.Cast<object>())
                        .OrderByDescending(m => ((dynamic)m).Fecha);

                    var total = todosMovimientos.Count();
                    var movimientosPaginados = todosMovimientos
                        .Skip((pagina - 1) * tamaño)
                        .Take(tamaño)
                        .ToList();

                    return Ok(new
                    {
                        Data = movimientosPaginados,
                        Total = total,
                        Pagina = pagina,
                        TotalPaginas = (int)Math.Ceiling(total / (double)tamaño)
                    });
                }

                // Para consultas de un solo tipo
                var totalMovimientos = await movimientos.CountAsync();
                var movimientosPag = await movimientos
                    .Skip((pagina - 1) * tamaño)
                    .Take(tamaño)
                    .ToListAsync();

                return Ok(new
                {
                    Data = movimientosPag,
                    Total = totalMovimientos,
                    Pagina = pagina,
                    TotalPaginas = (int)Math.Ceiling(totalMovimientos / (double)tamaño)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener movimientos de stock");
                return StatusCode(500, "Error interno del servidor");
            }
        }

        /// <summary>
        /// Obtener alertas de stock (bajo stock, próximos a vencer, etc.)
        /// </summary>
        [HttpGet("alertas")]
        public async Task<ActionResult<object>> GetAlertasStock()
        {
            try
            {
                var fechaActual = DateTime.Now.Date;
                var fechaProximoVencimiento = fechaActual.AddDays(30); // Próximos 30 días

                // Stock bajo (menos de 10 unidades)
                var stockBajo = await (from i in _context.Insumos
                                     join s in _context.Stocks on i.IdInsumo equals s.IdInsumo into stockGroup
                                     from stock in stockGroup.DefaultIfEmpty()
                                     group stock by new { i.IdInsumo, i.Nombre, i.IdFox } into g
                                     where g.Sum(s => s.Cantidad ?? 0) <= 10 && g.Sum(s => s.Cantidad ?? 0) > 0
                                     select new
                                     {
                                         g.Key.IdInsumo,
                                         g.Key.Nombre,
                                         g.Key.IdFox,
                                         StockActual = g.Sum(s => s.Cantidad ?? 0),
                                         TipoAlerta = "STOCK_BAJO"
                                     }).ToListAsync();

                // Lotes próximos a vencer
                var proximosVencer = await _context.Lotes
                    .Include(l => l.Insumo)
                    .Where(l => l.FechaExpiracion.HasValue && 
                              l.FechaExpiracion <= DateOnly.FromDateTime(fechaProximoVencimiento) &&
                              l.FechaExpiracion >= DateOnly.FromDateTime(fechaActual) &&
                              l.EstadoLote == "ACTIVO")
                    .Select(l => new
                    {
                        IdLote = l.IdLote,
                        Numero = l.Numero,
                        FechaExpiracion = l.FechaExpiracion.HasValue ? l.FechaExpiracion.Value.ToDateTime(TimeOnly.MinValue) : (DateTime?)null,
                        l.StockActual,
                        Insumo = new { l.Insumo!.IdInsumo, l.Insumo.Nombre, l.Insumo.IdFox },
                        TipoAlerta = "PROXIMO_VENCER",
                        DiasRestantes = l.FechaExpiracion.HasValue ? (l.FechaExpiracion.Value.DayNumber - DateOnly.FromDateTime(fechaActual).DayNumber) : (int?)null
                    })
                    .OrderBy(l => l.FechaExpiracion)
                    .ToListAsync();

                // Sin stock
                var sinStock = await (from i in _context.Insumos
                                    join s in _context.Stocks on i.IdInsumo equals s.IdInsumo into stockGroup
                                    from stock in stockGroup.DefaultIfEmpty()
                                    group stock by new { i.IdInsumo, i.Nombre, i.IdFox } into g
                                    where g.Sum(s => s.Cantidad ?? 0) <= 0
                                    select new
                                    {
                                        g.Key.IdInsumo,
                                        g.Key.Nombre,
                                        g.Key.IdFox,
                                        StockActual = 0,
                                        TipoAlerta = "SIN_STOCK"
                                    }).ToListAsync();

                var alertas = new
                {
                    StockBajo = stockBajo,
                    ProximosVencer = proximosVencer,
                    SinStock = sinStock,
                    Resumen = new
                    {
                        TotalAlertas = stockBajo.Count + proximosVencer.Count + sinStock.Count,
                        StockBajoCount = stockBajo.Count,
                        ProximosVencerCount = proximosVencer.Count,
                        SinStockCount = sinStock.Count
                    }
                };

                return Ok(alertas);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener alertas de stock");
                return StatusCode(500, "Error interno del servidor");
            }
        }

        /// <summary>
        /// Obtener stock por almacén
        /// </summary>
        [HttpGet("almacen/{idAlmacen}")]
        public async Task<ActionResult<object>> GetStockPorAlmacen(int idAlmacen)
        {
            try
            {
                var almacen = await _context.Almacenes.FindAsync(idAlmacen);
                if (almacen == null)
                {
                    return NotFound("Almacén no encontrado");
                }

                var stockAlmacen = await _context.Stocks
                    .Include(s => s.Insumo)
                        .ThenInclude(i => i!.Clase)
                    .Include(s => s.Lote)
                    .Where(s => s.IdAlmacen == idAlmacen && s.Cantidad > 0)
                    .Select(s => new
                    {
                        s.IdStock,
                        s.Cantidad,
                        s.Presentacion,
                        s.FechaEntrada,
                        Insumo = new
                        {
                            s.Insumo!.IdInsumo,
                            s.Insumo.Nombre,
                            s.Insumo.IdFox,
                            Clase = s.Insumo.Clase!.Familia
                        },
                        Lote = s.Lote != null ? new
                        {
                            s.Lote.IdLote,
                            Numero = s.Lote.Numero,
                            FechaExpiracion = s.Lote.FechaExpiracion.HasValue ? s.Lote.FechaExpiracion.Value.ToDateTime(TimeOnly.MinValue) : (DateTime?)null
                        } : null
                    })
                    .ToListAsync();

                var resumen = new
                {
                    Almacen = new { almacen.IdAlmacen, almacen.Nombre, almacen.Ubicacion },
                    TotalItems = stockAlmacen.Count,
                    StockTotal = stockAlmacen.Sum(s => s.Cantidad ?? 0),
                    Stock = stockAlmacen
                };

                return Ok(resumen);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener stock del almacén {IdAlmacen}", idAlmacen);
                return StatusCode(500, "Error interno del servidor");
            }
        }
    }
} 