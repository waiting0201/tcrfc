using Tcrfc.Api.Data;
using Xunit;

namespace Tcrfc.Api.Tests;

/// <summary>資料遷移 migration 內嵌的 SQL 必須與 db/migrations/*.sql 逐位元組相同（docs/20 §5）。純單元，不連資料庫。</summary>
public sealed class MigrationSqlParityTests
{
    private static readonly string[] Names =
    [
        "20261113_fallback-content-import",
        "20261113_form-definition-align",
        "20261116_app-link-template-cleanup",
    ];

    private static string RepoFile(string relative)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, relative)))
        {
            dir = dir.Parent;
        }

        return dir is null ? throw new FileNotFoundException(relative) : Path.Combine(dir.FullName, relative);
    }

    [Fact]
    public void 內嵌遷移腳本與_db_migrations_逐位元組相同()
    {
        var asm = typeof(EmbeddedMigrationSql).Assembly;
        foreach (var n in Names)
        {
            using var s = asm.GetManifestResourceStream($"migration-sql/{n}.sql") ?? throw new InvalidOperationException(n);
            using var ms = new MemoryStream();
            s.CopyTo(ms);
            var repo = File.ReadAllBytes(RepoFile($"db/migrations/{n}.sql"));
            Assert.True(repo.AsSpan().SequenceEqual(ms.ToArray()),
                $"apps/api/Data/Migrations/Sql/{n}.sql 與 db/migrations/{n}.sql 不同；請 cp 覆蓋。");
        }
    }

    [Fact]
    public void 每批包成_EXEC_且單引號已跳脫_批數合理()
    {
        foreach (var n in Names)
        {
            var batches = EmbeddedMigrationSql.Batches(EmbeddedMigrationSql.Read($"migration-sql/{n}.sql"));
            Assert.NotEmpty(batches);
            foreach (var b in batches)
            {
                var w = EmbeddedMigrationSql.WrapBatch(b);
                Assert.StartsWith("EXEC(N'", w);
                Assert.DoesNotContain("\nGO\n", w);
            }
        }
    }

    [Fact]
    public void 三支_migration_都有成對的內嵌資源()
    {
        var names = typeof(EmbeddedMigrationSql).Assembly.GetManifestResourceNames();
        foreach (var n in Names)
        {
            Assert.Contains($"migration-sql/{n}.sql", names);
        }
    }
}
