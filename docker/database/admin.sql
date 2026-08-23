USE [OpenClientDb];
GO

IF OBJECT_ID(N'dbo.Users', N'U') IS NULL
BEGIN
    PRINT 'Creando tabla dbo.Users...';

    CREATE TABLE dbo.Users
    (
        Id INT IDENTITY(1,1) NOT NULL
            CONSTRAINT PK_Users PRIMARY KEY,

        Email NVARCHAR(255) NOT NULL,
        PasswordHash NVARCHAR(500) NOT NULL,
        Role NVARCHAR(50) NOT NULL,
        IsActive BIT NOT NULL
            CONSTRAINT DF_Users_IsActive DEFAULT 1,
        CreatedAt DATETIME2 NOT NULL
            CONSTRAINT DF_Users_CreatedAt DEFAULT GETUTCDATE(),

        CONSTRAINT UQ_Users_Email UNIQUE (Email)
    );
END
ELSE
BEGIN
    PRINT 'La tabla dbo.Users ya existe.';
END
GO

GRANT SELECT ON dbo.Users TO openclient_runtime;
GO

IF NOT EXISTS
(
    SELECT 1
    FROM dbo.Users
    WHERE Email = N'__ADMIN_EMAIL__'
)
BEGIN
    INSERT INTO dbo.Users
    (
        Email,
        PasswordHash,
        Role,
        IsActive
    )
    VALUES
    (
        N'__ADMIN_EMAIL__',
        N'__ADMIN_PASSWORD_HASH__',
        N'Admin',
        1
    );

    PRINT 'Administrador creado correctamente.';
END
ELSE
BEGIN
    -- Política de contraseñas: .env es la fuente de verdad del administrador
    -- inicial. Si el admin ya existe, SOLO se actualiza el hash (nunca el
    -- correo, rol ni estado), permitiendo rotar OPENCLIENT_ADMIN_PASSWORD
    -- sin recrear la base de datos.
    UPDATE dbo.Users
    SET PasswordHash = N'__ADMIN_PASSWORD_HASH__'
    WHERE Email = N'__ADMIN_EMAIL__';

    PRINT 'Administrador existente: hash actualizado desde .env.';
END
GO