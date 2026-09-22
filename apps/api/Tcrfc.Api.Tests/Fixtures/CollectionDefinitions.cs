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

/// <summary>本輪新增：唯一開啟 <c>ENABLE_UNSAFE_DEV_WRITES</c> 的 collection，見 <see cref="AdminWriteApiFixture"/>。</summary>
[CollectionDefinition(Name)]
public sealed class AdminWriteCollection : ICollectionFixture<AdminWriteApiFixture>
{
    public const string Name = "api-admin-write";
}

/// <summary>本輪新增：驗證寫入後公開快取失效，見 <see cref="AdminWriteRedisEnabledApiFixture"/>。</summary>
[CollectionDefinition(Name)]
public sealed class AdminWriteRedisEnabledCollection : ICollectionFixture<AdminWriteRedisEnabledApiFixture>
{
    public const string Name = "api-admin-write-redis-enabled";
}
