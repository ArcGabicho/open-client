namespace OpenClient.Interfaces;

/// <summary>
/// Puesta en marcha de la base de datos al arrancar la aplicación: aplica las
/// migraciones pendientes, provisiona el administrador inicial y siembra los
/// clientes de muestra si la tabla está vacía. Debe ser idempotente.
/// </summary>
public interface IDbInitializer
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
}
