-- =====================================================================
-- WebApiCore - Esquema consolidado (Auth + CORE / Password Manager)
-- =====================================================================
-- Uso: ejecutar contra la BD de destino (db_testing en local o la BD de
--      producción en el hosting). El script reconstruye el esquema desde
--      cero (DROP + CREATE), por lo que es seguro ejecutarlo de nuevo.
--
-- NOTA: no incluye CREATE DATABASE / LOGIN porque son específicos del
-- entorno. Para recrear db_testing en local:
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
IF OBJECT_ID('dbo.Auth_Register', 'P') IS NOT NULL DROP PROCEDURE dbo.Auth_Register;
GO
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
    SqlToken UNIQUEIDENTIFIER,
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

-- Stored Procedures ----------------------------------------------------------

CREATE PROCEDURE dbo.Auth_Register
    @User_Id UNIQUEIDENTIFIER,
    @Email VARCHAR(100),
    @HashLogin VARCHAR(256),
    @SaltLogin VARCHAR(256)
AS
BEGIN
    SET NOCOUNT ON;

    IF 0 = (SELECT ISNULL(IsEnableRegister, 0) FROM dbo.Mae_Config WHERE Config_Id = 1)
    BEGIN
        SELECT 0 AS IsSuccess, 403 AS StatusCode, 'El Servicio de Registro No Esta Disponible' AS Message
        RETURN
    END

    IF EXISTS (SELECT User_Id FROM dbo.Auth_Users WHERE Email = @Email)
    BEGIN
        SELECT 0 AS IsSuccess, 400 AS StatusCode, 'El Usuario ya Existe' AS Message
        RETURN
    END

    BEGIN TRY
        INSERT INTO dbo.Auth_Users (User_Id, Email, HashLogin, SaltLogin, Profile_Id)
        VALUES (@User_Id, @Email, @HashLogin, @SaltLogin, 2)

        SELECT 1 AS IsSuccess, 201 AS StatusCode, 'Usuario Registrado Correctamente' AS Message
    END TRY
    BEGIN CATCH
        SELECT 0 AS IsSuccess, 500 AS StatusCode, ERROR_MESSAGE() AS Message
    END CATCH
END
GO
