-- SCRIPT POSTGRESQL - MODELO DE INSUMOS TEXFINA
-- Convertido de SQL Server a PostgreSQL
-- Fecha de conversión: 2025-09-04

-- Las bases de datos en PostgreSQL se crean automáticamente desde Neon
-- Usar directamente las tablas

-- Tabla CLASE
CREATE TABLE CLASE (
    id_clase VARCHAR(50) PRIMARY KEY,
    familia VARCHAR(100),
    sub_familia VARCHAR(100)
);

-- Tabla UNIDAD
CREATE TABLE UNIDAD (
    id_unidad VARCHAR(50) PRIMARY KEY,
    nombre VARCHAR(100)
);

-- Tabla ROL
CREATE TABLE ROL (
    id_rol VARCHAR(50) PRIMARY KEY,
    nombre VARCHAR(100),
    descripcion VARCHAR(200)
);

-- Tabla TIPO_USUARIO
CREATE TABLE TIPO_USUARIO (
    id_tipo_usuario SERIAL PRIMARY KEY,
    descripcion VARCHAR(100),
    requiere_cierre_automatico BOOLEAN
);

-- Tabla USUARIO
CREATE TABLE USUARIO (
    id_usuario SERIAL PRIMARY KEY,
    username VARCHAR(50) UNIQUE NOT NULL,
    email VARCHAR(100),
    password_hash VARCHAR(255),
    id_rol VARCHAR(50) REFERENCES ROL(id_rol),
    id_tipo_usuario INTEGER REFERENCES TIPO_USUARIO(id_tipo_usuario),
    activo BOOLEAN DEFAULT true,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    last_login TIMESTAMP
);

-- Tabla PERMISO
CREATE TABLE PERMISO (
    id_permiso SERIAL PRIMARY KEY,
    nombre VARCHAR(100),
    descripcion VARCHAR(200)
);

-- Tabla ROL_PERMISO
CREATE TABLE ROL_PERMISO (
    id SERIAL PRIMARY KEY,
    id_rol VARCHAR(50) REFERENCES ROL(id_rol),
    id_permiso INTEGER REFERENCES PERMISO(id_permiso)
);

-- Tabla SESION (usar UUID en lugar de UNIQUEIDENTIFIER)
CREATE TABLE SESION (
    id_sesion UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    id_usuario INTEGER REFERENCES USUARIO(id_usuario),
    inicio TIMESTAMP,
    fin TIMESTAMP,
    cerrada_automaticamente BOOLEAN
);

-- Tabla LOG_EVENTO
CREATE TABLE LOG_EVENTO (
    id SERIAL PRIMARY KEY,
    id_usuario INTEGER REFERENCES USUARIO(id_usuario),
    accion VARCHAR(100),
    descripcion VARCHAR(500),
    ip_origen VARCHAR(50),
    modulo VARCHAR(100),
    tabla_afectada VARCHAR(100),
    timestamp TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Tabla PROVEEDOR
CREATE TABLE PROVEEDOR (
    id_proveedor SERIAL PRIMARY KEY,
    empresa VARCHAR(200),
    ruc VARCHAR(20),
    contacto VARCHAR(200),
    direccion VARCHAR(500),
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Tabla INSUMO
CREATE TABLE INSUMO (
    id_insumo SERIAL PRIMARY KEY,
    codigo VARCHAR(100) UNIQUE,
    nombre VARCHAR(200),
    descripcion TEXT,
    id_clase VARCHAR(50) REFERENCES CLASE(id_clase),
    id_unidad VARCHAR(50) REFERENCES UNIDAD(id_unidad),
    precio_unitario DECIMAL(10,2),
    stock_minimo DECIMAL(10,2),
    stock_maximo DECIMAL(10,2),
    activo BOOLEAN DEFAULT true,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Tabla ALMACEN
CREATE TABLE ALMACEN (
    id_almacen SERIAL PRIMARY KEY,
    nombre VARCHAR(200),
    descripcion TEXT,
    ubicacion VARCHAR(500),
    activo BOOLEAN DEFAULT true,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Tabla STOCK
CREATE TABLE STOCK (
    id SERIAL PRIMARY KEY,
    id_insumo INTEGER REFERENCES INSUMO(id_insumo),
    id_almacen INTEGER REFERENCES ALMACEN(id_almacen),
    cantidad DECIMAL(10,2) DEFAULT 0,
    cantidad_reservada DECIMAL(10,2) DEFAULT 0,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    UNIQUE(id_insumo, id_almacen)
);

-- Tabla LOTE
CREATE TABLE LOTE (
    id_lote SERIAL PRIMARY KEY,
    numero_lote VARCHAR(100),
    id_insumo INTEGER REFERENCES INSUMO(id_insumo),
    id_almacen INTEGER REFERENCES ALMACEN(id_almacen),
    cantidad DECIMAL(10,2),
    fecha_vencimiento DATE,
    fecha_ingreso TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    activo BOOLEAN DEFAULT true
);

-- Tabla MOVIMIENTO
CREATE TABLE MOVIMIENTO (
    id_movimiento SERIAL PRIMARY KEY,
    id_insumo INTEGER REFERENCES INSUMO(id_insumo),
    id_almacen INTEGER REFERENCES ALMACEN(id_almacen),
    tipo_movimiento VARCHAR(20) CHECK (tipo_movimiento IN ('ENTRADA', 'SALIDA', 'TRANSFERENCIA', 'AJUSTE')),
    cantidad DECIMAL(10,2),
    precio_unitario DECIMAL(10,2),
    id_proveedor INTEGER REFERENCES PROVEEDOR(id_proveedor),
    numero_documento VARCHAR(100),
    observaciones TEXT,
    id_usuario INTEGER REFERENCES USUARIO(id_usuario),
    fecha_movimiento TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Tabla ALERTA
CREATE TABLE ALERTA (
    id_alerta SERIAL PRIMARY KEY,
    tipo VARCHAR(50) CHECK (tipo IN ('STOCK_BAJO', 'VENCIMIENTO_PROXIMO', 'SIN_STOCK')),
    id_insumo INTEGER REFERENCES INSUMO(id_insumo),
    id_almacen INTEGER REFERENCES ALMACEN(id_almacen),
    mensaje TEXT,
    activa BOOLEAN DEFAULT true,
    fecha_creacion TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    fecha_resolucion TIMESTAMP
);

-- Insertar datos básicos
INSERT INTO ROL (id_rol, nombre, descripcion) VALUES
('ADMIN', 'Administrador', 'Acceso completo al sistema'),
('SUPERVISOR', 'Supervisor', 'Supervisión de operaciones'),
('OPERARIO', 'Operario', 'Operaciones básicas de inventario'),
('CONSULTOR', 'Consultor', 'Solo consulta de información');

INSERT INTO TIPO_USUARIO (descripcion, requiere_cierre_automatico) VALUES
('Usuario Estándar', false),
('Usuario Temporal', true);

INSERT INTO USUARIO (username, email, password_hash, id_rol, id_tipo_usuario) VALUES
('admin', 'admin@texfina.com', '$2a$10$rQZ5Kt6PZmQtJ.l8L1q8kuJ.WQP6QfMvTfJj2Q9nX7K6.RQV8Y7z.', 'ADMIN', 1);

INSERT INTO CLASE (id_clase, familia, sub_familia) VALUES
('CL001', 'Químicos', 'Ácidos'),
('CL002', 'Químicos', 'Bases'),
('CL003', 'Textiles', 'Fibras');

INSERT INTO UNIDAD (id_unidad, nombre) VALUES
('KG', 'Kilogramos'),
('LT', 'Litros'),
('UN', 'Unidades'),
('MT', 'Metros');

INSERT INTO ALMACEN (nombre, descripcion, ubicacion) VALUES
('Almacén Principal', 'Almacén central de insumos', 'Piso 1 - Zona A'),
('Almacén Químicos', 'Almacén especializado para químicos', 'Piso 2 - Zona B');

INSERT INTO PERMISO (nombre, descripcion) VALUES
('CREAR_INSUMO', 'Crear nuevos insumos'),
('EDITAR_INSUMO', 'Editar insumos existentes'),
('ELIMINAR_INSUMO', 'Eliminar insumos'),
('VER_REPORTES', 'Ver reportes del sistema'),
('GESTIONAR_USUARIOS', 'Gestionar usuarios del sistema');

INSERT INTO ROL_PERMISO (id_rol, id_permiso) VALUES
('ADMIN', 1), ('ADMIN', 2), ('ADMIN', 3), ('ADMIN', 4), ('ADMIN', 5),
('SUPERVISOR', 1), ('SUPERVISOR', 2), ('SUPERVISOR', 4),
('OPERARIO', 1), ('OPERARIO', 2),
('CONSULTOR', 4);