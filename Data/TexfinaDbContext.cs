using Microsoft.EntityFrameworkCore;
using TexfinaApi.Models;

namespace TexfinaApi.Data
{
    public class TexfinaDbContext : DbContext
    {
        public TexfinaDbContext(DbContextOptions<TexfinaDbContext> options) : base(options)
        {
        }

        // DbSets principales
        public DbSet<Usuario> Usuarios { get; set; }
        public DbSet<Rol> Roles { get; set; }
        public DbSet<TipoUsuario> TiposUsuario { get; set; }
        public DbSet<Permiso> Permisos { get; set; }
        public DbSet<RolPermiso> RolPermisos { get; set; }
        public DbSet<Sesion> Sesiones { get; set; }
        public DbSet<LogEvento> LogEventos { get; set; }
        
        // DbSets de insumos
        public DbSet<Insumo> Insumos { get; set; }
        public DbSet<Clase> Clases { get; set; }
        public DbSet<Unidad> Unidades { get; set; }
        public DbSet<Proveedor> Proveedores { get; set; }
        public DbSet<InsumoProveedor> InsumoProveedores { get; set; }
        public DbSet<Lote> Lotes { get; set; }
        public DbSet<Almacen> Almacenes { get; set; }
        public DbSet<Stock> Stocks { get; set; }
        public DbSet<Ingreso> Ingresos { get; set; }
        public DbSet<Consumo> Consumos { get; set; }
        public DbSet<Receta> Recetas { get; set; }
        public DbSet<RecetaDetalle> RecetaDetalles { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configuraciones específicas de relaciones
            
            // Usuario - Rol (uno a muchos)
            modelBuilder.Entity<Usuario>()
                .HasOne(u => u.Rol)
                .WithMany(r => r.Usuarios)
                .HasForeignKey(u => u.IdRol)
                .OnDelete(DeleteBehavior.SetNull);

            // Usuario - TipoUsuario (uno a muchos)
            modelBuilder.Entity<Usuario>()
                .HasOne(u => u.TipoUsuario)
                .WithMany(t => t.Usuarios)
                .HasForeignKey(u => u.IdTipoUsuario)
                .OnDelete(DeleteBehavior.SetNull);

            // RolPermiso - Configuración de claves foráneas
            modelBuilder.Entity<RolPermiso>()
                .HasOne(rp => rp.Rol)
                .WithMany(r => r.RolPermisos)
                .HasForeignKey(rp => rp.IdRol)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<RolPermiso>()
                .HasOne(rp => rp.Permiso)
                .WithMany(p => p.RolPermisos)
                .HasForeignKey(rp => rp.IdPermiso)
                .OnDelete(DeleteBehavior.Cascade);

            // Sesion - Usuario
            modelBuilder.Entity<Sesion>()
                .HasOne(s => s.Usuario)
                .WithMany(u => u.Sesiones)
                .HasForeignKey(s => s.IdUsuario)
                .OnDelete(DeleteBehavior.Cascade);

            // LogEvento - Usuario
            modelBuilder.Entity<LogEvento>()
                .HasOne(l => l.Usuario)
                .WithMany(u => u.LogEventos)
                .HasForeignKey(l => l.IdUsuario)
                .OnDelete(DeleteBehavior.SetNull);

            // Insumo - Clase
            modelBuilder.Entity<Insumo>()
                .HasOne(i => i.Clase)
                .WithMany(c => c.Insumos)
                .HasForeignKey(i => i.IdClase)
                .OnDelete(DeleteBehavior.SetNull);

            // Insumo - Unidad
            modelBuilder.Entity<Insumo>()
                .HasOne(i => i.Unidad)
                .WithMany(u => u.Insumos)
                .HasForeignKey(i => i.IdUnidad)
                .OnDelete(DeleteBehavior.SetNull);

            // **CONFIGURACIONES FALTANTES - LOTES Y RELACIONADOS**
            
            // CONFIGURACIONES DE LOTE RESTAURADAS
            // Lote - Insumo
            modelBuilder.Entity<Lote>()
                .HasOne(l => l.Insumo)
                .WithMany(i => i.Lotes)
                .HasForeignKey(l => l.IdInsumo)
                .OnDelete(DeleteBehavior.SetNull);

            // Stock - Insumo
            modelBuilder.Entity<Stock>()
                .HasOne(s => s.Insumo)
                .WithMany(i => i.Stocks)
                .HasForeignKey(s => s.IdInsumo)
                .OnDelete(DeleteBehavior.SetNull);

            // Stock - Lote (RESTAURADO)
            modelBuilder.Entity<Stock>()
                .HasOne(s => s.Lote)
                .WithMany(l => l.Stocks)
                .HasForeignKey(s => s.IdLote)
                .OnDelete(DeleteBehavior.SetNull);

            // Stock - Almacen
            modelBuilder.Entity<Stock>()
                .HasOne(s => s.Almacen)
                .WithMany(a => a.Stocks)
                .HasForeignKey(s => s.IdAlmacen)
                .OnDelete(DeleteBehavior.SetNull);

            // Stock - Unidad
            modelBuilder.Entity<Stock>()
                .HasOne(s => s.Unidad)
                .WithMany(u => u.Stocks)
                .HasForeignKey(s => s.IdUnidad)
                .OnDelete(DeleteBehavior.SetNull);

            // Ingreso - Insumo
            modelBuilder.Entity<Ingreso>()
                .HasOne(i => i.Insumo)
                .WithMany(ins => ins.Ingresos)
                .HasForeignKey(i => i.IdInsumo)
                .OnDelete(DeleteBehavior.SetNull);

            // Ingreso - Lote
            modelBuilder.Entity<Ingreso>()
                .HasOne(i => i.Lote)
                .WithMany(l => l.Ingresos)
                .HasForeignKey(i => i.IdLote)
                .OnDelete(DeleteBehavior.SetNull);

            // Ingreso - Unidad
            modelBuilder.Entity<Ingreso>()
                .HasOne(i => i.Unidad)
                .WithMany(u => u.Ingresos)
                .HasForeignKey(i => i.IdUnidad)
                .OnDelete(DeleteBehavior.SetNull);

            // Ingreso - InsumoProveedor
            modelBuilder.Entity<Ingreso>()
                .HasOne(i => i.InsumoProveedor)
                .WithMany(ip => ip.Ingresos)
                .HasForeignKey(i => i.IdInsumoProveedor)
                .OnDelete(DeleteBehavior.SetNull);

            // Consumo - Insumo
            modelBuilder.Entity<Consumo>()
                .HasOne(c => c.Insumo)
                .WithMany(i => i.Consumos)
                .HasForeignKey(c => c.IdInsumo)
                .OnDelete(DeleteBehavior.SetNull);

            // Consumo - Lote
            modelBuilder.Entity<Consumo>()
                .HasOne(c => c.Lote)
                .WithMany(l => l.Consumos)
                .HasForeignKey(c => c.IdLote)
                .OnDelete(DeleteBehavior.SetNull);

            // InsumoProveedor - Insumo
            modelBuilder.Entity<InsumoProveedor>()
                .HasOne(ip => ip.Insumo)
                .WithMany(i => i.InsumoProveedores)
                .HasForeignKey(ip => ip.IdInsumo)
                .OnDelete(DeleteBehavior.Cascade);

            // InsumoProveedor - Proveedor
            modelBuilder.Entity<InsumoProveedor>()
                .HasOne(ip => ip.Proveedor)
                .WithMany(p => p.InsumoProveedores)
                .HasForeignKey(ip => ip.IdProveedor)
                .OnDelete(DeleteBehavior.Cascade);

            // RecetaDetalle - Receta
            modelBuilder.Entity<RecetaDetalle>()
                .HasOne(rd => rd.Receta)
                .WithMany(r => r.RecetaDetalles)
                .HasForeignKey(rd => rd.IdReceta)
                .OnDelete(DeleteBehavior.Cascade);

            // RecetaDetalle - Insumo
            modelBuilder.Entity<RecetaDetalle>()
                .HasOne(rd => rd.Insumo)
                .WithMany(i => i.RecetaDetalles)
                .HasForeignKey(rd => rd.IdInsumo)
                .OnDelete(DeleteBehavior.SetNull);

            // Índices únicos
            modelBuilder.Entity<Usuario>()
                .HasIndex(u => u.Username)
                .IsUnique();

            // Configuraciones de decimal para precios
            modelBuilder.Entity<Insumo>()
                .Property(i => i.PrecioUnitario)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<Insumo>()
                .Property(i => i.PesoUnitario)
                .HasColumnType("decimal(18,4)");

            // Configuraciones de tipos de datos para compatibilidad SQL Server
            modelBuilder.Entity<Lote>()
                .Property(l => l.StockInicial)
                .HasColumnType("real");

            modelBuilder.Entity<Lote>()
                .Property(l => l.StockActual)
                .HasColumnType("real");

            modelBuilder.Entity<Lote>()
                .Property(l => l.PrecioTotal)
                .HasColumnType("real");
        }
    }
} 