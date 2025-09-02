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
    public class UsuariosController : ControllerBase
    {
        private readonly TexfinaDbContext _context;
        private readonly ILogger<UsuariosController> _logger;

        public UsuariosController(TexfinaDbContext context, ILogger<UsuariosController> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Obtener todos los usuarios con filtros opcionales
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<IEnumerable<object>>> GetUsuarios(
            [FromQuery] string? buscar = null,
            [FromQuery] string? idRol = null,
            [FromQuery] bool? activo = null,
            [FromQuery] int pagina = 1,
            [FromQuery] int tamaño = 10)
        {
            try
            {
                var query = _context.Usuarios
                    .Include(u => u.Rol)
                    .Include(u => u.TipoUsuario)
                    .AsQueryable();

                // Filtros
                if (!string.IsNullOrEmpty(buscar))
                {
                    query = query.Where(u => u.Username!.Contains(buscar) || 
                                           u.Email!.Contains(buscar));
                }

                if (!string.IsNullOrEmpty(idRol))
                {
                    query = query.Where(u => u.IdRol == idRol);
                }

                if (activo.HasValue)
                {
                    query = query.Where(u => u.Activo == activo.Value);
                }

                // Paginación
                var total = await query.CountAsync();
                
                var usuarios = await query
                    .OrderByDescending(u => u.CreatedAt)
                    .Skip((pagina - 1) * tamaño)
                    .Take(tamaño)
                    .Select(u => new
                    {
                        u.IdUsuario,
                        u.Username,
                        u.Email,
                        u.Activo,
                        u.CreatedAt,
                        u.LastLogin,
                        Rol = new { 
                            IdRol = u.IdRol, 
                            Nombre = u.Rol!.Nombre,
                            Descripcion = u.Rol.Descripcion
                        },
                        TipoUsuario = new {
                            u.TipoUsuario!.IdTipoUsuario,
                            u.TipoUsuario.Descripcion,
                            u.TipoUsuario.RequiereCierreAutomatico
                        }
                    })
                    .ToListAsync();

                var respuesta = new
                {
                    Datos = usuarios,
                    TotalRegistros = total,
                    PaginaActual = pagina,
                    TotalPaginas = (int)Math.Ceiling(total / (double)tamaño),
                    TamañoPagina = tamaño
                };

                return Ok(respuesta);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener usuarios");
                return StatusCode(500, new { error = "Error interno del servidor", message = ex.Message });
            }
        }

        /// <summary>
        /// Obtener un usuario específico
        /// </summary>
        [HttpGet("{id}")]
        public async Task<ActionResult<object>> GetUsuario(int id)
        {
            try
            {
                var usuario = await _context.Usuarios
                    .Include(u => u.Rol)
                    .Include(u => u.TipoUsuario)
                    .Where(u => u.IdUsuario == id)
                    .Select(u => new
                    {
                        u.IdUsuario,
                        u.Username,
                        u.Email,
                        u.Activo,
                        u.CreatedAt,
                        u.LastLogin,
                        Rol = new { 
                            IdRol = u.IdRol, 
                            Nombre = u.Rol!.Nombre,
                            Descripcion = u.Rol.Descripcion
                        },
                        TipoUsuario = new {
                            u.TipoUsuario!.IdTipoUsuario,
                            u.TipoUsuario.Descripcion,
                            u.TipoUsuario.RequiereCierreAutomatico
                        },
                        // Incluir estadísticas del usuario
                        Estadisticas = new
                        {
                            TotalSesiones = _context.Sesiones.Count(s => s.IdUsuario == u.IdUsuario),
                            UltimaSesion = _context.Sesiones
                                .Where(s => s.IdUsuario == u.IdUsuario)
                                .OrderByDescending(s => s.Inicio)
                                .Select(s => s.Inicio)
                                .FirstOrDefault(),
                            TotalLogsEventos = _context.LogEventos.Count(l => l.IdUsuario == u.IdUsuario)
                        }
                    })
                    .FirstOrDefaultAsync();

                if (usuario == null)
                {
                    return NotFound(new { message = "Usuario no encontrado" });
                }

                return Ok(usuario);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener usuario {Id}", id);
                return StatusCode(500, new { error = "Error interno del servidor", message = ex.Message });
            }
        }

        /// <summary>
        /// Crear un nuevo usuario
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<object>> CreateUsuario(CreateUsuarioDto createUsuarioDto)
        {
            try
            {
                // Validar que el username no exista
                var existeUsername = await _context.Usuarios
                    .AnyAsync(u => u.Username == createUsuarioDto.Username);

                if (existeUsername)
                {
                    return BadRequest(new { error = "El nombre de usuario ya existe" });
                }

                // Validar que el email no exista
                if (!string.IsNullOrEmpty(createUsuarioDto.Email))
                {
                    var existeEmail = await _context.Usuarios
                        .AnyAsync(u => u.Email == createUsuarioDto.Email);

                    if (existeEmail)
                    {
                        return BadRequest(new { error = "El email ya está en uso" });
                    }
                }

                // Validar que el rol existe
                var rolExiste = await _context.Roles
                    .AnyAsync(r => r.IdRol == createUsuarioDto.IdRol);

                if (!rolExiste)
                {
                    return BadRequest(new { error = "El rol especificado no existe" });
                }

                // Crear el usuario
                var usuario = new Usuario
                {
                    Username = createUsuarioDto.Username,
                    Email = createUsuarioDto.Email,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(createUsuarioDto.Password),
                    IdRol = createUsuarioDto.IdRol,
                    IdTipoUsuario = createUsuarioDto.IdTipoUsuario ?? 1, // Usuario Regular por defecto
                    Activo = createUsuarioDto.Activo ?? true,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Usuarios.Add(usuario);
                await _context.SaveChangesAsync();

                // Obtener el usuario creado con sus relaciones
                var usuarioCreado = await _context.Usuarios
                    .Include(u => u.Rol)
                    .Include(u => u.TipoUsuario)
                    .Where(u => u.IdUsuario == usuario.IdUsuario)
                    .Select(u => new
                    {
                        u.IdUsuario,
                        u.Username,
                        u.Email,
                        u.Activo,
                        u.CreatedAt,
                        Rol = new { 
                            IdRol = u.IdRol, 
                            Nombre = u.Rol!.Nombre,
                            Descripcion = u.Rol.Descripcion
                        },
                        TipoUsuario = new {
                            u.TipoUsuario!.IdTipoUsuario,
                            u.TipoUsuario.Descripcion
                        }
                    })
                    .FirstOrDefaultAsync();

                return CreatedAtAction(
                    nameof(GetUsuario), 
                    new { id = usuario.IdUsuario }, 
                    usuarioCreado);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al crear usuario");
                return StatusCode(500, new { error = "Error interno del servidor", message = ex.Message });
            }
        }

        /// <summary>
        /// Actualizar un usuario existente
        /// </summary>
        [HttpPut("{id}")]
        public async Task<ActionResult<object>> UpdateUsuario(int id, UpdateUsuarioDto updateUsuarioDto)
        {
            try
            {
                var usuario = await _context.Usuarios.FindAsync(id);
                if (usuario == null)
                {
                    return NotFound(new { message = "Usuario no encontrado" });
                }

                // Validar username único (excluyendo el usuario actual)
                if (!string.IsNullOrEmpty(updateUsuarioDto.Username) && 
                    updateUsuarioDto.Username != usuario.Username)
                {
                    var existeUsername = await _context.Usuarios
                        .AnyAsync(u => u.Username == updateUsuarioDto.Username && u.IdUsuario != id);

                    if (existeUsername)
                    {
                        return BadRequest(new { error = "El nombre de usuario ya existe" });
                    }
                }

                // Validar email único (excluyendo el usuario actual)
                if (!string.IsNullOrEmpty(updateUsuarioDto.Email) && 
                    updateUsuarioDto.Email != usuario.Email)
                {
                    var existeEmail = await _context.Usuarios
                        .AnyAsync(u => u.Email == updateUsuarioDto.Email && u.IdUsuario != id);

                    if (existeEmail)
                    {
                        return BadRequest(new { error = "El email ya está en uso" });
                    }
                }

                // Actualizar campos
                if (!string.IsNullOrEmpty(updateUsuarioDto.Username))
                    usuario.Username = updateUsuarioDto.Username;

                if (!string.IsNullOrEmpty(updateUsuarioDto.Email))
                    usuario.Email = updateUsuarioDto.Email;

                if (!string.IsNullOrEmpty(updateUsuarioDto.Password))
                    usuario.PasswordHash = BCrypt.Net.BCrypt.HashPassword(updateUsuarioDto.Password);

                if (!string.IsNullOrEmpty(updateUsuarioDto.IdRol))
                    usuario.IdRol = updateUsuarioDto.IdRol;

                if (updateUsuarioDto.IdTipoUsuario.HasValue)
                    usuario.IdTipoUsuario = updateUsuarioDto.IdTipoUsuario.Value;

                if (updateUsuarioDto.Activo.HasValue)
                    usuario.Activo = updateUsuarioDto.Activo.Value;

                await _context.SaveChangesAsync();

                // Retornar el usuario actualizado
                var usuarioActualizado = await GetUsuario(id);
                return usuarioActualizado;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al actualizar usuario {Id}", id);
                return StatusCode(500, new { error = "Error interno del servidor", message = ex.Message });
            }
        }

        /// <summary>
        /// Eliminar un usuario (soft delete)
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<ActionResult> DeleteUsuario(int id)
        {
            try
            {
                var usuario = await _context.Usuarios.FindAsync(id);
                if (usuario == null)
                {
                    return NotFound(new { message = "Usuario no encontrado" });
                }

                // Verificar que no sea el único administrador
                if (usuario.IdRol == "ADMIN")
                {
                    var totalAdmins = await _context.Usuarios
                        .CountAsync(u => u.IdRol == "ADMIN" && u.Activo == true);

                    if (totalAdmins <= 1)
                    {
                        return BadRequest(new { error = "No se puede eliminar el único administrador del sistema" });
                    }
                }

                // Soft delete - marcar como inactivo
                usuario.Activo = false;
                await _context.SaveChangesAsync();

                return Ok(new { message = "Usuario desactivado exitosamente" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al eliminar usuario {Id}", id);
                return StatusCode(500, new { error = "Error interno del servidor", message = ex.Message });
            }
        }

        /// <summary>
        /// Obtener estadísticas de usuarios
        /// </summary>
        [HttpGet("estadisticas")]
        public async Task<ActionResult<object>> GetEstadisticasUsuarios()
        {
            try
            {
                var stats = await _context.Usuarios
                    .GroupBy(x => 1)
                    .Select(g => new
                    {
                        TotalUsuarios = g.Count(),
                        UsuariosActivos = g.Count(u => u.Activo == true),
                        UsuariosInactivos = g.Count(u => u.Activo == false)
                    })
                    .FirstOrDefaultAsync();

                var usuariosPorRol = await _context.Usuarios
                    .Include(u => u.Rol)
                    .Where(u => u.Activo == true)
                    .GroupBy(u => new { u.IdRol, u.Rol!.Nombre })
                    .Select(g => new
                    {
                        Rol = g.Key.Nombre,
                        Cantidad = g.Count()
                    })
                    .OrderByDescending(x => x.Cantidad)
                    .ToListAsync();

                var ultimosLogins = await _context.Usuarios
                    .Where(u => u.Activo == true && u.LastLogin.HasValue)
                    .OrderByDescending(u => u.LastLogin)
                    .Take(10)
                    .Select(u => new
                    {
                        u.Username,
                        u.LastLogin,
                        Rol = u.Rol!.Nombre
                    })
                    .ToListAsync();

                return Ok(new
                {
                    EstadisticasGenerales = stats ?? new { TotalUsuarios = 0, UsuariosActivos = 0, UsuariosInactivos = 0 },
                    UsuariosPorRol = usuariosPorRol,
                    UltimosLogins = ultimosLogins
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener estadísticas de usuarios");
                return StatusCode(500, new { error = "Error interno del servidor", message = ex.Message });
            }
        }

        /// <summary>
        /// Cambiar estado de activación de usuario
        /// </summary>
        [HttpPatch("{id}/toggle-status")]
        public async Task<ActionResult> ToggleUsuarioStatus(int id)
        {
            try
            {
                var usuario = await _context.Usuarios.FindAsync(id);
                if (usuario == null)
                {
                    return NotFound(new { message = "Usuario no encontrado" });
                }

                // Si se está desactivando un admin, verificar que no sea el único
                if (usuario.Activo == true && usuario.IdRol == "ADMIN")
                {
                    var totalAdmins = await _context.Usuarios
                        .CountAsync(u => u.IdRol == "ADMIN" && u.Activo == true);

                    if (totalAdmins <= 1)
                    {
                        return BadRequest(new { error = "No se puede desactivar el único administrador del sistema" });
                    }
                }

                usuario.Activo = !usuario.Activo;
                await _context.SaveChangesAsync();

                return Ok(new { 
                    message = usuario.Activo == true ? "Usuario activado exitosamente" : "Usuario desactivado exitosamente",
                    activo = usuario.Activo
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cambiar estado de usuario {Id}", id);
                return StatusCode(500, new { error = "Error interno del servidor", message = ex.Message });
            }
        }
    }
}

// DTOs para el UsuariosController
namespace TexfinaApi.DTOs
{
    public class CreateUsuarioDto
    {
        public string Username { get; set; } = null!;
        public string? Email { get; set; }
        public string Password { get; set; } = null!;
        public string IdRol { get; set; } = null!;
        public int? IdTipoUsuario { get; set; }
        public bool? Activo { get; set; }
    }

    public class UpdateUsuarioDto
    {
        public string? Username { get; set; }
        public string? Email { get; set; }
        public string? Password { get; set; }
        public string? IdRol { get; set; }
        public int? IdTipoUsuario { get; set; }
        public bool? Activo { get; set; }
    }
}