using Xunit;

namespace Tcrfc.Api.Tests.Fixtures;

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ApiFixture>
{
    public const string Name = "api";
}

[CollectionDefinition(Name)]
public sealed class RedisUnavailableCollection : ICollectionFixture<RedisUnavailableApiFixture>
{
    public const string Name = "api-redis-unavailable";
}

[CollectionDefinition(Name)]
public sealed class RedisEnabledCollection : ICollectionFixture<RedisEnabledApiFixture>
{
    public const string Name = "api-redis-enabled";
}
