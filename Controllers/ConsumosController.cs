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
    public class ConsumosController : ControllerBase
    {
        private readonly TexfinaDbContext _context;
        private readonly ILogger<ConsumosController> _logger;

        public ConsumosController(TexfinaDbContext context, ILogger<ConsumosController> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Obtener todos los consumos con filtros opcionales
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<IEnumerable<object>>> GetConsumos(
            [FromQuery] string? buscar = null,
            [FromQuery] int? idInsumo = null,
            [FromQuery] string? area = null,
            [FromQuery] DateTime? fechaDesde = null,
            [FromQuery] DateTime? fechaHasta = null,
            [FromQuery] string? estado = null,
            [FromQuery] int pagina = 1,
            [FromQuery] int tamaño = 10)
        {
            try
            {
                var query = _context.Consumos
                    .Include(c => c.Insumo)
                        .ThenInclude(i => i!.Clase)
                    .Include(c => c.Lote)
                    .AsQueryable();

                // Filtros
                if (!string.IsNullOrEmpty(buscar))
                {
                    query = query.Where(c => c.Insumo!.Nombre!.Contains(buscar) ||
                                           c.Area!.Contains(buscar));
                }

                if (idInsumo.HasValue)
                {
                    query = query.Where(c => c.IdInsumo == idInsumo);
                }

                if (!string.IsNullOrEmpty(area))
                {
                    query = query.Where(c => c.Area == area);
                }

                if (fechaDesde.HasValue)
                {
                    var fechaDesdeOnly = DateOnly.FromDateTime(fechaDesde.Value);
                    query = query.Where(c => c.Fecha >= fechaDesdeOnly);
                }

                if (fechaHasta.HasValue)
                {
                    var fechaHastaOnly = DateOnly.FromDateTime(fechaHasta.Value);
                    query = query.Where(c => c.Fecha <= fechaHastaOnly);
                }

                if (!string.IsNullOrEmpty(estado))
                {
                    query = query.Where(c => c.Estado == estado);
                }

                // Paginación
                var total = await query.CountAsync();
                
                // CONSULTA SIMPLIFICADA - Sin conversiones DateOnly en SQL
                var consumosRaw = await query
                    .OrderByDescending(c => c.Fecha)
                    .Skip((pagina - 1) * tamaño)
                    .Take(tamaño)
                    .ToListAsync();

                // CONVERSIONES EN MEMORIA - No en SQL
                var consumos = consumosRaw.Select(c => new
                {
                    IdConsumo = c.IdConsumo,
                    Fecha = c.Fecha.HasValue ? c.Fecha.Value.ToDateTime(TimeOnly.MinValue) : (DateTime?)null,
                    Cantidad = c.Cantidad,
                    Area = c.Area,
                    Estado = c.Estado,
                    Insumo = new
                    {
                        IdInsumo = c.Insumo?.IdInsumo ?? 0,
                        IdFox = c.Insumo?.IdFox ?? "Sin código",
                        Nombre = c.Insumo?.Nombre ?? "Sin nombre",
                        Clase = c.Insumo?.Clase?.Familia ?? "Sin clase",
                        PrecioUnitario = c.Insumo?.PrecioUnitario ?? 0
                    },
                    Lote = c.Lote != null ? new
                    {
                        IdLote = c.Lote.IdLote,
                        Numero = c.Lote.Numero ?? "Sin número",
                        EstadoLote = c.Lote.EstadoLote
                    } : null,
                    ValorConsumido = (c.Cantidad ?? 0) * (c.Insumo?.PrecioUnitario ?? 0)
                }).ToList();

                return Ok(new
                {
                    Data = consumos,
                    Total = total,
                    Pagina = pagina,
                    TotalPaginas = (int)Math.Ceiling(total / (double)tamaño)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener consumos");
                return StatusCode(500, "Error interno del servidor");
            }
        }

        /// <summary>
        /// Obtener un consumo específico por ID
        /// </summary>
        [HttpGet("{id}")]
        public async Task<ActionResult<object>> GetConsumo(int id)
        {
            try
            {
                // CONSULTA SIMPLIFICADA - Sin conversiones DateOnly
                var consumoRaw = await _context.Consumos
                    .Include(c => c.Insumo)
                        .ThenInclude(i => i!.Clase)
                    .Include(c => c.Lote)
                    .FirstOrDefaultAsync(c => c.IdConsumo == id);

                if (consumoRaw == null)
                {
                    return NotFound("Consumo no encontrado");
                }

                // CONVERSIONES EN MEMORIA
                var consumo = new
                {
                    IdConsumo = consumoRaw.IdConsumo,
                    Fecha = consumoRaw.Fecha.HasValue ? consumoRaw.Fecha.Value.ToDateTime(TimeOnly.MinValue) : (DateTime?)null,
                    Cantidad = consumoRaw.Cantidad,
                    Area = consumoRaw.Area,
                    Estado = consumoRaw.Estado,
                    Insumo = new
                    {
                        IdInsumo = consumoRaw.Insumo?.IdInsumo ?? 0,
                        IdFox = consumoRaw.Insumo?.IdFox ?? "Sin código",
                        Nombre = consumoRaw.Insumo?.Nombre ?? "Sin nombre",
                        Presentacion = consumoRaw.Insumo?.Presentacion ?? "Sin presentación",
                        PrecioUnitario = consumoRaw.Insumo?.PrecioUnitario ?? 0,
                        Clase = new { 
                            IdClase = consumoRaw.Insumo?.Clase?.IdClase ?? "Sin clase", 
                            Familia = consumoRaw.Insumo?.Clase?.Familia ?? "Sin familia", 
                            SubFamilia = consumoRaw.Insumo?.Clase?.SubFamilia ?? "Sin subfamilia" 
                        }
                    },
                    Lote = consumoRaw.Lote != null ? new
                    {
                        IdLote = consumoRaw.Lote.IdLote,
                        Numero = consumoRaw.Lote.Numero ?? "Sin número",
                        Ubicacion = consumoRaw.Lote.Ubicacion,
                        StockActual = consumoRaw.Lote.StockActual,
                        FechaExpiracion = consumoRaw.Lote.FechaExpiracion.HasValue ? 
                            consumoRaw.Lote.FechaExpiracion.Value.ToDateTime(TimeOnly.MinValue) : (DateTime?)null,
                        EstadoLote = consumoRaw.Lote.EstadoLote
                    } : null,
                    ValorConsumido = (consumoRaw.Cantidad ?? 0) * (consumoRaw.Insumo?.PrecioUnitario ?? 0)
                };

                return Ok(consumo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener consumo {Id}", id);
                return StatusCode(500, "Error interno del servidor");
            }
        }

        /// <summary>
        /// Registrar un nuevo consumo de insumo
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<Consumo>> CreateConsumo([FromBody] ConsumoCreateDto consumoDto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                using var transaction = await _context.Database.BeginTransactionAsync();

                // Verificar que el insumo existe
                var insumo = await _context.Insumos.FindAsync(consumoDto.IdInsumo);
                if (insumo == null)
                {
                    return BadRequest("El insumo especificado no existe");
                }

                // Verificar el lote si se especifica
                Lote? lote = null;
                if (consumoDto.IdLote.HasValue)
                {
                    lote = await _context.Lotes.FindAsync(consumoDto.IdLote);
                    if (lote == null)
                    {
                        return BadRequest("El lote especificado no existe");
                    }

                    // Verificar que el lote tiene stock suficiente
                    if (lote.StockActual < consumoDto.Cantidad)
                    {
                        return BadRequest($"Stock insuficiente en el lote. Disponible: {lote.StockActual}, Solicitado: {consumoDto.Cantidad}");
                    }
                }

                // Verificar stock disponible en almacenes si se especifica
                if (consumoDto.IdAlmacen.HasValue)
                {
                    var stockDisponible = await _context.Stocks
                        .Where(s => s.IdInsumo == consumoDto.IdInsumo && 
                                  s.IdAlmacen == consumoDto.IdAlmacen &&
                                  (consumoDto.IdLote == null || s.IdLote == consumoDto.IdLote))
                        .SumAsync(s => s.Cantidad ?? 0);

                    if (stockDisponible < consumoDto.Cantidad)
                    {
                        return BadRequest($"Stock insuficiente en el almacén. Disponible: {stockDisponible}, Solicitado: {consumoDto.Cantidad}");
                    }
                }

                // Crear el consumo
                var consumo = new Consumo
                {
                    IdInsumo = consumoDto.IdInsumo,
                    Area = consumoDto.Area,
                    Fecha = consumoDto.Fecha.HasValue ? DateOnly.FromDateTime(consumoDto.Fecha.Value) : DateOnly.FromDateTime(DateTime.Now),
                    Cantidad = consumoDto.Cantidad,
                    IdLote = consumoDto.IdLote,
                    Estado = "CONFIRMADO"
                };

                _context.Consumos.Add(consumo);

                // Actualizar stock del lote
                if (lote != null)
                {
                    lote.StockActual -= consumoDto.Cantidad ?? 0;
                    
                    // Cambiar estado del lote si se agotó
                    if (lote.StockActual <= 0)
                    {
                        lote.EstadoLote = "AGOTADO";
                    }
                }

                // Actualizar stock en almacén si se especifica
                if (consumoDto.IdAlmacen.HasValue)
                {
                    var stocksAActualizar = await _context.Stocks
                        .Where(s => s.IdInsumo == consumoDto.IdInsumo && 
                                  s.IdAlmacen == consumoDto.IdAlmacen &&
                                  (consumoDto.IdLote == null || s.IdLote == consumoDto.IdLote) &&
                                  s.Cantidad > 0)
                        .OrderBy(s => s.FechaEntrada) // FIFO
                        .ToListAsync();

                    float cantidadRestante = consumoDto.Cantidad ?? 0;

                    foreach (var stock in stocksAActualizar)
                    {
                        if (cantidadRestante <= 0) break;

                        float cantidadAReducir = Math.Min(stock.Cantidad ?? 0, cantidadRestante);
                        stock.Cantidad -= cantidadAReducir;
                        cantidadRestante -= cantidadAReducir;

                        // Marcar fecha de salida si el stock se agotó
                        if (stock.Cantidad <= 0)
                        {
                            stock.FechaSalida = DateTime.Now;
                        }
                    }
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return CreatedAtAction(nameof(GetConsumo), new { id = consumo.IdConsumo }, consumo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al crear consumo");
                return StatusCode(500, "Error interno del servidor");
            }
        }

        /// <summary>
        /// Actualizar estado de un consumo
        /// </summary>
        [HttpPatch("{id}/estado")]
        public async Task<IActionResult> CambiarEstado(int id, [FromBody] CambiarEstadoConsumoDto dto)
        {
            try
            {
                var consumo = await _context.Consumos.FindAsync(id);
                if (consumo == null)
                {
                    return NotFound("Consumo no encontrado");
                }

                var estadosValidos = new[] { "PENDIENTE", "CONFIRMADO", "CANCELADO" };
                if (!estadosValidos.Contains(dto.Estado))
                {
                    return BadRequest($"Estado inválido. Estados válidos: {string.Join(", ", estadosValidos)}");
                }

                consumo.Estado = dto.Estado;
                await _context.SaveChangesAsync();

                return Ok(new { mensaje = $"Estado del consumo cambiado a {dto.Estado}", consumo });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cambiar estado del consumo {Id}", id);
                return StatusCode(500, "Error interno del servidor");
            }
        }

        /// <summary>
        /// Obtener estadísticas de consumos
        /// </summary>
        [HttpGet("estadisticas")]
        public async Task<ActionResult<object>> GetEstadisticas(
            [FromQuery] DateTime? fechaDesde = null,
            [FromQuery] DateTime? fechaHasta = null)
        {
            try
            {
                var fechaDesdeDefault = fechaDesde ?? DateTime.Now.AddMonths(-1);
                var fechaHastaDefault = fechaHasta ?? DateTime.Now;

                var fechaDesdeOnly = DateOnly.FromDateTime(fechaDesdeDefault);
                var fechaHastaOnly = DateOnly.FromDateTime(fechaHastaDefault);

                var estadisticas = await _context.Consumos
                    .Include(c => c.Insumo)
                        .ThenInclude(i => i!.Clase)
                    .Where(c => c.Fecha >= fechaDesdeOnly && c.Fecha <= fechaHastaOnly)
                    .GroupBy(c => 1)
                    .Select(g => new
                    {
                        TotalConsumos = g.Count(),
                        CantidadTotal = g.Sum(c => c.Cantidad ?? 0),
                        ValorTotal = g.Sum(c => (c.Cantidad ?? 0) * (c.Insumo!.PrecioUnitario ?? 0)),
                        PromedioConsumo = g.Average(c => c.Cantidad ?? 0),
                        ConsumosPendientes = g.Count(c => c.Estado == "PENDIENTE"),
                        ConsumosConfirmados = g.Count(c => c.Estado == "CONFIRMADO"),
                        ConsumosCancelados = g.Count(c => c.Estado == "CANCELADO")
                    })
                    .FirstOrDefaultAsync();

                var porArea = await _context.Consumos
                    .Include(c => c.Insumo)
                    .Where(c => c.Fecha >= fechaDesdeOnly && c.Fecha <= fechaHastaOnly)
                    .GroupBy(c => c.Area)
                    .Select(g => new
                    {
                        Area = g.Key,
                        TotalConsumos = g.Count(),
                        CantidadTotal = g.Sum(c => c.Cantidad ?? 0),
                        ValorTotal = g.Sum(c => (c.Cantidad ?? 0) * (c.Insumo!.PrecioUnitario ?? 0))
                    })
                    .OrderByDescending(x => x.ValorTotal)
                    .Take(10)
                    .ToListAsync();

                var porClase = await _context.Consumos
                    .Include(c => c.Insumo)
                        .ThenInclude(i => i!.Clase)
                    .Where(c => c.Fecha >= fechaDesdeOnly && c.Fecha <= fechaHastaOnly)
                    .GroupBy(c => c.Insumo!.Clase!.Familia)
                    .Select(g => new
                    {
                        Clase = g.Key,
                        TotalConsumos = g.Count(),
                        CantidadTotal = g.Sum(c => c.Cantidad ?? 0),
                        ValorTotal = g.Sum(c => (c.Cantidad ?? 0) * (c.Insumo!.PrecioUnitario ?? 0))
                    })
                    .OrderByDescending(x => x.ValorTotal)
                    .Take(10)
                    .ToListAsync();

                var porMes = await _context.Consumos
                    .Include(c => c.Insumo)
                    .Where(c => c.Fecha >= fechaDesdeOnly && c.Fecha <= fechaHastaOnly)
                    .GroupBy(c => new { Año = c.Fecha!.Value.Year, Mes = c.Fecha.Value.Month })
                    .Select(g => new
                    {
                        Año = g.Key.Año,
                        Mes = g.Key.Mes,
                        TotalConsumos = g.Count(),
                        ValorTotal = g.Sum(c => (c.Cantidad ?? 0) * (c.Insumo!.PrecioUnitario ?? 0))
                    })
                    .OrderBy(x => x.Año)
                    .ThenBy(x => x.Mes)
                    .ToListAsync();

                // Top insumos más consumidos
                var topInsumos = await _context.Consumos
                    .Include(c => c.Insumo)
                        .ThenInclude(i => i!.Clase)
                    .Where(c => c.Fecha >= fechaDesdeOnly && c.Fecha <= fechaHastaOnly)
                    .GroupBy(c => new { c.Insumo!.IdInsumo, c.Insumo.Nombre, c.Insumo.IdFox })
                    .Select(g => new
                    {
                        g.Key.IdInsumo,
                        g.Key.Nombre,
                        g.Key.IdFox,
                        TotalConsumido = g.Sum(c => c.Cantidad ?? 0),
                        ValorTotal = g.Sum(c => (c.Cantidad ?? 0) * (c.Insumo!.PrecioUnitario ?? 0)),
                        FrecuenciaConsumo = g.Count()
                    })
                    .OrderByDescending(x => x.ValorTotal)
                    .Take(15)
                    .ToListAsync();

                return Ok(new
                {
                    Periodo = new { FechaDesde = fechaDesdeDefault, FechaHasta = fechaHastaDefault },
                    ResumenGeneral = estadisticas,
                    PorArea = porArea,
                    PorClase = porClase,
                    PorMes = porMes,
                    TopInsumos = topInsumos
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener estadísticas de consumos");
                return StatusCode(500, "Error interno del servidor");
            }
        }

        /// <summary>
        /// Obtener áreas con más consumo
        /// </summary>
        [HttpGet("areas")]
        public async Task<ActionResult<IEnumerable<object>>> GetAreas()
        {
            try
            {
                var areas = await _context.Consumos
                    .GroupBy(c => c.Area)
                    .Select(g => new
                    {
                        Area = g.Key,
                        TotalConsumos = g.Count(),
                        UltimoConsumo = g.Max(c => c.Fecha)
                    })
                    .OrderByDescending(a => a.TotalConsumos)
                    .ToListAsync();

                return Ok(areas);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener áreas");
                return StatusCode(500, "Error interno del servidor");
            }
        }
    }

    // DTOs para Consumos
    public class ConsumoCreateDto
    {
        public int IdInsumo { get; set; }
        public string? Area { get; set; }
        public DateTime? Fecha { get; set; }
        public float? Cantidad { get; set; }
        public int? IdLote { get; set; }
        public int? IdAlmacen { get; set; }
    }

    public class CambiarEstadoConsumoDto
    {
        public string Estado { get; set; } = string.Empty;
    }
} 