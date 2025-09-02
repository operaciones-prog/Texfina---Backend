-- SCRIPT SQL SERVER - MODELO DE INSUMOS TEXFINA
-- Fecha de generación: 2025-05-29 15:03:56
-- Adaptado para SQL Server con INT IDENTITY

-- Crear la base de datos
CREATE DATABASE TexfinaDB;
GO

USE TexfinaDB;
GO

-- Tabla CLASE
CREATE TABLE CLASE (
    id_clase NVARCHAR(50) PRIMARY KEY,
    familia NVARCHAR(100),
    sub_familia NVARCHAR(100)
);

-- Tabla UNIDAD
CREATE TABLE UNIDAD (
    id_unidad NVARCHAR(50) PRIMARY KEY,
    nombre NVARCHAR(100)
);

-- Tabla ROL
CREATE TABLE ROL (
    id_rol NVARCHAR(50) PRIMARY KEY,
    nombre NVARCHAR(100),
    descripcion NVARCHAR(200)
);

-- Tabla TIPO_USUARIO
CREATE TABLE TIPO_USUARIO (
    id_tipo_usuario INT IDENTITY(1,1) PRIMARY KEY,
    descripcion NVARCHAR(100),
    requiere_cierre_automatico BIT
);

-- Tabla USUARIO
CREATE TABLE USUARIO (
    id_usuario INT IDENTITY(1,1) PRIMARY KEY,
    username NVARCHAR(50) UNIQUE NOT NULL,
    email NVARCHAR(100),
    password_hash NVARCHAR(255),
    id_rol NVARCHAR(50) REFERENCES ROL(id_rol),
    id_tipo_usuario INT REFERENCES TIPO_USUARIO(id_tipo_usuario),
    activo BIT DEFAULT 1,
    created_at DATETIME2 DEFAULT GETDATE(),
    last_login DATETIME2
);

-- Tabla PERMISO
CREATE TABLE PERMISO (
    id_permiso INT IDENTITY(1,1) PRIMARY KEY,
    nombre NVARCHAR(100),
    descripcion NVARCHAR(200)
);

-- Tabla ROL_PERMISO
CREATE TABLE ROL_PERMISO (
    id INT IDENTITY(1,1) PRIMARY KEY,
    id_rol NVARCHAR(50) REFERENCES ROL(id_rol),
    id_permiso INT REFERENCES PERMISO(id_permiso)
);

-- Tabla SESION (mantiene UNIQUEIDENTIFIER para seguridad)
CREATE TABLE SESION (
    id_sesion UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    id_usuario INT REFERENCES USUARIO(id_usuario),
    inicio DATETIME2,
    fin DATETIME2,
    cerrada_automaticamente BIT
);

-- Tabla LOG_EVENTO
CREATE TABLE LOG_EVENTO (
    id INT IDENTITY(1,1) PRIMARY KEY,
    id_usuario INT REFERENCES USUARIO(id_usuario),
    accion NVARCHAR(100),
    descripcion NVARCHAR(500),
    ip_origen NVARCHAR(50),
    modulo NVARCHAR(100),
    tabla_afectada NVARCHAR(100),
    timestamp DATETIME2 DEFAULT GETDATE()
);

-- Tabla PROVEEDOR
CREATE TABLE PROVEEDOR (
    id_proveedor INT IDENTITY(1,1) PRIMARY KEY,
    empresa NVARCHAR(200),
    ruc NVARCHAR(20),
    contacto NVARCHAR(200),
    direccion NVARCHAR(500),
    created_at DATETIME2 DEFAULT GETDATE(),
    updated_at DATETIME2 DEFAULT GETDATE()
);

-- Tabla INSUMO
CREATE TABLE INSUMO (
    id_insumo INT IDENTITY(1,1) PRIMARY KEY,
    id_fox NVARCHAR(50),
    nombre NVARCHAR(200),
    id_clase NVARCHAR(50) REFERENCES CLASE(id_clase),
    peso_unitario FLOAT,
    id_unidad NVARCHAR(50) REFERENCES UNIDAD(id_unidad),
    presentacion NVARCHAR(100),
    precio_unitario FLOAT,
    created_at DATETIME2 DEFAULT GETDATE(),
    updated_at DATETIME2 DEFAULT GETDATE()
);

-- Tabla INSUMO_PROVEEDOR
CREATE TABLE INSUMO_PROVEEDOR (
    id INT IDENTITY(1,1) PRIMARY KEY,
    id_insumo INT REFERENCES INSUMO(id_insumo),
    id_proveedor INT REFERENCES PROVEEDOR(id_proveedor),
    precio_unitario FLOAT
);

-- Tabla LOTE
CREATE TABLE LOTE (
    id_lote INT IDENTITY(1,1) PRIMARY KEY,
    id_insumo INT REFERENCES INSUMO(id_insumo),
    lote NVARCHAR(100),
    ubicacion NVARCHAR(200),
    stock_inicial FLOAT,
    stock_actual FLOAT,
    fecha_expiracion DATE,
    precio_total FLOAT,
    estado_lote NVARCHAR(50)
);

-- Tabla ALMACEN
CREATE TABLE ALMACEN (
    id_almacen INT IDENTITY(1,1) PRIMARY KEY,
    nombre NVARCHAR(100),
    ubicacion NVARCHAR(200)
);

-- Tabla STOCK
CREATE TABLE STOCK (
    id_stock INT IDENTITY(1,1) PRIMARY KEY,
    id_insumo INT REFERENCES INSUMO(id_insumo),
    presentacion NVARCHAR(100),
    id_unidad NVARCHAR(50) REFERENCES UNIDAD(id_unidad),
    cantidad FLOAT,
    id_lote INT REFERENCES LOTE(id_lote),
    id_almacen INT REFERENCES ALMACEN(id_almacen),
    fecha_entrada DATETIME2,
    fecha_salida DATETIME2
);

-- Tabla INGRESO
CREATE TABLE INGRESO (
    id_ingreso INT IDENTITY(1,1) PRIMARY KEY,
    id_insumo INT REFERENCES INSUMO(id_insumo),
    id_insumo_proveedor INT REFERENCES INSUMO_PROVEEDOR(id),
    fecha DATE,
    presentacion NVARCHAR(100),
    id_unidad NVARCHAR(50) REFERENCES UNIDAD(id_unidad),
    cantidad FLOAT,
    id_lote INT REFERENCES LOTE(id_lote),
    precio_total_formula FLOAT,
    precio_unitario_historico FLOAT,
    numero_remision NVARCHAR(50),
    orden_compra NVARCHAR(50),
    estado NVARCHAR(50)
);

-- Tabla CONSUMO
CREATE TABLE CONSUMO (
    id_consumo INT IDENTITY(1,1) PRIMARY KEY,
    id_insumo INT REFERENCES INSUMO(id_insumo),
    area NVARCHAR(100),
    fecha DATE,
    cantidad FLOAT,
    id_lote INT REFERENCES LOTE(id_lote),
    estado NVARCHAR(50)
);

-- Tabla RECETA
CREATE TABLE RECETA (
    id_receta INT IDENTITY(1,1) PRIMARY KEY,
    nombre NVARCHAR(200)
);

-- Tabla RECETA_DETALLE
CREATE TABLE RECETA_DETALLE (
    id INT IDENTITY(1,1) PRIMARY KEY,
    id_receta INT REFERENCES RECETA(id_receta),
    id_insumo INT REFERENCES INSUMO(id_insumo),
    proporcion FLOAT,
    orden INT,
    tipo_medida NVARCHAR(50)
);

-- DATOS INICIALES

-- Insertar roles básicos
INSERT INTO ROL (id_rol, nombre, descripcion) VALUES 
('ADMIN', 'Administrador', 'Acceso completo al sistema'),
('SUPERVISOR', 'Supervisor', 'Supervisión de operaciones'),
('OPERARIO', 'Operario', 'Operaciones básicas de insumos'),
('CONSULTOR', 'Consultor', 'Solo consulta de información'),
('LABORATORISTA', 'Laboratorista', 'Aprobación de lotes y calidad'),
('COMERCIAL', 'Comercial', 'Gestión de precios y cotizaciones');

-- Insertar tipos de usuario
INSERT INTO TIPO_USUARIO (descripcion, requiere_cierre_automatico) VALUES 
('Usuario Regular', 0),
('Usuario Temporal', 1),
('Usuario Externo', 1);

-- Insertar permisos básicos
INSERT INTO PERMISO (nombre, descripcion) VALUES 
('USUARIOS_CREAR', 'Crear usuarios'),
('USUARIOS_EDITAR', 'Editar usuarios'),
('USUARIOS_ELIMINAR', 'Eliminar usuarios'),
('USUARIOS_CONSULTAR', 'Consultar usuarios'),
('INSUMOS_CREAR', 'Crear insumos'),
('INSUMOS_EDITAR', 'Editar insumos'),
('INSUMOS_ELIMINAR', 'Eliminar insumos'),
('INSUMOS_CONSULTAR', 'Consultar insumos'),
('STOCK_CONSULTAR', 'Consultar stock'),
('STOCK_GESTIONAR', 'Gestionar stock'),
('REPORTES_GENERAR', 'Generar reportes'),
('CONFIGURACION', 'Configuración del sistema'),
('PROVEEDORES_CREAR', 'Crear proveedores'),
('PROVEEDORES_EDITAR', 'Editar proveedores'),
('PROVEEDORES_ELIMINAR', 'Eliminar proveedores'),
('PROVEEDORES_CONSULTAR', 'Consultar proveedores'),
('LOTES_CREAR', 'Crear lotes'),
('LOTES_EDITAR', 'Editar lotes'),
('LOTES_ELIMINAR', 'Eliminar lotes'),
('LOTES_CONSULTAR', 'Consultar lotes'),
('LOTES_APROBAR', 'Aprobar lotes'),
('PRECIOS_ASIGNAR', 'Asignar precios a lotes'),
('INGRESOS_CREAR', 'Registrar ingresos'),
('INGRESOS_EDITAR', 'Editar ingresos'),
('INGRESOS_CONSULTAR', 'Consultar ingresos'),
('CONSUMOS_CREAR', 'Registrar consumos'),
('CONSUMOS_EDITAR', 'Editar consumos'),
('CONSUMOS_CONSULTAR', 'Consultar consumos'),
('RECETAS_CREAR', 'Crear recetas'),
('RECETAS_EDITAR', 'Editar recetas'),
('RECETAS_ELIMINAR', 'Eliminar recetas'),
('RECETAS_CONSULTAR', 'Consultar recetas'),
('ALMACENES_CREAR', 'Crear almacenes'),
('ALMACENES_EDITAR', 'Editar almacenes'),
('ALMACENES_ELIMINAR', 'Eliminar almacenes'),
('ALMACENES_CONSULTAR', 'Consultar almacenes');

-- Asignar todos los permisos al rol ADMIN
INSERT INTO ROL_PERMISO (id_rol, id_permiso)
SELECT 'ADMIN', id_permiso FROM PERMISO;

-- Asignar permisos limitados al rol SUPERVISOR
INSERT INTO ROL_PERMISO (id_rol, id_permiso)
SELECT 'SUPERVISOR', id_permiso 
FROM PERMISO 
WHERE nombre NOT IN ('USUARIOS_CREAR', 'USUARIOS_ELIMINAR', 'CONFIGURACION');

-- Asignar permisos específicos al rol LABORATORISTA
INSERT INTO ROL_PERMISO (id_rol, id_permiso)
SELECT 'LABORATORISTA', id_permiso 
FROM PERMISO 
WHERE nombre IN (
    'LOTES_CONSULTAR', 'LOTES_APROBAR', 'INSUMOS_CONSULTAR', 
    'STOCK_CONSULTAR', 'REPORTES_GENERAR'
);

-- Asignar permisos específicos al rol COMERCIAL
INSERT INTO ROL_PERMISO (id_rol, id_permiso)
SELECT 'COMERCIAL', id_permiso 
FROM PERMISO 
WHERE nombre IN (
    'LOTES_CONSULTAR', 'PRECIOS_ASIGNAR', 'PROVEEDORES_CONSULTAR',
    'INSUMOS_CONSULTAR', 'REPORTES_GENERAR'
);

-- Asignar permisos básicos al rol OPERARIO
INSERT INTO ROL_PERMISO (id_rol, id_permiso)
SELECT 'OPERARIO', id_permiso 
FROM PERMISO 
WHERE nombre IN (
    'INSUMOS_CONSULTAR', 'STOCK_CONSULTAR', 'STOCK_GESTIONAR',
    'PROVEEDORES_CONSULTAR', 'LOTES_CONSULTAR', 'LOTES_CREAR', 'LOTES_EDITAR',
    'INGRESOS_CREAR', 'INGRESOS_CONSULTAR', 'CONSUMOS_CREAR', 'CONSUMOS_CONSULTAR',
    'ALMACENES_CONSULTAR'
);

-- Asignar solo permisos de consulta al rol CONSULTOR
INSERT INTO ROL_PERMISO (id_rol, id_permiso)
SELECT 'CONSULTOR', id_permiso 
FROM PERMISO 
WHERE nombre LIKE '%_CONSULTAR';

-- Insertar unidades básicas
INSERT INTO UNIDAD (id_unidad, nombre) VALUES 
('KG', 'Kilogramo'),
('LT', 'Litro'),
('UN', 'Unidad'),
('MT', 'Metro'),
('GR', 'Gramo'),
('ML', 'Mililitro'),
('CM', 'Centímetro'),
('M2', 'Metro Cuadrado'),
('M3', 'Metro Cúbico'),
('TON', 'Tonelada');

-- Insertar clases básicas
INSERT INTO CLASE (id_clase, familia, sub_familia) VALUES 
('QUIM', 'Químicos', 'Químicos Básicos'),
('QUIM_ACE', 'Químicos', 'Aceites y Lubricantes'),
('QUIM_SOL', 'Químicos', 'Solventes'),
('MAT_PRIMA', 'Materia Prima', 'Materia Prima Textil'),
('MAT_FIB', 'Materia Prima', 'Fibras'),
('MAT_HIL', 'Materia Prima', 'Hilos'),
('TINTAS', 'Tintas', 'Tintas de Impresión'),
('TINTAS_PIG', 'Tintas', 'Pigmentos'),
('ACAB', 'Acabados', 'Acabados Textiles'),
('ACAB_SOFT', 'Acabados', 'Suavizantes'),
('ACAB_RIG', 'Acabados', 'Rigidizantes'),
('MANT', 'Mantenimiento', 'Repuestos'),
('MANT_HER', 'Mantenimiento', 'Herramientas'),
('ENVASE', 'Envases', 'Contenedores'),
('ENVASE_BOL', 'Envases', 'Bolsas');

-- Insertar almacenes básicos
INSERT INTO ALMACEN (nombre, ubicacion) VALUES 
('Almacén Principal', 'Planta - Área A'),
('Almacén de Químicos', 'Planta - Área B'),
('Almacén de Tintas', 'Planta - Área C'),
('Almacén de Materia Prima', 'Planta - Área D'),
('Almacén de Acabados', 'Planta - Área E');

-- ========================================
-- SISTEMA DE AUTOMATIZACIÓN DE STOCK
-- ========================================

-- Tabla para tareas pendientes por rol
CREATE TABLE TAREA_PENDIENTE (
    id_tarea INT IDENTITY(1,1) PRIMARY KEY,
    tipo_tarea NVARCHAR(50) NOT NULL, -- 'APROBAR_LOTE', 'ASIGNAR_PRECIO'
    id_entidad INT NOT NULL, -- ID del lote, insumo, etc.
    tabla_entidad NVARCHAR(50) NOT NULL, -- 'LOTE', 'INSUMO'
    id_rol_asignado NVARCHAR(50) REFERENCES ROL(id_rol),
    estado NVARCHAR(50) DEFAULT 'PENDIENTE', -- 'PENDIENTE', 'COMPLETADA', 'CANCELADA'
    prioridad INT DEFAULT 1,
    created_at DATETIME2 DEFAULT GETDATE(),
    completed_at DATETIME2,
    descripcion NVARCHAR(500),
    created_by INT REFERENCES USUARIO(id_usuario)
);

-- Tabla para notificaciones de usuarios
CREATE TABLE NOTIFICACION (
    id_notificacion INT IDENTITY(1,1) PRIMARY KEY,
    id_usuario INT REFERENCES USUARIO(id_usuario),
    id_tarea INT REFERENCES TAREA_PENDIENTE(id_tarea),
    mensaje NVARCHAR(500),
    leida BIT DEFAULT 0,
    created_at DATETIME2 DEFAULT GETDATE()
);

-- ========================================
-- TRIGGERS PARA AUTOMATIZACIÓN DE STOCK
-- ========================================

-- Trigger: Actualizar stock_actual cuando se registra un CONSUMO
CREATE TRIGGER TR_ActualizarStockConsumo
ON CONSUMO
AFTER INSERT
AS
BEGIN
    SET NOCOUNT ON;
    
    UPDATE LOTE 
    SET stock_actual = stock_actual - i.cantidad
    FROM LOTE l
    INNER JOIN inserted i ON l.id_lote = i.id_lote;
    
    -- Verificar si el lote quedó en stock bajo
    INSERT INTO TAREA_PENDIENTE (tipo_tarea, id_entidad, tabla_entidad, id_rol_asignado, descripcion)
    SELECT 
        'STOCK_BAJO',
        l.id_lote,
        'LOTE',
        'SUPERVISOR',
        'Lote ' + l.lote + ' tiene stock bajo: ' + CAST(l.stock_actual AS NVARCHAR(20))
    FROM LOTE l
    INNER JOIN inserted i ON l.id_lote = i.id_lote
    WHERE l.stock_actual <= (l.stock_inicial * 0.1); -- 10% del stock inicial
END;

-- Trigger: Crear tareas pendientes cuando se crea un LOTE
CREATE TRIGGER TR_CrearTareasPendientesLote
ON LOTE
AFTER INSERT
AS
BEGIN
    SET NOCOUNT ON;
    
    -- Crear tarea para asignar precio si no tiene
    INSERT INTO TAREA_PENDIENTE (tipo_tarea, id_entidad, tabla_entidad, id_rol_asignado, descripcion)
    SELECT 
        'ASIGNAR_PRECIO',
        i.id_lote,
        'LOTE',
        'COMERCIAL',
        'Asignar precio al lote: ' + i.lote
    FROM inserted i
    WHERE i.precio_total IS NULL;
    
    -- Crear tarea para aprobar lote
    INSERT INTO TAREA_PENDIENTE (tipo_tarea, id_entidad, tabla_entidad, id_rol_asignado, descripcion)
    SELECT 
        'APROBAR_LOTE',
        i.id_lote,
        'LOTE',
        'LABORATORISTA',
        'Aprobar calidad del lote: ' + i.lote
    FROM inserted i
    WHERE i.estado_lote = 'PENDIENTE_APROBACION' OR i.estado_lote IS NULL;
END;

-- Trigger: Actualizar stock_actual cuando se registra un INGRESO
CREATE TRIGGER TR_ActualizarStockIngreso
ON INGRESO
AFTER INSERT
AS
BEGIN
    SET NOCOUNT ON;
    
    UPDATE LOTE 
    SET stock_actual = stock_actual + i.cantidad
    FROM LOTE l
    INNER JOIN inserted i ON l.id_lote = i.id_lote;
END;

-- ========================================
-- VISTAS PARA CONSULTAS OPTIMIZADAS
-- ========================================

-- Vista: Stock actual por insumo consolidado
CREATE VIEW VW_StockActual AS
SELECT 
    i.id_insumo,
    i.nombre as insumo,
    i.id_fox,
    c.familia,
    c.sub_familia,
    u.nombre as unidad,
    SUM(l.stock_actual) as stock_total,
    COUNT(l.id_lote) as cantidad_lotes,
    MIN(l.fecha_expiracion) as proxima_expiracion,
    AVG(CASE WHEN l.precio_total IS NOT NULL AND l.stock_inicial > 0 
             THEN l.precio_total / l.stock_inicial 
             ELSE NULL END) as precio_unitario_promedio
FROM INSUMO i
LEFT JOIN LOTE l ON i.id_insumo = l.id_insumo AND l.stock_actual > 0
LEFT JOIN CLASE c ON i.id_clase = c.id_clase
LEFT JOIN UNIDAD u ON i.id_unidad = u.id_unidad
GROUP BY i.id_insumo, i.nombre, i.id_fox, c.familia, c.sub_familia, u.nombre;

-- Vista: Tareas pendientes por usuario
CREATE VIEW VW_TareasPendientesPorUsuario AS
SELECT 
    u.id_usuario,
    u.username,
    u.email,
    r.nombre as rol,
    tp.id_tarea,
    tp.tipo_tarea,
    tp.descripcion,
    tp.prioridad,
    tp.created_at,
    DATEDIFF(DAY, tp.created_at, GETDATE()) as dias_pendiente,
    CASE tp.tipo_tarea
        WHEN 'ASIGNAR_PRECIO' THEN 'Pendiente de asignar precio'
        WHEN 'APROBAR_LOTE' THEN 'Pendiente de aprobación de calidad'
        WHEN 'STOCK_BAJO' THEN 'Stock bajo - requiere reposición'
        ELSE tp.tipo_tarea
    END as descripcion_tarea
FROM USUARIO u
INNER JOIN ROL r ON u.id_rol = r.id_rol
INNER JOIN TAREA_PENDIENTE tp ON r.id_rol = tp.id_rol_asignado
WHERE tp.estado = 'PENDIENTE' AND u.activo = 1;

-- Vista: Lotes con estado detallado
CREATE VIEW VW_LotesEstadoDetallado AS
SELECT 
    l.id_lote,
    l.lote,
    i.nombre as insumo,
    l.ubicacion,
    l.stock_inicial,
    l.stock_actual,
    (l.stock_inicial - l.stock_actual) as stock_consumido,
    CASE 
        WHEN l.stock_actual <= 0 THEN 'AGOTADO'
        WHEN l.stock_actual <= (l.stock_inicial * 0.1) THEN 'STOCK_CRITICO'
        WHEN l.stock_actual <= (l.stock_inicial * 0.2) THEN 'STOCK_BAJO'
        ELSE 'STOCK_NORMAL'
    END as estado_stock,
    l.fecha_expiracion,
    CASE 
        WHEN l.fecha_expiracion < GETDATE() THEN 'VENCIDO'
        WHEN l.fecha_expiracion < DATEADD(DAY, 30, GETDATE()) THEN 'PROXIMO_VENCER'
        ELSE 'VIGENTE'
    END as estado_vigencia,
    l.precio_total,
    CASE 
        WHEN l.precio_total IS NULL THEN 'PENDIENTE_PRECIO'
        ELSE 'PRECIO_ASIGNADO'
    END as estado_precio,
    COALESCE(l.estado_lote, 'PENDIENTE_APROBACION') as estado_lote
FROM LOTE l
INNER JOIN INSUMO i ON l.id_insumo = i.id_insumo;

GO 

-- ========================================
-- COMENTARIOS EXPLICATIVOS
-- ========================================

/*
EXPLICACIÓN DEL MODELO DE STOCK:

1. LOTE es la tabla PRINCIPAL para el inventario:
   - stock_inicial: Cantidad que ingresó originalmente
   - stock_actual: Cantidad disponible ahora (se actualiza automáticamente)

2. STOCK es para historial de movimientos (OPCIONAL):
   - cantidad: Registra movimiento individual
   - Se puede usar para auditoría de movimientos

3. CONSUMO actualiza automáticamente LOTE.stock_actual via trigger

4. FLUJO AUTOMÁTICO:
   - Se crea LOTE → Se generan tareas para LABORATORISTA y COMERCIAL
   - Se registra CONSUMO → Se descuenta de LOTE.stock_actual
   - Stock bajo → Se genera alerta para SUPERVISOR
   - Sin precio → Tarea pendiente para COMERCIAL
   - Sin aprobar → Tarea pendiente para LABORATORISTA

5. Las vistas permiten consultas optimizadas del stock actual
*/ 