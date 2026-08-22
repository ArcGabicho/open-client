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