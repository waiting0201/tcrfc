namespace Tcrfc.Api.Caching;

/// <summary>REDIS_HOST 未設定時的唯一實作：不快取，一律回源。見 <see cref="IQueryCache"/> 上的完整說明。</summary>
public sealed class NoOpQueryCache : IQueryCache
{
    public Task<T> GetOrCreateAsync<T>(
        string entity,
        string club,
        string locale,
        string qualifier,
        Func<CancellationToken, Task<T>> factory,
        CancellationToken cancellationToken)
        => factory(cancellationToken);

    public Task InvalidateAsync(string entity, string club, CancellationToken cancellationToken)
        => Task.CompletedTask;
}
