using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TexfinaApi.Data;
using TexfinaApi.Models;
using TexfinaApi.DTOs;

namespace TexfinaApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class RolesController : ControllerBase
    {
        private readonly TexfinaDbContext _context;
        private readonly ILogger<RolesController> _logger;

        public RolesController(TexfinaDbContext context, ILogger<RolesController> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Obtener todos los roles
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<IEnumerable<object>>> GetRoles()
        {
            try
            {
                var roles = await _context.Roles
                    .Select(r => new
                    {
                        r.IdRol,
                        r.Nombre,
                        r.Descripcion,
                        TotalUsuarios = _context.Usuarios.Count(u => u.IdRol == r.IdRol && u.Activo == true),
                        TotalPermisos = _context.RolPermisos.Count(rp => rp.IdRol == r.IdRol)
                    })
                    .OrderBy(r => r.Nombre)
                    .ToListAsync();

                return Ok(roles);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener roles");
                return StatusCode(500, new { error = "Error interno del servidor", message = ex.Message });
            }
        }

        /// <summary>
        /// Obtener un rol específico con sus permisos
        /// </summary>
        [HttpGet("{idRol}")]
        public async Task<ActionResult<object>> GetRol(string idRol)
        {
            try
            {
                var rol = await _context.Roles
                    .Where(r => r.IdRol == idRol)
                    .Select(r => new
                    {
                        r.IdRol,
                        r.Nombre,
                        r.Descripcion,
                        TotalUsuarios = _context.Usuarios.Count(u => u.IdRol == r.IdRol && u.Activo == true),
                        Permisos = _context.RolPermisos
                            .Include(rp => rp.Permiso)
                            .Where(rp => rp.IdRol == r.IdRol)
                            .Select(rp => new
                            {
                                rp.Permiso!.IdPermiso,
                                rp.Permiso.Nombre,
                                rp.Permiso.Descripcion
                            })
                            .ToList()
                    })
                    .FirstOrDefaultAsync();

                if (rol == null)
                {
                    return NotFound(new { message = "Rol no encontrado" });
                }

                return Ok(rol);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener rol {IdRol}", idRol);
                return StatusCode(500, new { error = "Error interno del servidor", message = ex.Message });
            }
        }

        /// <summary>
        /// Crear un nuevo rol
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<object>> CreateRol(CreateRolDto createRolDto)
        {
            try
            {
                // Validar que el ID del rol no exista
                var existeRol = await _context.Roles
                    .AnyAsync(r => r.IdRol == createRolDto.IdRol);

                if (existeRol)
                {
                    return BadRequest(new { error = "El ID del rol ya existe" });
                }

                // Crear el rol
                var rol = new Rol
                {
                    IdRol = createRolDto.IdRol,
                    Nombre = createRolDto.Nombre,
                    Descripcion = createRolDto.Descripcion
                };

                _context.Roles.Add(rol);
                await _context.SaveChangesAsync();

                // Asignar permisos si se especificaron
                if (createRolDto.PermisosIds != null && createRolDto.PermisosIds.Any())
                {
                    var rolPermisos = createRolDto.PermisosIds.Select(permisoId => new RolPermiso
                    {
                        IdRol = rol.IdRol,
                        IdPermiso = permisoId
                    }).ToList();

                    _context.RolPermisos.AddRange(rolPermisos);
                    await _context.SaveChangesAsync();
                }

                // Retornar el rol creado con sus permisos
                var rolCreado = await GetRol(rol.IdRol);
                return CreatedAtAction(nameof(GetRol), new { idRol = rol.IdRol }, rolCreado.Value);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al crear rol");
                return StatusCode(500, new { error = "Error interno del servidor", message = ex.Message });
            }
        }

        /// <summary>
        /// Actualizar un rol existente
        /// </summary>
        [HttpPut("{idRol}")]
        public async Task<ActionResult<object>> UpdateRol(string idRol, UpdateRolDto updateRolDto)
        {
            try
            {
                var rol = await _context.Roles.FindAsync(idRol);
                if (rol == null)
                {
                    return NotFound(new { message = "Rol no encontrado" });
                }

                // Actualizar campos
                if (!string.IsNullOrEmpty(updateRolDto.Nombre))
                    rol.Nombre = updateRolDto.Nombre;

                if (!string.IsNullOrEmpty(updateRolDto.Descripcion))
                    rol.Descripcion = updateRolDto.Descripcion;

                await _context.SaveChangesAsync();

                // Actualizar permisos si se especificaron
                if (updateRolDto.PermisosIds != null)
                {
                    // Eliminar permisos actuales
                    var permisosActuales = await _context.RolPermisos
                        .Where(rp => rp.IdRol == idRol)
                        .ToListAsync();

                    _context.RolPermisos.RemoveRange(permisosActuales);

                    // Agregar nuevos permisos
                    var nuevosPermisos = updateRolDto.PermisosIds.Select(permisoId => new RolPermiso
                    {
                        IdRol = idRol,
                        IdPermiso = permisoId
                    }).ToList();

                    _context.RolPermisos.AddRange(nuevosPermisos);
                    await _context.SaveChangesAsync();
                }

                // Retornar el rol actualizado
                var rolActualizado = await GetRol(idRol);
                return rolActualizado;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al actualizar rol {IdRol}", idRol);
                return StatusCode(500, new { error = "Error interno del servidor", message = ex.Message });
            }
        }

        /// <summary>
        /// Eliminar un rol (solo si no tiene usuarios asignados)
        /// </summary>
        [HttpDelete("{idRol}")]
        public async Task<ActionResult> DeleteRol(string idRol)
        {
            try
            {
                var rol = await _context.Roles.FindAsync(idRol);
                if (rol == null)
                {
                    return NotFound(new { message = "Rol no encontrado" });
                }

                // Verificar que no tenga usuarios asignados
                var tieneUsuarios = await _context.Usuarios
                    .AnyAsync(u => u.IdRol == idRol);

                if (tieneUsuarios)
                {
                    return BadRequest(new { error = "No se puede eliminar un rol que tiene usuarios asignados" });
                }

                // Eliminar permisos del rol
                var permisos = await _context.RolPermisos
                    .Where(rp => rp.IdRol == idRol)
                    .ToListAsync();

                _context.RolPermisos.RemoveRange(permisos);

                // Eliminar el rol
                _context.Roles.Remove(rol);
                await _context.SaveChangesAsync();

                return Ok(new { message = "Rol eliminado exitosamente" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al eliminar rol {IdRol}", idRol);
                return StatusCode(500, new { error = "Error interno del servidor", message = ex.Message });
            }
        }

        /// <summary>
        /// Obtener todos los permisos disponibles
        /// </summary>
        [HttpGet("permisos")]
        public async Task<ActionResult<IEnumerable<object>>> GetPermisos()
        {
            try
            {
                var permisos = await _context.Permisos
                    .Select(p => new
                    {
                        p.IdPermiso,
                        p.Nombre,
                        p.Descripcion,
                        // Agrupar por categoría basado en el prefijo del nombre
                        Categoria = p.Nombre != null && p.Nombre.IndexOf("_") >= 0 ? p.Nombre.Substring(0, p.Nombre.IndexOf("_")) : "GENERAL"
                    })
                    .OrderBy(p => p.Categoria)
                    .ThenBy(p => p.Nombre)
                    .ToListAsync();

                // Agrupar permisos por categoría para mejor presentación en frontend
                var permisosAgrupados = permisos
                    .GroupBy(p => p.Categoria)
                    .Select(g => new
                    {
                        Categoria = g.Key,
                        Permisos = g.Select(p => new
                        {
                            p.IdPermiso,
                            p.Nombre,
                            p.Descripcion
                        }).ToList()
                    })
                    .ToList();

                return Ok(permisosAgrupados);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener permisos");
                return StatusCode(500, new { error = "Error interno del servidor", message = ex.Message });
            }
        }

        /// <summary>
        /// Obtener permisos de un rol específico
        /// </summary>
        [HttpGet("{idRol}/permisos")]
        public async Task<ActionResult<IEnumerable<object>>> GetPermisosRol(string idRol)
        {
            try
            {
                var permisos = await _context.RolPermisos
                    .Include(rp => rp.Permiso)
                    .Where(rp => rp.IdRol == idRol)
                    .Select(rp => new
                    {
                        rp.Permiso!.IdPermiso,
                        rp.Permiso.Nombre,
                        rp.Permiso.Descripcion
                    })
                    .OrderBy(p => p.Nombre)
                    .ToListAsync();

                return Ok(permisos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener permisos del rol {IdRol}", idRol);
                return StatusCode(500, new { error = "Error interno del servidor", message = ex.Message });
            }
        }

        /// <summary>
        /// Asignar permisos a un rol
        /// </summary>
        [HttpPost("{idRol}/permisos")]
        public async Task<ActionResult> AsignarPermisosRol(string idRol, AsignarPermisosDto asignarPermisosDto)
        {
            try
            {
                var rolExiste = await _context.Roles.AnyAsync(r => r.IdRol == idRol);
                if (!rolExiste)
                {
                    return NotFound(new { message = "Rol no encontrado" });
                }

                // Validar que todos los permisos existan
                var permisosValidos = await _context.Permisos
                    .Where(p => asignarPermisosDto.PermisosIds.Contains(p.IdPermiso))
                    .Select(p => p.IdPermiso)
                    .ToListAsync();

                var permisosInvalidos = asignarPermisosDto.PermisosIds.Except(permisosValidos).ToList();
                if (permisosInvalidos.Any())
                {
                    return BadRequest(new { 
                        error = "Permisos inválidos", 
                        permisosInvalidos = permisosInvalidos 
                    });
                }

                // Eliminar permisos actuales del rol
                var permisosActuales = await _context.RolPermisos
                    .Where(rp => rp.IdRol == idRol)
                    .ToListAsync();

                _context.RolPermisos.RemoveRange(permisosActuales);

                // Agregar nuevos permisos
                var nuevosPermisos = asignarPermisosDto.PermisosIds.Select(permisoId => new RolPermiso
                {
                    IdRol = idRol,
                    IdPermiso = permisoId
                }).ToList();

                _context.RolPermisos.AddRange(nuevosPermisos);
                await _context.SaveChangesAsync();

                return Ok(new { 
                    message = "Permisos asignados exitosamente",
                    totalPermisos = nuevosPermisos.Count
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al asignar permisos al rol {IdRol}", idRol);
                return StatusCode(500, new { error = "Error interno del servidor", message = ex.Message });
            }
        }

        /// <summary>
        /// Obtener estadísticas de roles
        /// </summary>
        [HttpGet("estadisticas")]
        public async Task<ActionResult<object>> GetEstadisticasRoles()
        {
            try
            {
                var totalRoles = await _context.Roles.CountAsync();
                var totalPermisos = await _context.Permisos.CountAsync();

                var rolesMasUsados = await _context.Usuarios
                    .Include(u => u.Rol)
                    .Where(u => u.Activo == true)
                    .GroupBy(u => new { u.IdRol, u.Rol!.Nombre })
                    .Select(g => new
                    {
                        Rol = g.Key.Nombre,
                        IdRol = g.Key.IdRol,
                        TotalUsuarios = g.Count()
                    })
                    .OrderByDescending(x => x.TotalUsuarios)
                    .ToListAsync();

                var rolesConPermisos = await _context.Roles
                    .Select(r => new
                    {
                        r.IdRol,
                        r.Nombre,
                        TotalPermisos = _context.RolPermisos.Count(rp => rp.IdRol == r.IdRol)
                    })
                    .OrderByDescending(x => x.TotalPermisos)
                    .ToListAsync();

                return Ok(new
                {
                    TotalRoles = totalRoles,
                    TotalPermisos = totalPermisos,
                    RolesMasUsados = rolesMasUsados,
                    RolesConPermisos = rolesConPermisos
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener estadísticas de roles");
                return StatusCode(500, new { error = "Error interno del servidor", message = ex.Message });
            }
        }
    }
}

// DTOs adicionales para RolesController
namespace TexfinaApi.DTOs
{
    public class CreateRolDto
    {
        public string IdRol { get; set; } = null!;
        public string Nombre { get; set; } = null!;
        public string? Descripcion { get; set; }
        public List<int>? PermisosIds { get; set; }
    }

    public class UpdateRolDto
    {
        public string? Nombre { get; set; }
        public string? Descripcion { get; set; }
        public List<int>? PermisosIds { get; set; }
    }

    public class AsignarPermisosDto
    {
        public List<int> PermisosIds { get; set; } = new List<int>();
    }
}