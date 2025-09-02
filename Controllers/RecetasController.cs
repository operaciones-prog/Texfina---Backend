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
    public class RecetasController : ControllerBase
    {
        private readonly TexfinaDbContext _context;
        private readonly ILogger<RecetasController> _logger;

        public RecetasController(TexfinaDbContext context, ILogger<RecetasController> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Obtener todas las recetas con resumen de insumos
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<IEnumerable<object>>> GetRecetas([FromQuery] string? buscar = null)
        {
            try
            {
                var query = _context.Recetas
                    .Include(r => r.RecetaDetalles)
                        .ThenInclude(rd => rd.Insumo)
                            .ThenInclude(i => i!.Clase)
                    .AsQueryable();

                if (!string.IsNullOrEmpty(buscar))
                {
                    query = query.Where(r => r.Nombre!.Contains(buscar));
                }

                var recetas = await query
                    .Select(r => new
                    {
                        r.IdReceta,
                        r.Nombre,
                        TotalInsumos = r.RecetaDetalles.Count(),
                        CostoEstimado = r.RecetaDetalles.Sum(rd => (rd.Proporcion ?? 0) * (rd.Insumo!.PrecioUnitario ?? 0)),
                        ClasesInvolucradas = r.RecetaDetalles.Select(rd => rd.Insumo!.Clase!.Familia).Distinct().Count(),
                        UltimaModificacion = r.RecetaDetalles.Max(rd => rd.Insumo!.UpdatedAt),
                        Complejidad = r.RecetaDetalles.Count() <= 5 ? "SIMPLE" :
                                    r.RecetaDetalles.Count() <= 15 ? "MEDIA" : "COMPLEJA"
                    })
                    .OrderBy(r => r.Nombre)
                    .ToListAsync();

                return Ok(recetas);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener recetas");
                return StatusCode(500, "Error interno del servidor");
            }
        }

        /// <summary>
        /// Obtener una receta específica con todos sus detalles
        /// </summary>
        [HttpGet("{id}")]
        public async Task<ActionResult<object>> GetReceta(int id)
        {
            try
            {
                var receta = await _context.Recetas
                    .Include(r => r.RecetaDetalles)
                        .ThenInclude(rd => rd.Insumo)
                            .ThenInclude(i => i!.Clase)
                    .Include(r => r.RecetaDetalles)
                        .ThenInclude(rd => rd.Insumo)
                            .ThenInclude(i => i!.Unidad)
                    .Where(r => r.IdReceta == id)
                    .Select(r => new
                    {
                        r.IdReceta,
                        r.Nombre,
                        Detalles = r.RecetaDetalles.OrderBy(rd => rd.Orden).Select(rd => new
                        {
                            rd.Id,
                            rd.Proporcion,
                            rd.Orden,
                            rd.TipoMedida,
                            Insumo = new
                            {
                                rd.Insumo!.IdInsumo,
                                rd.Insumo.IdFox,
                                rd.Insumo.Nombre,
                                rd.Insumo.Presentacion,
                                rd.Insumo.PrecioUnitario,
                                Clase = new { rd.Insumo.Clase!.IdClase, rd.Insumo.Clase.Familia },
                                Unidad = rd.Insumo.Unidad != null ? rd.Insumo.Unidad.Nombre : null
                            },
                            CostoLinea = (rd.Proporcion ?? 0) * (rd.Insumo.PrecioUnitario ?? 0),
                            StockDisponible = rd.Insumo.Stocks.Sum(s => s.Cantidad ?? 0)
                        }),
                        Resumen = new
                        {
                            TotalInsumos = r.RecetaDetalles.Count(),
                            CostoTotal = r.RecetaDetalles.Sum(rd => (rd.Proporcion ?? 0) * (rd.Insumo!.PrecioUnitario ?? 0)),
                            ClasesInvolucradas = r.RecetaDetalles.Select(rd => rd.Insumo!.Clase!.Familia).Distinct(),
                            DisponibilidadCompleta = r.RecetaDetalles.All(rd => 
                                rd.Insumo!.Stocks.Sum(s => s.Cantidad ?? 0) >= rd.Proporcion),
                            InsumosConStockInsuficiente = r.RecetaDetalles.Count(rd => 
                                rd.Insumo!.Stocks.Sum(s => s.Cantidad ?? 0) < rd.Proporcion)
                        }
                    })
                    .FirstOrDefaultAsync();

                if (receta == null)
                {
                    return NotFound("Receta no encontrada");
                }

                return Ok(receta);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener receta {Id}", id);
                return StatusCode(500, "Error interno del servidor");
            }
        }

        /// <summary>
        /// Crear una nueva receta
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<Receta>> CreateReceta([FromBody] RecetaCreateDto recetaDto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                using var transaction = await _context.Database.BeginTransactionAsync();

                // Verificar que no existe una receta con el mismo nombre
                var existeReceta = await _context.Recetas.AnyAsync(r => r.Nombre == recetaDto.Nombre);
                if (existeReceta)
                {
                    return BadRequest("Ya existe una receta con ese nombre");
                }

                // Crear la receta
                var receta = new Receta
                {
                    Nombre = recetaDto.Nombre
                };

                _context.Recetas.Add(receta);
                await _context.SaveChangesAsync(); // Para obtener el ID

                // Agregar los detalles
                if (recetaDto.Detalles != null && recetaDto.Detalles.Any())
                {
                    var orden = 1;
                    foreach (var detalle in recetaDto.Detalles)
                    {
                        // Verificar que el insumo existe
                        var insumo = await _context.Insumos.FindAsync(detalle.IdInsumo);
                        if (insumo == null)
                        {
                            return BadRequest($"El insumo {detalle.IdInsumo} no existe");
                        }

                        var recetaDetalle = new RecetaDetalle
                        {
                            IdReceta = receta.IdReceta,
                            IdInsumo = detalle.IdInsumo,
                            Proporcion = detalle.Proporcion,
                            Orden = detalle.Orden ?? orden++,
                            TipoMedida = detalle.TipoMedida ?? "CANTIDAD"
                        };

                        _context.RecetaDetalles.Add(recetaDetalle);
                    }
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return CreatedAtAction(nameof(GetReceta), new { id = receta.IdReceta }, receta);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al crear receta");
                return StatusCode(500, "Error interno del servidor");
            }
        }

        /// <summary>
        /// Actualizar una receta existente
        /// </summary>
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateReceta(int id, [FromBody] RecetaUpdateDto recetaDto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                using var transaction = await _context.Database.BeginTransactionAsync();

                var receta = await _context.Recetas
                    .Include(r => r.RecetaDetalles)
                    .FirstOrDefaultAsync(r => r.IdReceta == id);

                if (receta == null)
                {
                    return NotFound("Receta no encontrada");
                }

                // Actualizar nombre si se proporciona
                if (!string.IsNullOrEmpty(recetaDto.Nombre) && recetaDto.Nombre != receta.Nombre)
                {
                    var existeNombre = await _context.Recetas
                        .AnyAsync(r => r.Nombre == recetaDto.Nombre && r.IdReceta != id);

                    if (existeNombre)
                    {
                        return BadRequest("Ya existe una receta con ese nombre");
                    }

                    receta.Nombre = recetaDto.Nombre;
                }

                // Actualizar detalles si se proporcionan
                if (recetaDto.Detalles != null)
                {
                    // Eliminar detalles existentes
                    _context.RecetaDetalles.RemoveRange(receta.RecetaDetalles);

                    // Agregar nuevos detalles
                    var orden = 1;
                    foreach (var detalle in recetaDto.Detalles)
                    {
                        var insumo = await _context.Insumos.FindAsync(detalle.IdInsumo);
                        if (insumo == null)
                        {
                            return BadRequest($"El insumo {detalle.IdInsumo} no existe");
                        }

                        var recetaDetalle = new RecetaDetalle
                        {
                            IdReceta = receta.IdReceta,
                            IdInsumo = detalle.IdInsumo,
                            Proporcion = detalle.Proporcion,
                            Orden = detalle.Orden ?? orden++,
                            TipoMedida = detalle.TipoMedida ?? "CANTIDAD"
                        };

                        _context.RecetaDetalles.Add(recetaDetalle);
                    }
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(receta);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al actualizar receta {Id}", id);
                return StatusCode(500, "Error interno del servidor");
            }
        }

        /// <summary>
        /// Eliminar una receta
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteReceta(int id)
        {
            try
            {
                var receta = await _context.Recetas
                    .Include(r => r.RecetaDetalles)
                    .FirstOrDefaultAsync(r => r.IdReceta == id);

                if (receta == null)
                {
                    return NotFound("Receta no encontrada");
                }

                _context.RecetaDetalles.RemoveRange(receta.RecetaDetalles);
                _context.Recetas.Remove(receta);
                await _context.SaveChangesAsync();

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al eliminar receta {Id}", id);
                return StatusCode(500, "Error interno del servidor");
            }
        }

        /// <summary>
        /// Verificar disponibilidad de stock para producir una receta
        /// </summary>
        [HttpPost("{id}/verificar-stock")]
        public async Task<ActionResult<object>> VerificarStock(int id, [FromBody] VerificarStockDto dto)
        {
            try
            {
                var receta = await _context.Recetas
                    .Include(r => r.RecetaDetalles)
                        .ThenInclude(rd => rd.Insumo)
                            .ThenInclude(i => i!.Stocks)
                    .FirstOrDefaultAsync(r => r.IdReceta == id);

                if (receta == null)
                {
                    return NotFound("Receta no encontrada");
                }

                var multiplicador = dto.CantidadAProducir ?? 1;
                var resultadoVerificacion = new List<object>();
                var puedeProducir = true;

                foreach (var detalle in receta.RecetaDetalles)
                {
                    var cantidadNecesaria = (detalle.Proporcion ?? 0) * multiplicador;
                    var stockDisponible = detalle.Insumo!.Stocks.Sum(s => s.Cantidad ?? 0);
                    var suficiente = stockDisponible >= cantidadNecesaria;

                    if (!suficiente) puedeProducir = false;

                    resultadoVerificacion.Add(new
                    {
                        IdInsumo = detalle.Insumo.IdInsumo,
                        NombreInsumo = detalle.Insumo.Nombre,
                        CantidadNecesaria = cantidadNecesaria,
                        StockDisponible = stockDisponible,
                        Suficiente = suficiente,
                        Faltante = suficiente ? 0 : cantidadNecesaria - stockDisponible
                    });
                }

                return Ok(new
                {
                    IdReceta = receta.IdReceta,
                    NombreReceta = receta.Nombre,
                    CantidadAProducir = multiplicador,
                    PuedeProducir = puedeProducir,
                    DetalleVerificacion = resultadoVerificacion,
                    CostoTotal = receta.RecetaDetalles.Sum(rd => (rd.Proporcion ?? 0) * multiplicador * (rd.Insumo!.PrecioUnitario ?? 0)),
                    InsumosInsuficientes = resultadoVerificacion.Count(r => !(bool)r.GetType().GetProperty("Suficiente")!.GetValue(r)!)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al verificar stock para receta {Id}", id);
                return StatusCode(500, "Error interno del servidor");
            }
        }

        /// <summary>
        /// Simular consumo de una receta (sin confirmar)
        /// </summary>
        [HttpPost("{id}/simular-consumo")]
        public async Task<ActionResult<object>> SimularConsumo(int id, [FromBody] SimularConsumoDto dto)
        {
            try
            {
                var receta = await _context.Recetas
                    .Include(r => r.RecetaDetalles)
                        .ThenInclude(rd => rd.Insumo)
                            .ThenInclude(i => i!.Stocks)
                                .ThenInclude(s => s.Lote)
                    .FirstOrDefaultAsync(r => r.IdReceta == id);

                if (receta == null)
                {
                    return NotFound("Receta no encontrada");
                }

                var multiplicador = dto.CantidadAProducir ?? 1;
                var simulacion = new List<object>();

                foreach (var detalle in receta.RecetaDetalles)
                {
                    var cantidadNecesaria = (detalle.Proporcion ?? 0) * multiplicador;
                    var stocksOrdenados = detalle.Insumo!.Stocks
                        .Where(s => s.Cantidad > 0)
                        .OrderBy(s => s.FechaEntrada) // FIFO
                        .ToList();

                    var movimientos = new List<object>();
                    var cantidadRestante = cantidadNecesaria;

                    foreach (var stock in stocksOrdenados)
                    {
                        if (cantidadRestante <= 0) break;

                        var cantidadAConsumir = Math.Min(stock.Cantidad ?? 0, cantidadRestante);
                        cantidadRestante -= cantidadAConsumir;

                        movimientos.Add(new
                        {
                            IdStock = stock.IdStock,
                            CantidadActual = stock.Cantidad,
                            CantidadAConsumir = cantidadAConsumir,
                            CantidadRestante = (stock.Cantidad ?? 0) - cantidadAConsumir,
                            Lote = stock.Lote != null ? stock.Lote.Numero : "Sin lote",
                            FechaIngreso = stock.FechaEntrada
                        });
                    }

                    simulacion.Add(new
                    {
                        IdInsumo = detalle.Insumo.IdInsumo,
                        NombreInsumo = detalle.Insumo.Nombre,
                        CantidadNecesaria = cantidadNecesaria,
                        CantidadDisponible = stocksOrdenados.Sum(s => s.Cantidad ?? 0),
                        PuedeConsumir = cantidadRestante <= 0,
                        Faltante = cantidadRestante > 0 ? cantidadRestante : 0,
                        Movimientos = movimientos
                    });
                }

                return Ok(new
                {
                    IdReceta = receta.IdReceta,
                    NombreReceta = receta.Nombre,
                    CantidadAProducir = multiplicador,
                    Area = dto.Area,
                    Simulacion = simulacion,
                    EsFactible = simulacion.All(s => (bool)s.GetType().GetProperty("PuedeConsumir")!.GetValue(s)!)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al simular consumo para receta {Id}", id);
                return StatusCode(500, "Error interno del servidor");
            }
        }

        /// <summary>
        /// Ejecutar consumo real de una receta
        /// </summary>
        [HttpPost("{id}/ejecutar-consumo")]
        public async Task<ActionResult<object>> EjecutarConsumo(int id, [FromBody] EjecutarConsumoDto dto)
        {
            try
            {
                using var transaction = await _context.Database.BeginTransactionAsync();

                var receta = await _context.Recetas
                    .Include(r => r.RecetaDetalles)
                        .ThenInclude(rd => rd.Insumo)
                            .ThenInclude(i => i!.Stocks)
                                .ThenInclude(s => s.Lote)
                    .FirstOrDefaultAsync(r => r.IdReceta == id);

                if (receta == null)
                {
                    return NotFound("Receta no encontrada");
                }

                var multiplicador = dto.CantidadAProducir ?? 1;
                var consumosCreados = new List<object>();

                foreach (var detalle in receta.RecetaDetalles)
                {
                    var cantidadNecesaria = (detalle.Proporcion ?? 0) * multiplicador;
                    
                    // Verificar stock disponible
                    var stockDisponible = detalle.Insumo!.Stocks.Sum(s => s.Cantidad ?? 0);
                    if (stockDisponible < cantidadNecesaria)
                    {
                        return BadRequest($"Stock insuficiente para {detalle.Insumo.Nombre}. Disponible: {stockDisponible}, Necesario: {cantidadNecesaria}");
                    }

                    // Crear consumo
                    var consumo = new Consumo
                    {
                        IdInsumo = detalle.Insumo.IdInsumo,
                        Area = dto.Area ?? "PRODUCCION",
                        Fecha = DateOnly.FromDateTime(DateTime.Now),
                        Cantidad = cantidadNecesaria,
                        Estado = "CONFIRMADO"
                    };

                    _context.Consumos.Add(consumo);

                    // Actualizar stocks FIFO
                    var stocksOrdenados = detalle.Insumo.Stocks
                        .Where(s => s.Cantidad > 0)
                        .OrderBy(s => s.FechaEntrada)
                        .ToList();

                    var cantidadRestante = cantidadNecesaria;

                    foreach (var stock in stocksOrdenados)
                    {
                        if (cantidadRestante <= 0) break;

                        var cantidadAReducir = Math.Min(stock.Cantidad ?? 0, cantidadRestante);
                        stock.Cantidad -= cantidadAReducir;
                        cantidadRestante -= cantidadAReducir;

                        if (stock.Cantidad <= 0)
                        {
                            stock.FechaSalida = DateTime.Now;
                        }

                        // Actualizar lote si existe
                        if (stock.Lote != null)
                        {
                            stock.Lote.StockActual -= cantidadAReducir;
                            if (stock.Lote.StockActual <= 0)
                            {
                                stock.Lote.EstadoLote = "AGOTADO";
                            }
                        }
                    }

                    consumosCreados.Add(new
                    {
                        IdConsumo = consumo.IdConsumo,
                        IdInsumo = detalle.Insumo.IdInsumo,
                        NombreInsumo = detalle.Insumo.Nombre,
                        CantidadConsumida = cantidadNecesaria
                    });
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new
                {
                    mensaje = $"Consumo ejecutado exitosamente para la receta {receta.Nombre}",
                    IdReceta = receta.IdReceta,
                    NombreReceta = receta.Nombre,
                    CantidadProducida = multiplicador,
                    Area = dto.Area,
                    FechaEjecucion = DateTime.Now,
                    ConsumosCreados = consumosCreados
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al ejecutar consumo para receta {Id}", id);
                return StatusCode(500, "Error interno del servidor");
            }
        }
    }

    // DTOs para Recetas
    public class RecetaCreateDto
    {
        public string? Nombre { get; set; }
        public List<RecetaDetalleDto>? Detalles { get; set; }
    }

    public class RecetaUpdateDto
    {
        public string? Nombre { get; set; }
        public List<RecetaDetalleDto>? Detalles { get; set; }
    }

    public class RecetaDetalleDto
    {
        public int IdInsumo { get; set; }
        public float? Proporcion { get; set; }
        public int? Orden { get; set; }
        public string? TipoMedida { get; set; }
    }

    public class VerificarStockDto
    {
        public float? CantidadAProducir { get; set; } = 1;
    }

    public class SimularConsumoDto
    {
        public float? CantidadAProducir { get; set; } = 1;
        public string? Area { get; set; }
    }

    public class EjecutarConsumoDto
    {
        public float? CantidadAProducir { get; set; } = 1;
        public string? Area { get; set; }
    }
} 