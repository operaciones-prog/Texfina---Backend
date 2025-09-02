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
    public class ProveedoresController : ControllerBase
    {
        private readonly TexfinaDbContext _context;
        private readonly ILogger<ProveedoresController> _logger;

        public ProveedoresController(TexfinaDbContext context, ILogger<ProveedoresController> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Obtener todos los proveedores con filtros opcionales
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<IEnumerable<object>>> GetProveedores(
            [FromQuery] string? buscar = null,
            [FromQuery] int pagina = 1,
            [FromQuery] int tamaño = 10)
        {
            try
            {
                var query = _context.Proveedores
                    .Include(p => p.InsumoProveedores)
                        .ThenInclude(ip => ip.Insumo)
                    .AsQueryable();

                // Filtros
                if (!string.IsNullOrEmpty(buscar))
                {
                    query = query.Where(p => p.Empresa!.Contains(buscar) || 
                                           p.Ruc!.Contains(buscar) ||
                                           p.Contacto!.Contains(buscar));
                }

                // Paginación
                var total = await query.CountAsync();
                var proveedores = await query
                    .Skip((pagina - 1) * tamaño)
                    .Take(tamaño)
                    .Select(p => new
                    {
                        p.IdProveedor,
                        p.Empresa,
                        p.Ruc,
                        p.Contacto,
                        p.Direccion,
                        p.CreatedAt,
                        p.UpdatedAt,
                        TotalInsumos = p.InsumoProveedores.Count(),
                        InsumosActivos = p.InsumoProveedores.Count(ip => ip.Insumo != null)
                    })
                    .ToListAsync();

                return Ok(new
                {
                    Data = proveedores,
                    Total = total,
                    Pagina = pagina,
                    TotalPaginas = (int)Math.Ceiling(total / (double)tamaño)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener proveedores");
                return StatusCode(500, "Error interno del servidor");
            }
        }

        /// <summary>
        /// Obtener un proveedor específico por ID
        /// </summary>
        [HttpGet("{id}")]
        public async Task<ActionResult<object>> GetProveedor(int id)
        {
            try
            {
                var proveedor = await _context.Proveedores
                    .Include(p => p.InsumoProveedores)
                        .ThenInclude(ip => ip.Insumo)
                            .ThenInclude(i => i!.Clase)
                    .Where(p => p.IdProveedor == id)
                    .Select(p => new
                    {
                        p.IdProveedor,
                        p.Empresa,
                        p.Ruc,
                        p.Contacto,
                        p.Direccion,
                        p.CreatedAt,
                        p.UpdatedAt,
                        Insumos = p.InsumoProveedores.Select(ip => new
                        {
                            ip.Id,
                            ip.PrecioUnitario,
                            Insumo = new
                            {
                                ip.Insumo!.IdInsumo,
                                ip.Insumo.Nombre,
                                ip.Insumo.IdFox,
                                ip.Insumo.Presentacion,
                                Clase = ip.Insumo.Clase!.Familia
                            }
                        }).ToList()
                    })
                    .FirstOrDefaultAsync();

                if (proveedor == null)
                {
                    return NotFound("Proveedor no encontrado");
                }

                return Ok(proveedor);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener proveedor {Id}", id);
                return StatusCode(500, "Error interno del servidor");
            }
        }

        /// <summary>
        /// Crear un nuevo proveedor
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<Proveedor>> CreateProveedor([FromBody] ProveedorCreateDto proveedorDto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                // Verificar si ya existe un proveedor con el mismo RUC
                if (!string.IsNullOrEmpty(proveedorDto.Ruc))
                {
                    var existeRuc = await _context.Proveedores
                        .AnyAsync(p => p.Ruc == proveedorDto.Ruc);
                    
                    if (existeRuc)
                    {
                        return BadRequest("Ya existe un proveedor con ese RUC");
                    }
                }

                var proveedor = new Proveedor
                {
                    Empresa = proveedorDto.Empresa,
                    Ruc = proveedorDto.Ruc,
                    Contacto = proveedorDto.Contacto,
                    Direccion = proveedorDto.Direccion,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                };

                _context.Proveedores.Add(proveedor);
                await _context.SaveChangesAsync();

                return CreatedAtAction(nameof(GetProveedor), new { id = proveedor.IdProveedor }, proveedor);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al crear proveedor");
                return StatusCode(500, "Error interno del servidor");
            }
        }

        /// <summary>
        /// Actualizar un proveedor existente
        /// </summary>
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateProveedor(int id, [FromBody] ProveedorUpdateDto proveedorDto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var proveedor = await _context.Proveedores.FindAsync(id);
                if (proveedor == null)
                {
                    return NotFound("Proveedor no encontrado");
                }

                // Verificar RUC duplicado si se está cambiando
                if (!string.IsNullOrEmpty(proveedorDto.Ruc) && proveedorDto.Ruc != proveedor.Ruc)
                {
                    var existeRuc = await _context.Proveedores
                        .AnyAsync(p => p.Ruc == proveedorDto.Ruc && p.IdProveedor != id);
                    
                    if (existeRuc)
                    {
                        return BadRequest("Ya existe un proveedor con ese RUC");
                    }
                }

                // Actualizar propiedades
                proveedor.Empresa = proveedorDto.Empresa ?? proveedor.Empresa;
                proveedor.Ruc = proveedorDto.Ruc ?? proveedor.Ruc;
                proveedor.Contacto = proveedorDto.Contacto ?? proveedor.Contacto;
                proveedor.Direccion = proveedorDto.Direccion ?? proveedor.Direccion;
                proveedor.UpdatedAt = DateTime.Now;

                await _context.SaveChangesAsync();

                return Ok(proveedor);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al actualizar proveedor {Id}", id);
                return StatusCode(500, "Error interno del servidor");
            }
        }

        /// <summary>
        /// Eliminar un proveedor
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteProveedor(int id)
        {
            try
            {
                var proveedor = await _context.Proveedores.FindAsync(id);
                if (proveedor == null)
                {
                    return NotFound("Proveedor no encontrado");
                }

                // Verificar si tiene insumos asociados
                var tieneInsumos = await _context.InsumoProveedores
                    .AnyAsync(ip => ip.IdProveedor == id);

                if (tieneInsumos)
                {
                    return BadRequest("No se puede eliminar el proveedor porque tiene insumos asociados");
                }

                _context.Proveedores.Remove(proveedor);
                await _context.SaveChangesAsync();

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al eliminar proveedor {Id}", id);
                return StatusCode(500, "Error interno del servidor");
            }
        }

        /// <summary>
        /// Buscar proveedores por empresa, RUC o contacto
        /// </summary>
        [HttpGet("buscar")]
        public async Task<ActionResult<IEnumerable<object>>> BuscarProveedores([FromQuery] string termino)
        {
            try
            {
                if (string.IsNullOrEmpty(termino))
                {
                    return BadRequest("Debe proporcionar un término de búsqueda");
                }

                var proveedores = await _context.Proveedores
                    .Where(p => p.Empresa!.Contains(termino) || 
                              p.Ruc!.Contains(termino) ||
                              p.Contacto!.Contains(termino))
                    .Take(20)
                    .Select(p => new
                    {
                        p.IdProveedor,
                        p.Empresa,
                        p.Ruc,
                        p.Contacto
                    })
                    .ToListAsync();

                return Ok(proveedores);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al buscar proveedores");
                return StatusCode(500, "Error interno del servidor");
            }
        }

        /// <summary>
        /// Obtener estadísticas generales de proveedores
        /// </summary>
        [HttpGet("estadisticas")]
        public async Task<ActionResult<object>> GetEstadisticasProveedores()
        {
            try
            {
                var totalProveedores = await _context.Proveedores.CountAsync();
                var proveedoresConInsumos = await _context.Proveedores
                    .Where(p => p.InsumoProveedores.Any())
                    .CountAsync();
                
                var totalInsumosSuministrados = await _context.InsumoProveedores.CountAsync();
                var proveedoresRecientes = await _context.Proveedores
                    .Where(p => p.CreatedAt >= DateTime.Now.AddDays(-30))
                    .CountAsync();

                return Ok(new
                {
                    TotalProveedores = totalProveedores,
                    ProveedoresConInsumos = proveedoresConInsumos,
                    ProveedoresSinInsumos = totalProveedores - proveedoresConInsumos,
                    TotalInsumosSuministrados = totalInsumosSuministrados,
                    ProveedoresRecientes = proveedoresRecientes,
                    FechaConsulta = DateTime.Now
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener estadísticas de proveedores");
                return StatusCode(500, "Error interno del servidor");
            }
        }

        /// <summary>
        /// Obtener top proveedores por cantidad de insumos
        /// </summary>
        [HttpGet("top")]
        public async Task<ActionResult<IEnumerable<object>>> GetTopProveedores([FromQuery] int limite = 10)
        {
            try
            {
                var topProveedores = await _context.Proveedores
                    .Select(p => new
                    {
                        p.IdProveedor,
                        p.Empresa,
                        p.Ruc,
                        p.Contacto,
                        TotalInsumos = p.InsumoProveedores.Count(),
                        UltimaActividad = p.UpdatedAt
                    })
                    .Where(p => p.TotalInsumos > 0)
                    .OrderByDescending(p => p.TotalInsumos)
                    .Take(limite)
                    .ToListAsync();

                return Ok(topProveedores);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener top proveedores");
                return StatusCode(500, "Error interno del servidor");
            }
        }

        /// <summary>
        /// Asociar un insumo a un proveedor con precio
        /// </summary>
        [HttpPost("{id}/insumos")]
        public async Task<ActionResult> AsociarInsumo(int id, [FromBody] InsumoProveedorCreateDto dto)
        {
            try
            {
                // Verificar que el proveedor existe
                var proveedorExists = await _context.Proveedores.AnyAsync(p => p.IdProveedor == id);
                if (!proveedorExists)
                {
                    return NotFound("Proveedor no encontrado");
                }

                // Verificar que el insumo existe
                var insumoExists = await _context.Insumos.AnyAsync(i => i.IdInsumo == dto.IdInsumo);
                if (!insumoExists)
                {
                    return BadRequest("El insumo especificado no existe");
                }

                // Verificar si ya existe la asociación
                var existeAsociacion = await _context.InsumoProveedores
                    .AnyAsync(ip => ip.IdProveedor == id && ip.IdInsumo == dto.IdInsumo);

                if (existeAsociacion)
                {
                    return BadRequest("La asociación ya existe");
                }

                var insumoProveedor = new InsumoProveedor
                {
                    IdProveedor = id,
                    IdInsumo = dto.IdInsumo,
                    PrecioUnitario = dto.PrecioUnitario
                };

                _context.InsumoProveedores.Add(insumoProveedor);
                await _context.SaveChangesAsync();

                return Ok(insumoProveedor);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al asociar insumo al proveedor {Id}", id);
                return StatusCode(500, "Error interno del servidor");
            }
        }
    }

    // DTOs para Proveedores
    public class ProveedorCreateDto
    {
        public string? Empresa { get; set; }
        public string? Ruc { get; set; }
        public string? Contacto { get; set; }
        public string? Direccion { get; set; }
    }

    public class ProveedorUpdateDto
    {
        public string? Empresa { get; set; }
        public string? Ruc { get; set; }
        public string? Contacto { get; set; }
        public string? Direccion { get; set; }
    }

    public class InsumoProveedorCreateDto
    {
        public int IdInsumo { get; set; }
        public float? PrecioUnitario { get; set; }
    }
} 