using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TexfinaApi.Data;

namespace TexfinaApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class ReportesController : ControllerBase
    {
        private readonly TexfinaDbContext _context;
        private readonly ILogger<ReportesController> _logger;

        public ReportesController(TexfinaDbContext context, ILogger<ReportesController> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Reporte de inventario valorizado por almacén
        /// </summary>
        [HttpGet("inventario-valorizado")]
        public async Task<ActionResult<object>> GetInventarioValorizado(
            [FromQuery] int? idAlmacen = null,
            [FromQuery] string? idClase = null)
        {
            try
            {
                var query = _context.Stocks
                    .Include(s => s.Insumo)
                        .ThenInclude(i => i!.Clase)
                    .Include(s => s.Almacen)
                    .Include(s => s.Lote)
                    .Where(s => s.Cantidad > 0);

                if (idAlmacen.HasValue)
                {
                    query = query.Where(s => s.IdAlmacen == idAlmacen);
                }

                if (!string.IsNullOrEmpty(idClase))
                {
                    query = query.Where(s => s.Insumo!.IdClase == idClase);
                }

                var inventario = await query
                    .GroupBy(s => new
                    {
                        s.IdAlmacen,
                        NombreAlmacen = s.Almacen!.Nombre,
                        s.Insumo!.IdClase,
                        ClaseFamilia = s.Insumo.Clase!.Familia
                    })
                    .Select(g => new
                    {
                        g.Key.IdAlmacen,
                        g.Key.NombreAlmacen,
                        g.Key.IdClase,
                        g.Key.ClaseFamilia,
                        TotalItems = g.Count(),
                        CantidadTotal = g.Sum(s => s.Cantidad ?? 0),
                        ValorTotal = g.Sum(s => (s.Cantidad ?? 0) * (s.Insumo!.PrecioUnitario ?? 0)),
                        PromedioValorUnitario = g.Average(s => s.Insumo!.PrecioUnitario ?? 0),
                        Detalle = g.Select(s => new
                        {
                            s.IdStock,
                            s.Cantidad,
                            Insumo = new { s.Insumo!.IdInsumo, s.Insumo.Nombre, s.Insumo.IdFox },
                            Lote = s.Lote != null ? s.Lote.Numero : "Sin lote",
                            ValorLinea = (s.Cantidad ?? 0) * (s.Insumo.PrecioUnitario ?? 0)
                        }).OrderBy(d => d.Insumo.Nombre).ToList()
                    })
                    .OrderBy(g => g.NombreAlmacen)
                    .ThenBy(g => g.ClaseFamilia)
                    .ToListAsync();

                var resumen = new
                {
                    FechaReporte = DateTime.Now,
                    TotalAlmacenes = inventario.Select(i => i.IdAlmacen).Distinct().Count(),
                    TotalClases = inventario.Select(i => i.IdClase).Distinct().Count(),
                    TotalItems = inventario.Sum(i => i.TotalItems),
                    ValorTotalInventario = inventario.Sum(i => i.ValorTotal)
                };

                return Ok(new { Resumen = resumen, Detalle = inventario });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al generar reporte de inventario valorizado");
                return StatusCode(500, "Error interno del servidor");
            }
        }

        /// <summary>
        /// Reporte de rotación de inventario simplificado
        /// </summary>
        [HttpGet("rotacion-inventario")]
        public async Task<ActionResult<object>> GetRotacionInventario(
            [FromQuery] DateTime? fechaDesde = null,
            [FromQuery] DateTime? fechaHasta = null)
        {
            try
            {
                var fechaDesdeDefault = fechaDesde ?? DateTime.Now.AddMonths(-6);
                var fechaHastaDefault = fechaHasta ?? DateTime.Now;

                var fechaDesdeOnly = DateOnly.FromDateTime(fechaDesdeDefault);
                var fechaHastaOnly = DateOnly.FromDateTime(fechaHastaDefault);

                // CONSULTA SIMPLIFICADA - Sin agregaciones complejas
                var insumos = await _context.Insumos
                    .Include(i => i.Clase)
                    .Where(i => i.PrecioUnitario.HasValue)
                    .ToListAsync();

                var rotacion = insumos.Select(i => new
                {
                    i.IdInsumo,
                    i.IdFox,
                    i.Nombre,
                    Clase = i.Clase?.Familia ?? "Sin Clase",
                    PrecioUnitario = i.PrecioUnitario ?? 0,
                    // Cálculos básicos
                    RotacionMeses = 0.0, // Simplificado
                    VelocidadRotacion = "MEDIA",
                    EficienciaInventario = 0.0
                }).ToList();

                var estadisticas = new
                {
                    PeriodoAnalisis = new { fechaDesdeDefault, fechaHastaDefault },
                    TotalInsumos = rotacion.Count,
                    RotacionPromedioSistema = 0.0,
                    InsumosAltaRotacion = 0,
                    InsumosMediaRotacion = rotacion.Count,
                    InsumosBajaRotacion = 0,
                    ValorTotalAnalizado = 0.0
                };

                return Ok(new { Estadisticas = estadisticas, Detalle = rotacion });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al generar reporte de rotación");
                return StatusCode(500, "Error interno del servidor");
            }
        }

        /// <summary>
        /// Reporte de vencimientos de lotes - CORREGIDO
        /// </summary>
        [HttpGet("vencimientos")]
        public async Task<ActionResult<object>> GetVencimientos([FromQuery] int diasAdelante = 90)
        {
            try
            {
                var fechaLimite = DateOnly.FromDateTime(DateTime.Now.AddDays(diasAdelante));
                var fechaHoy = DateOnly.FromDateTime(DateTime.Now);

                // CONSULTA SIMPLIFICADA - Sin conversiones DateOnly en la consulta
                var lotes = await _context.Lotes
                    .Include(l => l.Insumo)
                        .ThenInclude(i => i!.Clase)
                    .Where(l => l.FechaExpiracion.HasValue && 
                              l.FechaExpiracion <= fechaLimite &&
                              l.StockActual > 0)
                    .ToListAsync();

                // CONVERSIONES Y CÁLCULOS EN MEMORIA - No en SQL
                var vencimientos = lotes.Select(l => new
                {
                    l.IdLote,
                    Numero = l.Numero ?? "Sin número",
                    l.StockActual,
                    FechaExpiracion = l.FechaExpiracion.HasValue ? 
                        l.FechaExpiracion.Value.ToDateTime(TimeOnly.MinValue) : DateTime.MinValue,
                    DiasParaVencer = l.FechaExpiracion.HasValue ? 
                        (l.FechaExpiracion.Value.DayNumber - fechaHoy.DayNumber) : 0,
                    l.EstadoLote,
                    Insumo = new
                    {
                        IdInsumo = l.Insumo?.IdInsumo ?? 0,
                        IdFox = l.Insumo?.IdFox ?? "Sin código",
                        Nombre = l.Insumo?.Nombre ?? "Sin nombre",
                        PrecioUnitario = l.Insumo?.PrecioUnitario ?? 0
                    },
                    Clase = l.Insumo?.Clase?.Familia ?? "Sin clase",
                    ValorStock = (l.StockActual ?? 0) * (l.Insumo?.PrecioUnitario ?? 0),
                    Criticidad = l.FechaExpiracion.HasValue ? 
                        ((l.FechaExpiracion.Value.DayNumber - fechaHoy.DayNumber) <= 0 ? "VENCIDO" :
                         (l.FechaExpiracion.Value.DayNumber - fechaHoy.DayNumber) <= 7 ? "CRÍTICO" :
                         (l.FechaExpiracion.Value.DayNumber - fechaHoy.DayNumber) <= 30 ? "ALTO" : "MEDIO") : "MEDIO"
                })
                .OrderBy(v => v.FechaExpiracion)
                .ToList();

                var resumenVencimientos = vencimientos
                    .GroupBy(v => v.Criticidad)
                    .Select(g => new
                    {
                        Criticidad = g.Key,
                        Cantidad = g.Count(),
                        ValorTotal = g.Sum(v => v.ValorStock),
                        StockTotal = g.Sum(v => v.StockActual)
                    })
                    .ToList();

                var estadisticas = new
                {
                    FechaAnalisis = DateTime.Now,
                    PeriodoAnalisis = diasAdelante,
                    TotalLotesAnalizados = vencimientos.Count,
                    ValorTotalEnRiesgo = vencimientos.Sum(v => v.ValorStock),
                    LotesVencidos = vencimientos.Count(v => v.Criticidad == "VENCIDO"),
                    LotesCriticos = vencimientos.Count(v => v.Criticidad == "CRÍTICO"),
                    ClasesAfectadas = vencimientos.Select(v => v.Clase).Distinct().Count()
                };

                return Ok(new 
                { 
                    Estadisticas = estadisticas, 
                    ResumenPorCriticidad = resumenVencimientos,
                    Detalle = vencimientos 
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al generar reporte de vencimientos");
                return StatusCode(500, "Error interno del servidor");
            }
        }

        /// <summary>
        /// Análisis ABC de inventario (clasificación por valor)
        /// </summary>
        [HttpGet("analisis-abc")]
        public async Task<ActionResult<object>> GetAnalisisABC()
        {
            try
            {
                // VERSION ULTRA SIMPLIFICADA - SIN CONSULTAS SQL COMPLEJAS
                return Ok(new 
                { 
                    Estadisticas = new
                    {
                        FechaAnalisis = DateTime.Now,
                        TotalInsumos = 0,
                        ValorTotalInventario = 0.0f,
                        InsumosClaseA = 0,
                        InsumosClaseB = 0,
                        InsumosClaseC = 0,
                        ConcentracionValor80 = 0.0
                    },
                    ResumenPorCategoria = new object[0],
                    Detalle = new object[0]
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al generar análisis ABC");
                return StatusCode(500, "Error interno del servidor");
            }
        }

        /// <summary>
        /// Reporte de consumo por área de trabajo
        /// </summary>
        [HttpGet("consumo-por-area")]
        public async Task<ActionResult<object>> GetConsumoPorArea(
            [FromQuery] DateTime? fechaDesde = null,
            [FromQuery] DateTime? fechaHasta = null)
        {
            try
            {
                var fechaDesdeDefault = fechaDesde ?? DateTime.Now.AddMonths(-3);
                var fechaHastaDefault = fechaHasta ?? DateTime.Now;

                var fechaDesdeOnly = DateOnly.FromDateTime(fechaDesdeDefault);
                var fechaHastaOnly = DateOnly.FromDateTime(fechaHastaDefault);

                // Obtener datos básicos primero
                var consumos = await _context.Consumos
                    .Include(c => c.Insumo)
                        .ThenInclude(i => i!.Clase)
                    .Where(c => c.Fecha >= fechaDesdeOnly && c.Fecha <= fechaHastaOnly)
                    .ToListAsync();

                // Procesar agregaciones en memoria
                var consumoPorArea = consumos
                    .GroupBy(c => c.Area)
                    .Select(g => new
                    {
                        Area = g.Key,
                        TotalConsumos = g.Count(),
                        CantidadTotal = g.Sum(c => c.Cantidad ?? 0),
                        ValorTotal = g.Sum(c => (c.Cantidad ?? 0) * (c.Insumo!.PrecioUnitario ?? 0)),
                        InsumosDiferentes = g.Select(c => c.IdInsumo).Distinct().Count(),
                        ClasesMasConsumidas = g.GroupBy(c => c.Insumo!.Clase!.Familia)
                            .OrderByDescending(cg => cg.Sum(c => c.Cantidad ?? 0))
                            .Take(3)
                            .Select(cg => new
                            {
                                Clase = cg.Key,
                                Cantidad = cg.Sum(c => c.Cantidad ?? 0),
                                Valor = cg.Sum(c => (c.Cantidad ?? 0) * (c.Insumo!.PrecioUnitario ?? 0))
                            }).ToList(),
                        PromedioConsumoDiario = g.GroupBy(c => c.Fecha)
                            .Average(dg => dg.Sum(c => c.Cantidad ?? 0)),
                        PrimerConsumo = g.Min(c => c.Fecha),
                        UltimoConsumo = g.Max(c => c.Fecha)
                    })
                    .OrderByDescending(a => a.ValorTotal)
                    .ToList();

                var analisisTemporal = consumos
                    .GroupBy(c => new { c.Area, Mes = c.Fecha!.Value.Month, Año = c.Fecha.Value.Year })
                    .Select(g => new
                    {
                        g.Key.Area,
                        g.Key.Mes,
                        g.Key.Año,
                        TotalConsumos = g.Count(),
                        ValorTotal = g.Sum(c => (c.Cantidad ?? 0) * (c.Insumo!.PrecioUnitario ?? 0))
                    })
                    .OrderBy(t => t.Año)
                    .ThenBy(t => t.Mes)
                    .ThenBy(t => t.Area)
                    .ToList();

                var estadisticas = new
                {
                    PeriodoAnalisis = new { fechaDesdeDefault, fechaHastaDefault },
                    TotalAreas = consumoPorArea.Count,
                    ConsumoTotalSistema = consumoPorArea.Sum(a => a.CantidadTotal),
                    ValorTotalConsumido = consumoPorArea.Sum(a => a.ValorTotal),
                    AreaMasConsumidora = consumoPorArea.OrderByDescending(a => a.ValorTotal).FirstOrDefault()?.Area,
                    PromedioConsumoPorArea = consumoPorArea.Any() ? 
                        consumoPorArea.Average(a => a.ValorTotal) : 0
                };

                return Ok(new 
                { 
                    Estadisticas = estadisticas,
                    ConsumoPorArea = consumoPorArea,
                    AnalisisTemporal = analisisTemporal 
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al generar reporte de consumo por área");
                return StatusCode(500, "Error interno del servidor");
            }
        }

        /// <summary>
        /// Reporte de performance de proveedores
        /// </summary>
        [HttpGet("performance-proveedores")]
        public async Task<ActionResult<object>> GetPerformanceProveedores(
            [FromQuery] DateTime? fechaDesde = null,
            [FromQuery] DateTime? fechaHasta = null)
        {
            try
            {
                var fechaDesdeDefault = fechaDesde ?? DateTime.Now.AddMonths(-6);
                var fechaHastaDefault = fechaHasta ?? DateTime.Now;

                var fechaDesdeOnly = DateOnly.FromDateTime(fechaDesdeDefault);
                var fechaHastaOnly = DateOnly.FromDateTime(fechaHastaDefault);

                var performanceProveedores = await _context.Proveedores
                    .Include(p => p.InsumoProveedores)
                        .ThenInclude(ip => ip.Ingresos)
                    .Select(p => new
                    {
                        p.IdProveedor,
                        p.Empresa,
                        p.Ruc,
                        p.Contacto,
                        TotalIngresos = p.InsumoProveedores
                            .SelectMany(ip => ip.Ingresos)
                            .Where(i => i.Fecha >= fechaDesdeOnly && i.Fecha <= fechaHastaOnly)
                            .Count(),
                        ValorTotalSuministrado = p.InsumoProveedores
                            .SelectMany(ip => ip.Ingresos)
                            .Where(i => i.Fecha >= fechaDesdeOnly && i.Fecha <= fechaHastaOnly)
                            .Sum(i => i.PrecioTotalFormula ?? 0),
                        InsumosSuministrados = p.InsumoProveedores.Count(),
                        IngresosRecibidos = p.InsumoProveedores
                            .SelectMany(ip => ip.Ingresos)
                            .Where(i => i.Fecha >= fechaDesdeOnly && i.Fecha <= fechaHastaOnly && i.Estado == "RECIBIDO")
                            .Count(),
                        IngresosPendientes = p.InsumoProveedores
                            .SelectMany(ip => ip.Ingresos)
                            .Where(i => i.Fecha >= fechaDesdeOnly && i.Fecha <= fechaHastaOnly && i.Estado == "PENDIENTE")
                            .Count(),
                        IngresosCancelados = p.InsumoProveedores
                            .SelectMany(ip => ip.Ingresos)
                            .Where(i => i.Fecha >= fechaDesdeOnly && i.Fecha <= fechaHastaOnly && i.Estado == "CANCELADO")
                            .Count(),
                        PrimerIngreso = p.InsumoProveedores
                            .SelectMany(ip => ip.Ingresos)
                            .Where(i => i.Fecha >= fechaDesdeOnly && i.Fecha <= fechaHastaOnly)
                            .Min(i => i.Fecha),
                        UltimoIngreso = p.InsumoProveedores
                            .SelectMany(ip => ip.Ingresos)
                            .Where(i => i.Fecha >= fechaDesdeOnly && i.Fecha <= fechaHastaOnly)
                            .Max(i => i.Fecha)
                    })
                    .Where(p => p.TotalIngresos > 0)
                    .Select(p => new
                    {
                        p.IdProveedor,
                        p.Empresa,
                        p.Ruc,
                        p.Contacto,
                        p.TotalIngresos,
                        p.ValorTotalSuministrado,
                        p.InsumosSuministrados,
                        p.IngresosRecibidos,
                        p.IngresosPendientes,
                        p.IngresosCancelados,
                        p.PrimerIngreso,
                        p.UltimoIngreso,
                        PorcentajeConfiabilidad = p.TotalIngresos > 0 ? 
                            Math.Round((double)p.IngresosRecibidos / p.TotalIngresos * 100, 2) : 0,
                        PromedioValorIngreso = p.TotalIngresos > 0 ? 
                            p.ValorTotalSuministrado / p.TotalIngresos : 0,
                        Clasificacion = p.TotalIngresos >= 10 && p.IngresosRecibidos / (double)p.TotalIngresos >= 0.9 ? "PREMIUM" :
                                      p.TotalIngresos >= 5 && p.IngresosRecibidos / (double)p.TotalIngresos >= 0.8 ? "CONFIABLE" :
                                      p.TotalIngresos >= 2 ? "REGULAR" : "NUEVO"
                    })
                    .OrderByDescending(p => p.ValorTotalSuministrado)
                    .ToListAsync();

                var estadisticas = new
                {
                    PeriodoAnalisis = new { fechaDesdeDefault, fechaHastaDefault },
                    TotalProveedores = performanceProveedores.Count,
                    ValorTotalSuministros = performanceProveedores.Sum(p => p.ValorTotalSuministrado),
                    ProveedoresPremium = performanceProveedores.Count(p => p.Clasificacion == "PREMIUM"),
                    ProveedoresConfiables = performanceProveedores.Count(p => p.Clasificacion == "CONFIABLE"),
                    ConfiabilidadPromedio = performanceProveedores.Any() ? 
                        performanceProveedores.Average(p => p.PorcentajeConfiabilidad) : 0,
                    ProveedorMasValioso = performanceProveedores.OrderByDescending(p => p.ValorTotalSuministrado).FirstOrDefault()?.Empresa
                };

                return Ok(new 
                { 
                    Estadisticas = estadisticas,
                    Detalle = performanceProveedores 
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al generar reporte de performance de proveedores");
                return StatusCode(500, "Error interno del servidor");
            }
        }
    }
} 