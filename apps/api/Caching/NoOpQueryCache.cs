namespace Tcrfc.Api.Caching;

/// <summary>本次任務的唯一實作：不快取，一律回源。見 <see cref="IQueryCache"/> 上的完整說明。</summary>
public sealed class NoOpQueryCache : IQueryCache
{
    public Task<T> GetOrCreateAsync<T>(
        string key,
        Func<CancellationToken, Task<T>> factory,
        CancellationToken cancellationToken)
        => factory(cancellationToken);
}
