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
    public class LotesController : ControllerBase
    {
        private readonly TexfinaDbContext _context;
        private readonly ILogger<LotesController> _logger;

        public LotesController(TexfinaDbContext context, ILogger<LotesController> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Obtener todos los lotes
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<IEnumerable<object>>> GetLotes([FromQuery] string? buscar = null)
        {
            try
            {
                var query = _context.Lotes.AsQueryable();

                if (!string.IsNullOrEmpty(buscar))
                {
                    query = query.Where(l => l.Numero!.Contains(buscar));
                }

                var lotes = await query
                    .Select(l => new
                    {
                        l.IdLote,
                        l.Numero,
                        l.Ubicacion,
                        l.StockInicial,
                        l.StockActual,
                        l.PrecioTotal,
                        l.EstadoLote,
                        l.IdInsumo
                    })
                    .OrderBy(l => l.IdLote)
                    .ToListAsync();

                return Ok(lotes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener lotes");
                return StatusCode(500, "Error interno del servidor");
            }
        }

        /// <summary>
        /// Obtener un lote específico por ID
        /// </summary>
        [HttpGet("{id}")]
        public async Task<ActionResult<object>> GetLote(int id)
        {
            try
            {
                var lote = await _context.Lotes
                    .Include(l => l.Insumo)
                        .ThenInclude(i => i!.Clase)
                    .Where(l => l.IdLote == id)
                    .Select(l => new
                    {
                        l.IdLote,
                        l.Numero,
                        l.Ubicacion,
                        l.StockInicial,
                        l.StockActual,
                        l.FechaExpiracion,
                        l.PrecioTotal,
                        l.EstadoLote,
                        Insumo = new
                        {
                            l.Insumo!.IdInsumo,
                            l.Insumo.Nombre,
                            l.Insumo.IdFox,
                            Clase = l.Insumo.Clase!.Familia
                        }
                    })
                    .FirstOrDefaultAsync();

                if (lote == null)
                {
                    return NotFound("Lote no encontrado");
                }

                return Ok(lote);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener lote {Id}", id);
                return StatusCode(500, "Error interno del servidor");
            }
        }

        /// <summary>
        /// Obtener lotes activos
        /// </summary>
        [HttpGet("activos")]
        public async Task<ActionResult<IEnumerable<object>>> GetLotesActivos()
        {
            try
            {
                var lotesActivos = await _context.Lotes
                    .Include(l => l.Insumo)
                        .ThenInclude(i => i!.Clase)
                    .Where(l => l.EstadoLote == "ACTIVO" && l.StockActual > 0)
                    .Select(l => new
                    {
                        l.IdLote,
                        l.Numero,
                        l.Ubicacion,
                        l.StockInicial,
                        l.StockActual,
                        l.FechaExpiracion,
                        l.PrecioTotal,
                        l.EstadoLote,
                        DiasParaVencer = l.FechaExpiracion.HasValue ? 
                            (DateTime.Today - l.FechaExpiracion.Value.ToDateTime(TimeOnly.MinValue)).Days * -1 : (int?)null,
                        Insumo = new
                        {
                            l.Insumo!.IdInsumo,
                            l.Insumo.Nombre,
                            l.Insumo.IdFox,
                            Clase = l.Insumo.Clase!.Familia
                        }
                    })
                    .OrderBy(l => l.FechaExpiracion)
                    .ToListAsync();

                return Ok(new
                {
                    TotalLotesActivos = lotesActivos.Count,
                    Lotes = lotesActivos,
                    FechaConsulta = DateTime.Now
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener lotes activos");
                return StatusCode(500, "Error interno del servidor");
            }
        }

        /// <summary>
        /// Obtener lotes próximos a vencer (30 días por defecto)
        /// </summary>
        [HttpGet("por-vencer")]
        public async Task<ActionResult<IEnumerable<object>>> GetLotesPorVencer([FromQuery] int diasAlerta = 30)
        {
            try
            {
                var fechaLimite = DateOnly.FromDateTime(DateTime.Today.AddDays(diasAlerta));
                var fechaHoy = DateOnly.FromDateTime(DateTime.Today);
                
                var lotesPorVencer = await _context.Lotes
                    .Include(l => l.Insumo)
                        .ThenInclude(i => i!.Clase)
                    .Where(l => l.EstadoLote == "ACTIVO" && 
                              l.FechaExpiracion.HasValue && 
                              l.FechaExpiracion.Value <= fechaLimite &&
                              l.FechaExpiracion.Value > fechaHoy &&
                              l.StockActual > 0)
                    .ToListAsync();

                var resultado = lotesPorVencer.Select(l => new
                {
                    l.IdLote,
                    l.Numero,
                    l.Ubicacion,
                    l.StockInicial,
                    l.StockActual,
                    l.FechaExpiracion,
                    l.PrecioTotal,
                    DiasParaVencer = (l.FechaExpiracion!.Value.ToDateTime(TimeOnly.MinValue) - DateTime.Today).Days,
                    Criticidad = (l.FechaExpiracion!.Value.ToDateTime(TimeOnly.MinValue) - DateTime.Today).Days <= 7 ? "CRITICO" :
                               (l.FechaExpiracion!.Value.ToDateTime(TimeOnly.MinValue) - DateTime.Today).Days <= 15 ? "ALTO" : "MEDIO",
                    Insumo = new
                    {
                        l.Insumo!.IdInsumo,
                        l.Insumo.Nombre,
                        l.Insumo.IdFox,
                        Clase = l.Insumo.Clase!.Familia
                    }
                }).OrderBy(l => l.FechaExpiracion).ToList();

                return Ok(new
                {
                    DiasAlertaConsultados = diasAlerta,
                    TotalLotesPorVencer = resultado.Count,
                    LotesCriticos = resultado.Count(l => l.Criticidad == "CRITICO"),
                    LotesAltoRiesgo = resultado.Count(l => l.Criticidad == "ALTO"),
                    LotesRiesgoMedio = resultado.Count(l => l.Criticidad == "MEDIO"),
                    Lotes = resultado,
                    FechaConsulta = DateTime.Now
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener lotes por vencer");
                return StatusCode(500, "Error interno del servidor");
            }
        }

        /// <summary>
        /// Obtener lotes vencidos
        /// </summary>
        [HttpGet("vencidos")]
        public async Task<ActionResult<IEnumerable<object>>> GetLotesVencidos()
        {
            try
            {
                var fechaHoy = DateOnly.FromDateTime(DateTime.Today);
                
                var lotesVencidos = await _context.Lotes
                    .Include(l => l.Insumo)
                        .ThenInclude(i => i!.Clase)
                    .Where(l => l.FechaExpiracion.HasValue && 
                              l.FechaExpiracion.Value < fechaHoy &&
                              l.StockActual > 0)
                    .ToListAsync();

                var resultado = lotesVencidos.Select(l => new
                {
                    l.IdLote,
                    l.Numero,
                    l.Ubicacion,
                    l.StockInicial,
                    l.StockActual,
                    l.FechaExpiracion,
                    l.PrecioTotal,
                    l.EstadoLote,
                    DiasVencido = (DateTime.Today - l.FechaExpiracion!.Value.ToDateTime(TimeOnly.MinValue)).Days,
                    Insumo = new
                    {
                        l.Insumo!.IdInsumo,
                        l.Insumo.Nombre,
                        l.Insumo.IdFox,
                        Clase = l.Insumo.Clase!.Familia
                    }
                }).OrderByDescending(l => l.DiasVencido).ToList();

                return Ok(new
                {
                    TotalLotesVencidos = resultado.Count,
                    StockVencidoTotal = resultado.Sum(l => l.StockActual ?? 0),
                    ValorStockVencido = resultado.Sum(l => l.PrecioTotal ?? 0),
                    Lotes = resultado,
                    FechaConsulta = DateTime.Now
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener lotes vencidos");
                return StatusCode(500, "Error interno del servidor");
            }
        }

        /// <summary>
        /// Obtener estadísticas generales de lotes
        /// </summary>
        [HttpGet("estadisticas")]
        public async Task<ActionResult<object>> GetEstadisticasLotes()
        {
            try
            {
                var fechaHoy = DateOnly.FromDateTime(DateTime.Today);
                var fecha30Dias = DateOnly.FromDateTime(DateTime.Today.AddDays(30));

                var totalLotes = await _context.Lotes.CountAsync();
                var lotesActivos = await _context.Lotes.CountAsync(l => l.EstadoLote == "ACTIVO");
                var lotesConStock = await _context.Lotes.CountAsync(l => l.StockActual > 0);
                
                var lotesPorVencer30 = await _context.Lotes
                    .CountAsync(l => l.FechaExpiracion.HasValue && 
                               l.FechaExpiracion.Value <= fecha30Dias &&
                               l.FechaExpiracion.Value > fechaHoy &&
                               l.StockActual > 0);
                
                var lotesVencidos = await _context.Lotes
                    .CountAsync(l => l.FechaExpiracion.HasValue && 
                               l.FechaExpiracion.Value < fechaHoy &&
                               l.StockActual > 0);

                var stockTotalActivo = await _context.Lotes
                    .Where(l => l.StockActual > 0)
                    .SumAsync(l => l.StockActual ?? 0);

                var valorTotalLotes = await _context.Lotes
                    .Where(l => l.StockActual > 0)
                    .SumAsync(l => l.PrecioTotal ?? 0);

                return Ok(new
                {
                    TotalLotes = totalLotes,
                    LotesActivos = lotesActivos,
                    LotesConStock = lotesConStock,
                    LotesSinStock = totalLotes - lotesConStock,
                    LotesPorVencer30Dias = lotesPorVencer30,
                    LotesVencidos = lotesVencidos,
                    StockTotalActivo = stockTotalActivo,
                    ValorTotalLotes = valorTotalLotes,
                    PromedioStockPorLote = lotesConStock > 0 ? stockTotalActivo / lotesConStock : 0,
                    FechaConsulta = DateTime.Now
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener estadísticas de lotes");
                return StatusCode(500, "Error interno del servidor");
            }
        }
    }
} 