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

/// <summary>`AdminNews`／`AdminAuth` 相關測試共用的 collection，見 <see cref="AdminWriteApiFixture"/>。</summary>
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

/// <summary>S0-8 圖片上傳共用元件：真實 Azurite ＋ 開發寫入開關，見 <see cref="AdminWriteAzuriteEnabledApiFixture"/>。</summary>
[CollectionDefinition(Name)]
public sealed class AdminWriteAzuriteEnabledCollection : ICollectionFixture<AdminWriteAzuriteEnabledApiFixture>
{
    public const string Name = "api-admin-write-azurite-enabled";
}
