create database DB_GCS
go
use BD_prueba

CREATE TABLE dbo.Seguridad_Usuario (
    IdUsuario               INT IDENTITY(1,1) PRIMARY KEY,
    Correo                  VARCHAR(150) NOT NULL UNIQUE,
    PasswordHash            VARCHAR(200) NOT NULL,
    Nombres                 VARCHAR(150) NOT NULL,
    Rol                     VARCHAR(50)  NOT NULL DEFAULT('User'),
    Estado                  BIT NOT NULL DEFAULT(1),
    UltimoCambioPasswordUtc VARCHAR(30) NULL,
    FechaCreacionUtc        DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
);
GO

CREATE TABLE dbo.Proyecto (
    IdProyecto       INT IDENTITY(1,1) PRIMARY KEY,
    Codigo           NVARCHAR(50)  NOT NULL UNIQUE,
    Nombre           NVARCHAR(150) NOT NULL,
    Descripcion      NVARCHAR(400) NULL,
    Estado           NVARCHAR(20)  NOT NULL DEFAULT (N'Activo'),
    FechaInicio      DATE          NULL,
    FechaFin         DATE          NULL,
    IdUsuarioOwner   INT           NULL, -- referencia lógica
    Metodologia      NVARCHAR(20)  NULL,
    FechaCreacionUtc DATETIME2     NOT NULL DEFAULT SYSUTCDATETIME()
);
GO

CREATE TABLE dbo.ProyectoMiembro (
    IdProyecto   INT PRIMARY KEY NOT NULL,
    IdUsuario    INT NOT NULL,
    RolMiembro   NVARCHAR(50) NULL,
    Estado       BIT NOT NULL DEFAULT(1),
    FechaAltaUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
);
GO

CREATE TABLE dbo.ProyectoInvitacion (
    IdInvitacion     INT IDENTITY(1,1) PRIMARY KEY,
    IdProyecto       INT NOT NULL,      -- referencia lógica
    CorreoDestino    VARCHAR(150) NOT NULL,
    Token            UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    Estado           NVARCHAR(20) NOT NULL DEFAULT (N'Pendiente'),
    ExpiraUtc        DATETIME2    NOT NULL,
    FechaEnvioUtc    DATETIME2    NOT NULL DEFAULT SYSUTCDATETIME(),
    IdUsuarioInvita  INT          NOT NULL -- referencia lógica
);
GO

CREATE TABLE dbo.ProyectoColumna (
    IdColumna  INT IDENTITY(1,1) PRIMARY KEY,
    IdProyecto INT           NOT NULL,          -- referencia lógica
    Titulo     NVARCHAR(80)  NOT NULL,
    Clave      NVARCHAR(30)  NULL,
    ColorHex   NVARCHAR(9)   NULL,              -- p.ej. '#3b82f6' o '#3b82f680'
    Orden      INT           NOT NULL DEFAULT (0)
);
GO

CREATE TABLE dbo.ProyectoTarea (
    IdTarea           INT IDENTITY(1,1) PRIMARY KEY,
    IdProyecto        INT           NOT NULL,        -- referencia lógica
    IdColumna         INT           NULL,            -- estado/columna lógico
    Grupo             NVARCHAR(120) NULL,            -- agrupador opcional
    IdTareaPadre      INT           NULL,            -- subtareas (lógico)
    Titulo            NVARCHAR(150) NOT NULL,
    Hecho             BIT           NOT NULL DEFAULT (0),
    Estado            NVARCHAR(20)  NOT NULL DEFAULT (N'Pendiente'),
    Progreso          TINYINT       NOT NULL DEFAULT (0),  -- 0..100
    Esfuerzo          NVARCHAR(12)  NULL,                  -- Fácil/Medio/Difícil/Muy Difícil
    AsignadoA         INT           NULL,                  -- responsable único opcional
    FechaInicio       DATE          NULL,
    FechaFin          DATE          NULL,
    FechaVencimiento  DATE          NULL,
    Orden             INT           NOT NULL DEFAULT (0),
    FechaCreacionUtc  DATETIME2     NOT NULL DEFAULT SYSUTCDATETIME()
);
GO

CREATE TABLE dbo.ProyectoTareaAsignacion (
    IdTarea        INT           PRIMARY KEY NOT NULL,       -- referencia lógica
    IdUsuario      INT           NOT NULL,       -- referencia lógica
    HorasAsignadas DECIMAL(9,2)  NOT NULL DEFAULT (0),
    EsResponsable  BIT           NOT NULL DEFAULT (0),
    FechaAltaUtc   DATETIME2     NOT NULL DEFAULT SYSUTCDATETIME()
);
GO

CREATE TABLE dbo.Etiqueta (
    IdEtiqueta INT IDENTITY(1,1) PRIMARY KEY,
    Nombre     NVARCHAR(50) NOT NULL UNIQUE
);
GO

CREATE TABLE dbo.ProyectoEtiqueta (
    IdProyecto INT PRIMARY KEY NOT NULL,  -- referencia lógica
    IdEtiqueta INT NOT NULL,  -- referencia lógica
);
GO

CREATE TABLE dbo.ProyectoNota (
    IdNota                 INT IDENTITY(1,1) PRIMARY KEY,
    IdProyecto             INT NOT NULL UNIQUE,   -- una nota por proyecto
    Contenido              NVARCHAR(MAX) NULL,
    FechaActualizacionUtc  DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
);
GO

CREATE TABLE dbo.ProyectoGrupo(
        IdGrupo     INT IDENTITY(1,1) PRIMARY KEY,
        IdProyecto  INT NOT NULL,
        Nombre      NVARCHAR(120) NOT NULL,
        Orden       INT NOT NULL DEFAULT(0),
        CONSTRAINT UQ_ProyectoGrupo UNIQUE(IdProyecto, Nombre)
    );

ALTER TABLE dbo.ProyectoMiembro ADD Estado BIT NULL;
GO

ALTER TABLE dbo.ProyectoTarea ADD IdColumna INT NULL;
GO
use BD_prueba
select * from Proyecto
select * from ProyectoTarea
select * from ProyectoTareaAsignacion