namespace OpenClient.Interfaces;

public interface IDbInitializer
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
}
