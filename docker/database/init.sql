IF DB_ID(N'OpenClientDb') IS NULL
BEGIN
    PRINT 'Creando OpenClientDb...';

    CREATE DATABASE [OpenClientDb];
END
ELSE
BEGIN
    PRINT 'OpenClientDb ya existe';
END
GO


IF NOT EXISTS
(
    SELECT 1
    FROM sys.server_principals
    WHERE name = N'openclient_user'
)
BEGIN
    PRINT 'Creando LOGIN openclient_user...';

    CREATE LOGIN [openclient_user]
    WITH PASSWORD = '__MSSQL_APP_PASSWORD__',
         CHECK_POLICY = ON,
         CHECK_EXPIRATION = OFF;
END
ELSE
BEGIN
    PRINT 'El USUARIO openclient_user ya existe.';
END
GO


USE [OpenClientDb];
GO


IF NOT EXISTS
(
    SELECT 1
    FROM sys.database_principals
    WHERE name = N'openclient_user'
)
BEGIN
    PRINT 'Creando USUARIO openclient_user...';

    CREATE USER [openclient_user]
    FOR LOGIN [openclient_user];
END
ELSE
BEGIN
    PRINT 'El usuario openclient_user ya existe';
END
GO


IF NOT EXISTS
(
    SELECT 1
    FROM sys.database_principals
    WHERE name = N'openclient_runtime'
      AND type = 'R'
)
BEGIN
    PRINT 'Creando ROL openclient_runtime...';

    CREATE ROLE [openclient_runtime];
END
ELSE
BEGIN
    PRINT 'El ROL openclient_runtime ya existe.';
END
GO


IF NOT EXISTS
(
    SELECT 1
    FROM sys.database_role_members drm
    INNER JOIN sys.database_principals role_principal
        ON drm.role_principal_id = role_principal.principal_id
    INNER JOIN sys.database_principals user_principal
        ON drm.member_principal_id = user_principal.principal_id
    WHERE role_principal.name = N'openclient_runtime'
      AND user_principal.name = N'openclient_user'
)
BEGIN
    PRINT 'Agregando openclient_user al openclient_runtime...';

    ALTER ROLE [openclient_runtime]
    ADD MEMBER [openclient_user];
END
GO

USE [OpenClientDb];
GO

IF OBJECT_ID(N'dbo.Clients', N'U') IS NULL
BEGIN
    PRINT 'Creando tabla dbo.Clients...';

    CREATE TABLE dbo.Clients
    (
        Id INT IDENTITY(1,1) NOT NULL
            CONSTRAINT PK_Clients PRIMARY KEY,

        CompanyName NVARCHAR(100) NULL,
        LegalName NVARCHAR(100) NULL,
        Industry NVARCHAR(200) NULL,
        FirstName NVARCHAR(50) NULL,
        LastName NVARCHAR(50) NULL,
        JobTitle NVARCHAR(50) NULL,
        TaxId NVARCHAR(20) NULL,
        PhoneNumber NVARCHAR(20) NULL,
        Email NVARCHAR(400) NULL,
        Website NVARCHAR(500) NULL,
        Address NVARCHAR(500) NULL,
        District NVARCHAR(100) NULL,
        Province NVARCHAR(100) NULL,
        CreatedAt DATETIME2 NOT NULL
    );
END
ELSE
BEGIN
    PRINT 'La tabla dbo.Clients ya existe.';
END
GO