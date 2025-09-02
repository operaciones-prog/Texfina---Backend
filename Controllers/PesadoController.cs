using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TexfinaApi.Data;
using TexfinaApi.Models;
using TexfinaApi.DTOs;
using System.Security.Claims;

namespace TexfinaApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class PesadoController : ControllerBase
    {
        private readonly TexfinaDbContext _context;
        private readonly ILogger<PesadoController> _logger;

        public PesadoController(TexfinaDbContext context, ILogger<PesadoController> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Obtener historial de pesadas con filtros
        /// </summary>
        [HttpGet("historial")]
        public async Task<ActionResult<IEnumerable<object>>> GetHistorialPesadas(
            [FromQuery] int? idInsumo = null,
            [FromQuery] int? idLote = null,
            [FromQuery] DateTime? fechaDesde = null,
            [FromQuery] DateTime? fechaHasta = null,
            [FromQuery] string? operador = null,
            [FromQuery] int pagina = 1,
            [FromQuery] int tamaño = 10)
        {
            try
            {
                // Por ahora usar tabla CONSUMO como proxy de pesadas
                // En producción se crearían tablas específicas para pesadas
                var query = _context.Consumos
                    .Include(c => c.Insumo)
                        .ThenInclude(i => i!.Clase)
                    .Include(c => c.Lote)
                    .AsQueryable();

                // Filtros
                if (idInsumo.HasValue)
                {
                    query = query.Where(c => c.IdInsumo == idInsumo);
                }

                if (idLote.HasValue)
                {
                    query = query.Where(c => c.IdLote == idLote);
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

                if (!string.IsNullOrEmpty(operador))
                {
                    query = query.Where(c => c.Area!.Contains(operador));
                }

                // Paginación
                var total = await query.CountAsync();
                
                var pesadas = await query
                    .OrderByDescending(c => c.Fecha)
                    .Skip((pagina - 1) * tamaño)
                    .Take(tamaño)
                    .Select(c => new
                    {
                        IdPesada = c.IdConsumo,
                        Fecha = c.Fecha.HasValue ? c.Fecha.Value.ToDateTime(TimeOnly.MinValue) : (DateTime?)null,
                        Insumo = new
                        {
                            c.Insumo!.IdInsumo,
                            c.Insumo.Nombre,
                            c.Insumo.IdFox,
                            Clase = c.Insumo.Clase!.Familia
                        },
                        Lote = new
                        {
                            c.Lote!.IdLote,
                            Numero = c.Lote.Numero,
                            c.Lote.FechaExpiracion
                        },
                        PesoBruto = c.Cantidad,
                        PesoNeto = c.Cantidad, // Simulado
                        PesoTara = 0.0, // Simulado
                        Operador = c.Area,
                        Estado = c.Estado ?? "COMPLETADO",
                        // Simulación de datos de balanza
                        DatosBalanza = new
                        {
                            ModeloBalanza = "Mettler MS32001L",
                            NumeroSerie = "MT-" + (c.IdConsumo % 1000).ToString("D4"),
                            Calibracion = DateTime.Now.AddDays(-30),
                            EstadoCalibr = "CALIBRADA"
                        }
                    })
                    .ToListAsync();

                var respuesta = new
                {
                    Datos = pesadas,
                    TotalRegistros = total,
                    PaginaActual = pagina,
                    TotalPaginas = (int)Math.Ceiling(total / (double)tamaño),
                    TamañoPagina = tamaño
                };

                return Ok(respuesta);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener historial de pesadas");
                return StatusCode(500, new { error = "Error interno del servidor", message = ex.Message });
            }
        }

        /// <summary>
        /// Registrar nueva pesada manual
        /// </summary>
        [HttpPost("registrar")]
        public async Task<ActionResult<object>> RegistrarPesada(RegistrarPesadaDto pesadaDto)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (!int.TryParse(userIdClaim, out var userId))
                {
                    return BadRequest("ID de usuario inválido");
                }

                // Validar que el insumo existe
                var insumo = await _context.Insumos.FindAsync(pesadaDto.IdInsumo);
                if (insumo == null)
                {
                    return BadRequest(new { error = "Insumo no encontrado" });
                }

                // Validar que el lote existe y tiene stock
                var lote = await _context.Lotes.FindAsync(pesadaDto.IdLote);
                if (lote == null)
                {
                    return BadRequest(new { error = "Lote no encontrado" });
                }

                if ((lote.StockActual ?? 0) < pesadaDto.PesoNeto)
                {
                    return BadRequest(new { error = "Stock insuficiente en el lote" });
                }

                // Validar rangos de peso
                if (pesadaDto.PesoNeto <= 0)
                {
                    return BadRequest(new { error = "El peso neto debe ser mayor a 0" });
                }

                if (pesadaDto.PesoBruto < pesadaDto.PesoNeto)
                {
                    return BadRequest(new { error = "El peso bruto no puede ser menor al peso neto" });
                }

                // Por ahora registrar como consumo
                // En producción se crearían tablas específicas para pesadas
                var consumo = new Consumo
                {
                    IdInsumo = pesadaDto.IdInsumo,
                    IdLote = pesadaDto.IdLote,
                    Fecha = DateOnly.FromDateTime(DateTime.Now),
                    Cantidad = (float)pesadaDto.PesoNeto,
                    Area = pesadaDto.Operador ?? $"Usuario-{userId}",
                    Estado = "PESADO_REGISTRADO"
                };

                _context.Consumos.Add(consumo);

                // Actualizar stock del lote
                lote.StockActual = (lote.StockActual ?? 0) - (float)pesadaDto.PesoNeto;
                
                await _context.SaveChangesAsync();

                // Crear log del evento
                var logEvento = new LogEvento
                {
                    IdUsuario = userId,
                    Accion = "PESADA_REGISTRADA",
                    Descripcion = $"Pesada registrada: {pesadaDto.PesoNeto}kg de {insumo.Nombre} (Lote: {lote.Numero})",
                    IpOrigen = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown",
                    Modulo = "PESADO",
                    TablaAfectada = "CONSUMO",
                    Timestamp = DateTime.UtcNow
                };

                _context.LogEventos.Add(logEvento);
                await _context.SaveChangesAsync();

                var resultado = new
                {
                    IdPesada = consumo.IdConsumo,
                    Mensaje = "Pesada registrada exitosamente",
                    Detalles = new
                    {
                        Insumo = insumo.Nombre,
                        Lote = lote.Numero,
                        PesoNeto = pesadaDto.PesoNeto,
                        StockRestante = lote.StockActual,
                        Timestamp = DateTime.Now
                    }
                };

                return Ok(resultado);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al registrar pesada");
                return StatusCode(500, new { error = "Error interno del servidor", message = ex.Message });
            }
        }

        /// <summary>
        /// Capturar peso desde balanza electrónica (simulado)
        /// </summary>
        [HttpPost("capturar-peso")]
        public async Task<ActionResult<object>> CapturarPesoElectronico(CapturarPesoDto capturarDto)
        {
            try
            {
                // Simulación de comunicación con balanza electrónica
                // En producción esto se conectaría con el hardware real

                var random = new Random();
                
                // Simular lectura de balanza
                var pesoSimulado = capturarDto.PesoEsperado + random.NextDouble() * 2 - 1; // ±1kg variación
                var pesoRedondeado = Math.Round(pesoSimulado, 3);

                // Simular validaciones de la balanza
                var estadoBalanza = new
                {
                    Estable = random.Next(0, 100) > 5, // 95% probabilidad estable
                    Calibrada = true,
                    Error = false,
                    Temperatura = 20 + random.NextDouble() * 10,
                    Humedad = 45 + random.NextDouble() * 20
                };

                if (!estadoBalanza.Estable)
                {
                    return BadRequest(new { 
                        error = "La balanza no está estable", 
                        codigo = "BALANZA_INESTABLE",
                        reintentarEn = 3
                    });
                }

                // Validar que el peso esté dentro de rangos aceptables
                var tolerancia = capturarDto.Tolerancia ?? 0.1; // 10% tolerancia por defecto
                var rangoMinimo = capturarDto.PesoEsperado * (1 - tolerancia);
                var rangoMaximo = capturarDto.PesoEsperado * (1 + tolerancia);

                var dentroDeTolerancias = pesoRedondeado >= rangoMinimo && pesoRedondeado <= rangoMaximo;

                var resultado = new
                {
                    PesoCapturado = pesoRedondeado,
                    PesoEsperado = capturarDto.PesoEsperado,
                    Diferencia = Math.Round(pesoRedondeado - capturarDto.PesoEsperado, 3),
                    DentroDeTolerancias = dentroDeTolerancias,
                    Tolerancia = tolerancia,
                    RangoMinimo = Math.Round(rangoMinimo, 3),
                    RangoMaximo = Math.Round(rangoMaximo, 3),
                    Timestamp = DateTime.UtcNow,
                    DatosBalanza = new
                    {
                        Modelo = capturarDto.ModeloBalanza ?? "Mettler MS32001L",
                        NumeroSerie = capturarDto.NumeroSerie ?? "MT-0001",
                        EstadoBalanza = estadoBalanza,
                        UltimaCalibr = DateTime.Now.AddDays(-15),
                        ProximaCalibr = DateTime.Now.AddDays(45)
                    }
                };

                return Ok(resultado);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al capturar peso electrónico");
                return StatusCode(500, new { error = "Error interno del servidor", message = ex.Message });
            }
        }

        /// <summary>
        /// Obtener configuración de balanzas
        /// </summary>
        [HttpGet("balanzas")]
        public async Task<ActionResult<IEnumerable<object>>> GetConfiguracionBalanzas()
        {
            try
            {
                // Simulación de configuración de balanzas
                // En producción esto vendría de una tabla de configuración
                var balanzas = new[]
                {
                    new
                    {
                        Id = 1,
                        Modelo = "Mettler MS32001L",
                        NumeroSerie = "MT-0001",
                        Ubicacion = "Almacén Principal",
                        CapacidadMaxima = 320.0,
                        Precision = 0.001,
                        Estado = "ACTIVA",
                        UltimaCalibr = DateTime.Now.AddDays(-15),
                        ProximaCalibr = DateTime.Now.AddDays(45),
                        Configuracion = new
                        {
                            Puerto = "COM3",
                            BaudRate = 9600,
                            TiempoEstabilizacion = 3,
                            AutoTara = true
                        }
                    },
                    new
                    {
                        Id = 2,
                        Modelo = "Mettler MS303TS",
                        NumeroSerie = "MT-0002",
                        Ubicacion = "Laboratorio",
                        CapacidadMaxima = 320.0,
                        Precision = 0.001,
                        Estado = "ACTIVA",
                        UltimaCalibr = DateTime.Now.AddDays(-8),
                        ProximaCalibr = DateTime.Now.AddDays(52),
                        Configuracion = new
                        {
                            Puerto = "COM4",
                            BaudRate = 9600,
                            TiempoEstabilizacion = 2,
                            AutoTara = true
                        }
                    },
                    new
                    {
                        Id = 3,
                        Modelo = "Mettler MS32001L",
                        NumeroSerie = "MT-0003",
                        Ubicacion = "Área de Producción",
                        CapacidadMaxima = 320.0,
                        Precision = 0.001,
                        Estado = "MANTENIMIENTO",
                        UltimaCalibr = DateTime.Now.AddDays(-62),
                        ProximaCalibr = DateTime.Now.AddDays(-2), // Vencida
                        Configuracion = new
                        {
                            Puerto = "COM5",
                            BaudRate = 9600,
                            TiempoEstabilizacion = 3,
                            AutoTara = false
                        }
                    }
                };

                return Ok(balanzas);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener configuración de balanzas");
                return StatusCode(500, new { error = "Error interno del servidor", message = ex.Message });
            }
        }

        /// <summary>
        /// Calibrar balanza
        /// </summary>
        [HttpPost("calibrar")]
        public async Task<ActionResult<object>> CalibrarBalanza(CalibrarBalanzaDto calibrarDto)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (!int.TryParse(userIdClaim, out var userId))
                {
                    return BadRequest("ID de usuario inválido");
                }

                // Simulación de proceso de calibración
                await Task.Delay(2000); // Simular tiempo de calibración

                var random = new Random();
                var exito = random.Next(0, 100) > 5; // 95% probabilidad de éxito

                if (!exito)
                {
                    return BadRequest(new 
                    { 
                        error = "Error en el proceso de calibración",
                        codigo = "CALIBRACION_FALLIDA",
                        detalles = "Verificar pesas de calibración y condiciones ambientales"
                    });
                }

                // Crear log del evento de calibración
                var logEvento = new LogEvento
                {
                    IdUsuario = userId,
                    Accion = "CALIBRACION_BALANZA",
                    Descripcion = $"Balanza {calibrarDto.NumeroSerie} calibrada exitosamente",
                    IpOrigen = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown",
                    Modulo = "PESADO",
                    TablaAfectada = "BALANZAS",
                    Timestamp = DateTime.UtcNow
                };

                _context.LogEventos.Add(logEvento);
                await _context.SaveChangesAsync();

                var resultado = new
                {
                    Mensaje = "Calibración completada exitosamente",
                    Balanza = new
                    {
                        calibrarDto.NumeroSerie,
                        calibrarDto.Modelo,
                        FechaCalibr = DateTime.UtcNow,
                        ProximaCalibr = DateTime.UtcNow.AddDays(60),
                        Estado = "CALIBRADA",
                        CertificadoCalibr = $"CERT-{DateTime.Now:yyyyMMdd}-{calibrarDto.NumeroSerie}"
                    },
                    PesasUtilizadas = calibrarDto.PesasCalibr ?? new[] { 0.0, 100.0, 200.0, 320.0 },
                    ResultadosCalibr = new
                    {
                        Linearidad = "ACEPTABLE",
                        Repetibilidad = "EXCELENTE", 
                        ErrorMaximo = 0.002,
                        Incertidumbre = 0.001
                    }
                };

                return Ok(resultado);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al calibrar balanza");
                return StatusCode(500, new { error = "Error interno del servidor", message = ex.Message });
            }
        }

        /// <summary>
        /// Obtener estadísticas de pesado
        /// </summary>
        [HttpGet("estadisticas")]
        public async Task<ActionResult<object>> GetEstadisticasPesado(
            [FromQuery] DateTime? fechaDesde = null,
            [FromQuery] DateTime? fechaHasta = null)
        {
            try
            {
                var hoy = DateTime.Now;
                var fechaDesdeCalc = fechaDesde ?? hoy.AddDays(-30);
                var fechaHastaCalc = fechaHasta ?? hoy;

                var fechaDesdeOnly = DateOnly.FromDateTime(fechaDesdeCalc);
                var fechaHastaOnly = DateOnly.FromDateTime(fechaHastaCalc);

                // Usar tabla CONSUMO como proxy
                var query = _context.Consumos
                    .Where(c => c.Fecha >= fechaDesdeOnly && c.Fecha <= fechaHastaOnly);

                var totalPesadas = await query.CountAsync();
                var pesoTotalProcesado = await query.SumAsync(c => c.Cantidad);

                var pesadasPorDia = await query
                    .GroupBy(c => c.Fecha)
                    .Select(g => new
                    {
                        Fecha = g.Key.HasValue ? g.Key.Value.ToDateTime(TimeOnly.MinValue) : DateTime.MinValue,
                        Cantidad = g.Count(),
                        PesoTotal = g.Sum(c => c.Cantidad)
                    })
                    .OrderBy(x => x.Fecha)
                    .ToListAsync();

                var pesadasPorOperador = await query
                    .GroupBy(c => c.Area)
                    .Select(g => new
                    {
                        Operador = g.Key ?? "No especificado",
                        TotalPesadas = g.Count(),
                        PesoTotal = g.Sum(c => c.Cantidad)
                    })
                    .OrderByDescending(x => x.TotalPesadas)
                    .Take(10)
                    .ToListAsync();

                var insumosMasPesados = await query
                    .Include(c => c.Insumo)
                    .GroupBy(c => new { c.IdInsumo, c.Insumo!.Nombre })
                    .Select(g => new
                    {
                        Insumo = g.Key.Nombre,
                        TotalPesadas = g.Count(),
                        PesoTotal = g.Sum(c => c.Cantidad)
                    })
                    .OrderByDescending(x => x.PesoTotal)
                    .Take(10)
                    .ToListAsync();

                return Ok(new
                {
                    Periodo = new
                    {
                        FechaDesde = fechaDesdeCalc,
                        FechaHasta = fechaHastaCalc
                    },
                    ResumenGeneral = new
                    {
                        TotalPesadas = totalPesadas,
                        PesoTotalProcesado = pesoTotalProcesado,
                        PromedioPorPesada = totalPesadas > 0 ? pesoTotalProcesado / totalPesadas : 0
                    },
                    PesadasPorDia = pesadasPorDia,
                    PesadasPorOperador = pesadasPorOperador,
                    InsumosMasPesados = insumosMasPesados,
                    EstadoBalanzas = new
                    {
                        TotalBalanzas = 3,
                        Activas = 2,
                        EnMantenimiento = 1,
                        RequierenCalibracion = 1
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener estadísticas de pesado");
                return StatusCode(500, new { error = "Error interno del servidor", message = ex.Message });
            }
        }
    }
}

// DTOs para PesadoController
namespace TexfinaApi.DTOs
{
    public class RegistrarPesadaDto
    {
        public int IdInsumo { get; set; }
        public int IdLote { get; set; }
        public double PesoBruto { get; set; }
        public double PesoNeto { get; set; }
        public double PesoTara { get; set; }
        public string? Operador { get; set; }
        public string? Observaciones { get; set; }
    }

    public class CapturarPesoDto
    {
        public double PesoEsperado { get; set; }
        public double? Tolerancia { get; set; } = 0.1;
        public string? ModeloBalanza { get; set; }
        public string? NumeroSerie { get; set; }
        public int TiempoEspera { get; set; } = 3;
    }

    public class CalibrarBalanzaDto
    {
        public string NumeroSerie { get; set; } = null!;
        public string? Modelo { get; set; }
        public double[]? PesasCalibr { get; set; }
        public string? Observaciones { get; set; }
    }
}