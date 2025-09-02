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
    public class IngresosController : ControllerBase
    {
        private readonly TexfinaDbContext _context;
        private readonly ILogger<IngresosController> _logger;

        public IngresosController(TexfinaDbContext context, ILogger<IngresosController> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Obtener todos los ingresos con filtros opcionales
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<IEnumerable<object>>> GetIngresos(
            [FromQuery] string? buscar = null,
            [FromQuery] int? idInsumo = null,
            [FromQuery] int? idProveedor = null,
            [FromQuery] DateTime? fechaDesde = null,
            [FromQuery] DateTime? fechaHasta = null,
            [FromQuery] string? estado = null,
            [FromQuery] int pagina = 1,
            [FromQuery] int tamaño = 10)
        {
            try
            {
                var query = _context.Ingresos
                    .Include(i => i.Insumo)
                        .ThenInclude(ins => ins!.Clase)
                    .Include(i => i.InsumoProveedor)
                        .ThenInclude(ip => ip!.Proveedor)
                    .Include(i => i.Lote)
                    .Include(i => i.Unidad)
                    .AsQueryable();

                // Filtros
                if (!string.IsNullOrEmpty(buscar))
                {
                    query = query.Where(i => i.Insumo!.Nombre!.Contains(buscar) ||
                                           i.NumeroRemision!.Contains(buscar) ||
                                           i.OrdenCompra!.Contains(buscar));
                }

                if (idInsumo.HasValue)
                {
                    query = query.Where(i => i.IdInsumo == idInsumo);
                }

                if (idProveedor.HasValue)
                {
                    query = query.Where(i => i.InsumoProveedor!.IdProveedor == idProveedor);
                }

                if (fechaDesde.HasValue)
                {
                    var fechaDesdeOnly = DateOnly.FromDateTime(fechaDesde.Value);
                    query = query.Where(i => i.Fecha >= fechaDesdeOnly);
                }

                if (fechaHasta.HasValue)
                {
                    var fechaHastaOnly = DateOnly.FromDateTime(fechaHasta.Value);
                    query = query.Where(i => i.Fecha <= fechaHastaOnly);
                }

                if (!string.IsNullOrEmpty(estado))
                {
                    query = query.Where(i => i.Estado == estado);
                }

                // Paginación
                var total = await query.CountAsync();
                
                // CONSULTA SIMPLIFICADA - Sin conversiones DateOnly en SQL
                var ingresosRaw = await query
                    .OrderByDescending(i => i.Fecha)
                    .Skip((pagina - 1) * tamaño)
                    .Take(tamaño)
                    .ToListAsync();

                // CONVERSIONES EN MEMORIA - No en SQL
                var ingresos = ingresosRaw.Select(i => new
                {
                    IdIngreso = i.IdIngreso,
                    Fecha = i.Fecha.HasValue ? i.Fecha.Value.ToDateTime(TimeOnly.MinValue) : (DateTime?)null,
                    Cantidad = i.Cantidad,
                    Presentacion = i.Presentacion,
                    PrecioTotalFormula = i.PrecioTotalFormula,
                    PrecioUnitarioHistorico = i.PrecioUnitarioHistorico,
                    NumeroRemision = i.NumeroRemision,
                    OrdenCompra = i.OrdenCompra,
                    Estado = i.Estado,
                    Insumo = new
                    {
                        IdInsumo = i.Insumo?.IdInsumo ?? 0,
                        IdFox = i.Insumo?.IdFox ?? "Sin código",
                        Nombre = i.Insumo?.Nombre ?? "Sin nombre",
                        Clase = i.Insumo?.Clase?.Familia ?? "Sin clase"
                    },
                    Proveedor = i.InsumoProveedor?.Proveedor != null ? new
                    {
                        IdProveedor = i.InsumoProveedor.Proveedor.IdProveedor,
                        Empresa = i.InsumoProveedor.Proveedor.Empresa
                    } : null,
                    Lote = i.Lote != null ? new
                    {
                        IdLote = i.Lote.IdLote,
                        Numero = i.Lote.Numero ?? "Sin número"
                    } : null,
                    Unidad = i.Unidad?.Nombre ?? "Sin unidad"
                }).ToList();

                return Ok(new
                {
                    Data = ingresos,
                    Total = total,
                    Pagina = pagina,
                    TotalPaginas = (int)Math.Ceiling(total / (double)tamaño)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener ingresos");
                return StatusCode(500, "Error interno del servidor");
            }
        }

        /// <summary>
        /// Obtener un ingreso específico por ID
        /// </summary>
        [HttpGet("{id}")]
        public async Task<ActionResult<object>> GetIngreso(int id)
        {
            try
            {
                // CONSULTA SIMPLIFICADA - Sin conversiones DateOnly
                var ingresoRaw = await _context.Ingresos
                    .Include(i => i.Insumo)
                        .ThenInclude(ins => ins!.Clase)
                    .Include(i => i.InsumoProveedor)
                        .ThenInclude(ip => ip!.Proveedor)
                    .Include(i => i.Lote)
                    .Include(i => i.Unidad)
                    .FirstOrDefaultAsync(i => i.IdIngreso == id);

                if (ingresoRaw == null)
                {
                    return NotFound("Ingreso no encontrado");
                }

                // CONVERSIONES EN MEMORIA
                var ingreso = new
                {
                    IdIngreso = ingresoRaw.IdIngreso,
                    Fecha = ingresoRaw.Fecha.HasValue ? ingresoRaw.Fecha.Value.ToDateTime(TimeOnly.MinValue) : (DateTime?)null,
                    Cantidad = ingresoRaw.Cantidad,
                    Presentacion = ingresoRaw.Presentacion,
                    PrecioTotalFormula = ingresoRaw.PrecioTotalFormula,
                    PrecioUnitarioHistorico = ingresoRaw.PrecioUnitarioHistorico,
                    NumeroRemision = ingresoRaw.NumeroRemision,
                    OrdenCompra = ingresoRaw.OrdenCompra,
                    Estado = ingresoRaw.Estado,
                    Insumo = new
                    {
                        IdInsumo = ingresoRaw.Insumo?.IdInsumo ?? 0,
                        IdFox = ingresoRaw.Insumo?.IdFox ?? "Sin código",
                        Nombre = ingresoRaw.Insumo?.Nombre ?? "Sin nombre",
                        Presentacion = ingresoRaw.Insumo?.Presentacion ?? "Sin presentación",
                        PrecioUnitario = ingresoRaw.Insumo?.PrecioUnitario ?? 0,
                        Clase = new { 
                            IdClase = ingresoRaw.Insumo?.Clase?.IdClase ?? "Sin clase", 
                            Familia = ingresoRaw.Insumo?.Clase?.Familia ?? "Sin familia", 
                            SubFamilia = ingresoRaw.Insumo?.Clase?.SubFamilia ?? "Sin subfamilia" 
                        }
                    },
                    Proveedor = ingresoRaw.InsumoProveedor?.Proveedor != null ? new
                    {
                        IdProveedor = ingresoRaw.InsumoProveedor.Proveedor.IdProveedor,
                        Empresa = ingresoRaw.InsumoProveedor.Proveedor.Empresa,
                        Ruc = ingresoRaw.InsumoProveedor.Proveedor.Ruc,
                        Contacto = ingresoRaw.InsumoProveedor.Proveedor.Contacto,
                        PrecioAcordado = ingresoRaw.InsumoProveedor.PrecioUnitario
                    } : null,
                    Lote = ingresoRaw.Lote != null ? new
                    {
                        IdLote = ingresoRaw.Lote.IdLote,
                        Numero = ingresoRaw.Lote.Numero ?? "Sin número",
                        Ubicacion = ingresoRaw.Lote.Ubicacion,
                        StockInicial = ingresoRaw.Lote.StockInicial,
                        StockActual = ingresoRaw.Lote.StockActual,
                        FechaExpiracion = ingresoRaw.Lote.FechaExpiracion.HasValue ? 
                            ingresoRaw.Lote.FechaExpiracion.Value.ToDateTime(TimeOnly.MinValue) : (DateTime?)null,
                        EstadoLote = ingresoRaw.Lote.EstadoLote
                    } : null,
                    Unidad = ingresoRaw.Unidad != null ? new { IdUnidad = ingresoRaw.Unidad.IdUnidad, Nombre = ingresoRaw.Unidad.Nombre } : null,
                    PrecioUnitarioCalculado = ingresoRaw.Cantidad.HasValue && ingresoRaw.Cantidad > 0 ? 
                        (ingresoRaw.PrecioTotalFormula ?? 0) / ingresoRaw.Cantidad : 0
                };

                return Ok(ingreso);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener ingreso {Id}", id);
                return StatusCode(500, "Error interno del servidor");
            }
        }

        /// <summary>
        /// Registrar un nuevo ingreso de insumo
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<Ingreso>> CreateIngreso([FromBody] IngresoCreateDto ingresoDto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                using var transaction = await _context.Database.BeginTransactionAsync();

                // Verificar que el insumo existe
                var insumo = await _context.Insumos.FindAsync(ingresoDto.IdInsumo);
                if (insumo == null)
                {
                    return BadRequest("El insumo especificado no existe");
                }

                // Verificar la relación insumo-proveedor si se especifica
                InsumoProveedor? insumoProveedor = null;
                if (ingresoDto.IdInsumoProveedor.HasValue)
                {
                    insumoProveedor = await _context.InsumoProveedores
                        .FirstOrDefaultAsync(ip => ip.Id == ingresoDto.IdInsumoProveedor);
                    
                    if (insumoProveedor == null)
                    {
                        return BadRequest("La relación insumo-proveedor especificada no existe");
                    }
                }

                // Crear o buscar el lote
                Lote? lote = null;
                if (ingresoDto.IdLote.HasValue)
                {
                    lote = await _context.Lotes.FindAsync(ingresoDto.IdLote);
                    if (lote == null)
                    {
                        return BadRequest("El lote especificado no existe");
                    }
                }
                else if (!string.IsNullOrEmpty(ingresoDto.NumeroLoteNuevo))
                {
                    // Crear nuevo lote
                    lote = new Lote
                    {
                        IdInsumo = ingresoDto.IdInsumo,
                        Numero = ingresoDto.NumeroLoteNuevo,
                        Ubicacion = ingresoDto.UbicacionLote,
                        StockInicial = ingresoDto.Cantidad ?? 0,
                        StockActual = ingresoDto.Cantidad ?? 0,
                        FechaExpiracion = ingresoDto.FechaExpiracion.HasValue ? 
                            DateOnly.FromDateTime(ingresoDto.FechaExpiracion.Value) : null,
                        PrecioTotal = ingresoDto.PrecioTotalFormula,
                        EstadoLote = "ACTIVO"
                    };
                    _context.Lotes.Add(lote);
                    await _context.SaveChangesAsync(); // Para obtener el ID del lote
                }

                // Crear el ingreso
                var ingreso = new Ingreso
                {
                    IdInsumo = ingresoDto.IdInsumo,
                    IdInsumoProveedor = ingresoDto.IdInsumoProveedor,
                    Fecha = ingresoDto.Fecha.HasValue ? DateOnly.FromDateTime(ingresoDto.Fecha.Value) : DateOnly.FromDateTime(DateTime.Now),
                    Presentacion = ingresoDto.Presentacion,
                    IdUnidad = ingresoDto.IdUnidad,
                    Cantidad = ingresoDto.Cantidad,
                    IdLote = lote?.IdLote,
                    PrecioTotalFormula = ingresoDto.PrecioTotalFormula,
                    PrecioUnitarioHistorico = ingresoDto.PrecioUnitarioHistorico,
                    NumeroRemision = ingresoDto.NumeroRemision,
                    OrdenCompra = ingresoDto.OrdenCompra,
                    Estado = "RECIBIDO"
                };

                _context.Ingresos.Add(ingreso);

                // Actualizar stock si se especifica almacén
                if (ingresoDto.IdAlmacen.HasValue)
                {
                    var stockExistente = await _context.Stocks
                        .FirstOrDefaultAsync(s => s.IdInsumo == ingresoDto.IdInsumo &&
                                                s.IdLote == lote!.IdLote &&
                                                s.IdAlmacen == ingresoDto.IdAlmacen);

                    if (stockExistente != null)
                    {
                        stockExistente.Cantidad += ingresoDto.Cantidad;
                    }
                    else
                    {
                        var nuevoStock = new Stock
                        {
                            IdInsumo = ingresoDto.IdInsumo,
                            IdLote = lote!.IdLote,
                            IdAlmacen = ingresoDto.IdAlmacen,
                            IdUnidad = ingresoDto.IdUnidad,
                            Presentacion = ingresoDto.Presentacion,
                            Cantidad = ingresoDto.Cantidad,
                            FechaEntrada = DateTime.Now
                        };
                        _context.Stocks.Add(nuevoStock);
                    }
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return CreatedAtAction(nameof(GetIngreso), new { id = ingreso.IdIngreso }, ingreso);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al crear ingreso");
                return StatusCode(500, "Error interno del servidor");
            }
        }

        /// <summary>
        /// Actualizar estado de un ingreso
        /// </summary>
        [HttpPatch("{id}/estado")]
        public async Task<IActionResult> CambiarEstado(int id, [FromBody] CambiarEstadoIngresoDto dto)
        {
            try
            {
                var ingreso = await _context.Ingresos.FindAsync(id);
                if (ingreso == null)
                {
                    return NotFound("Ingreso no encontrado");
                }

                var estadosValidos = new[] { "PENDIENTE", "RECIBIDO", "PARCIAL", "CANCELADO" };
                if (!estadosValidos.Contains(dto.Estado))
                {
                    return BadRequest($"Estado inválido. Estados válidos: {string.Join(", ", estadosValidos)}");
                }

                ingreso.Estado = dto.Estado;
                await _context.SaveChangesAsync();

                return Ok(new { mensaje = $"Estado del ingreso cambiado a {dto.Estado}", ingreso });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cambiar estado del ingreso {Id}", id);
                return StatusCode(500, "Error interno del servidor");
            }
        }

        /// <summary>
        /// Obtener estadísticas de ingresos
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

                var estadisticas = await _context.Ingresos
                    .Include(i => i.Insumo)
                        .ThenInclude(ins => ins!.Clase)
                    .Where(i => i.Fecha >= fechaDesdeOnly && i.Fecha <= fechaHastaOnly)
                    .GroupBy(i => 1)
                    .Select(g => new
                    {
                        TotalIngresos = g.Count(),
                        CantidadTotal = g.Sum(i => i.Cantidad ?? 0),
                        ValorTotal = g.Sum(i => i.PrecioTotalFormula ?? 0),
                        PromedioValorIngreso = g.Average(i => i.PrecioTotalFormula ?? 0),
                        IngresosPendientes = g.Count(i => i.Estado == "PENDIENTE"),
                        IngresosRecibidos = g.Count(i => i.Estado == "RECIBIDO"),
                        IngresosCancelados = g.Count(i => i.Estado == "CANCELADO")
                    })
                    .FirstOrDefaultAsync();

                var porClase = await _context.Ingresos
                    .Include(i => i.Insumo)
                        .ThenInclude(ins => ins!.Clase)
                    .Where(i => i.Fecha >= fechaDesdeOnly && i.Fecha <= fechaHastaOnly)
                    .GroupBy(i => i.Insumo!.Clase!.Familia)
                    .Select(g => new
                    {
                        Clase = g.Key,
                        TotalIngresos = g.Count(),
                        CantidadTotal = g.Sum(i => i.Cantidad ?? 0),
                        ValorTotal = g.Sum(i => i.PrecioTotalFormula ?? 0)
                    })
                    .OrderByDescending(x => x.ValorTotal)
                    .Take(10)
                    .ToListAsync();

                var porMes = await _context.Ingresos
                    .Where(i => i.Fecha >= fechaDesdeOnly && i.Fecha <= fechaHastaOnly)
                    .GroupBy(i => new { Año = i.Fecha!.Value.Year, Mes = i.Fecha.Value.Month })
                    .Select(g => new
                    {
                        Año = g.Key.Año,
                        Mes = g.Key.Mes,
                        TotalIngresos = g.Count(),
                        ValorTotal = g.Sum(i => i.PrecioTotalFormula ?? 0)
                    })
                    .OrderBy(x => x.Año)
                    .ThenBy(x => x.Mes)
                    .ToListAsync();

                return Ok(new
                {
                    Periodo = new { FechaDesde = fechaDesdeDefault, FechaHasta = fechaHastaDefault },
                    ResumenGeneral = estadisticas,
                    PorClase = porClase,
                    PorMes = porMes
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener estadísticas de ingresos");
                return StatusCode(500, "Error interno del servidor");
            }
        }
    }

    // DTOs para Ingresos
    public class IngresoCreateDto
    {
        public int IdInsumo { get; set; }
        public int? IdInsumoProveedor { get; set; }
        public DateTime? Fecha { get; set; }
        public string? Presentacion { get; set; }
        public string? IdUnidad { get; set; }
        public float? Cantidad { get; set; }
        public int? IdLote { get; set; }
        public string? NumeroLoteNuevo { get; set; }
        public string? UbicacionLote { get; set; }
        public DateTime? FechaExpiracion { get; set; }
        public float? PrecioTotalFormula { get; set; }
        public float? PrecioUnitarioHistorico { get; set; }
        public string? NumeroRemision { get; set; }
        public string? OrdenCompra { get; set; }
        public int? IdAlmacen { get; set; }
    }

    public class CambiarEstadoIngresoDto
    {
        public string Estado { get; set; } = string.Empty;
    }
} 