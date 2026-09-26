-- =====================================================================
-- WebApiCore - Esquema consolidado (Auth + CORE / Password Manager)
-- =====================================================================
-- Uso: ejecutar contra la BD de destino (db_testing en local o la BD de
--      producción en el hosting). El script reconstruye el esquema desde
--      cero (DROP + CREATE), por lo que es seguro ejecutarlo de nuevo.
--
-- ATENCIÓN: este script BORRA las tablas. Solo usarlo para una instalación
--      limpia; nunca contra una base que ya tenga datos.
--
-- NOTA: no incluye CREATE DATABASE / LOGIN porque son específicos del
--      entorno. Para recrear db_testing en local:
--
--   CREATE LOGIN testing WITH PASSWORD = 'testing', CHECK_POLICY = OFF;
--   GO
--   CREATE DATABASE db_testing;
--   GO
--   USE db_testing;
--   GO
--   CREATE USER testing FOR LOGIN testing;
--   GO
--   EXEC sp_addrolemember 'db_owner', 'testing';
--
-- =====================================================================

-- Drops (reconstrucción limpia) ---------------------------------------
IF OBJECT_ID('dbo.PM_CoreData', 'U') IS NOT NULL DROP TABLE dbo.PM_CoreData;
GO
IF OBJECT_ID('dbo.Auth_Users', 'U') IS NOT NULL DROP TABLE dbo.Auth_Users;
GO
IF OBJECT_ID('dbo.Auth_Profiles', 'U') IS NOT NULL DROP TABLE dbo.Auth_Profiles;
GO
IF OBJECT_ID('dbo.Mae_Config', 'U') IS NOT NULL DROP TABLE dbo.Mae_Config;
GO

-- Tablas ------------------------------------------------------------------

CREATE TABLE dbo.Mae_Config (
    Config_Id INT PRIMARY KEY IDENTITY(1,1),
    ApiKey VARCHAR(256),
    IsEnableRegister BIT NOT NULL
);
GO

CREATE TABLE dbo.Auth_Profiles (
    Profile_Id INT PRIMARY KEY IDENTITY(1,1),
    Name VARCHAR(50) NOT NULL UNIQUE
);
GO

CREATE TABLE dbo.Auth_Users (
    User_Id UNIQUEIDENTIFIER PRIMARY KEY,
    Email VARCHAR(100) NOT NULL UNIQUE,
    HashLogin VARCHAR(256) NOT NULL,
    SaltLogin VARCHAR(256) NOT NULL,
    HashPM VARCHAR(256),
    SaltPM VARCHAR(256),
    -- Token de sesión (SqlToken). Se guarda SOLO su SHA-256 en hexadecimal
    -- (64 chars); el token crudo nunca se persiste. Se genera en C# al
    -- iniciar sesión.
    SqlTokenHash VARCHAR(64),
    Profile_Id INT NOT NULL,
    FOREIGN KEY (Profile_Id) REFERENCES dbo.Auth_Profiles(Profile_Id)
);
GO

CREATE TABLE dbo.PM_CoreData (
    Data_Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    Data01 VARCHAR(256) NOT NULL,
    Data02 VARCHAR(256) NOT NULL,
    Data03 VARCHAR(256) NOT NULL,
    User_Id UNIQUEIDENTIFIER NOT NULL,
    FOREIGN KEY (User_Id) REFERENCES dbo.Auth_Users(User_Id)
);
GO

-- Seed ----------------------------------------------------------------------

SET IDENTITY_INSERT dbo.Auth_Profiles ON;
INSERT INTO dbo.Auth_Profiles (Profile_Id, Name) VALUES (1, 'ADMIN'), (2, 'USER');
SET IDENTITY_INSERT dbo.Auth_Profiles OFF;
GO

-- ApiKey del entorno local; en producción reemplazar por el valor real.
SET IDENTITY_INSERT dbo.Mae_Config ON;
INSERT INTO dbo.Mae_Config (Config_Id, ApiKey, IsEnableRegister) VALUES (1, 'Testing-777', 1);
SET IDENTITY_INSERT dbo.Mae_Config OFF;
GO
