using System.Data;
using Microsoft.Data.SqlClient;

namespace Tcrfc.Api.Data;

/// <summary>
/// 連線字串只從設定（環境變數／secrets 檔）讀，⛔ 絕對不允許出現字面上的連線字串或密碼——
/// 這行 CLAUDE.md 明文規定，且本 repo 是公開的。
/// 本機開發：<c>CLUB_SQL_CONNECTION_STRING</c> 由 <c>deploy/dev/club.env</c>（不進版控）提供，
/// 灌進 <c>ConnectionStrings:Club</c> 設定鍵（見 Program.cs 的環境變數對應）。
/// 正式環境：同一個設定鍵由 VM 上 <c>/opt/tcrfc/secrets/club.env</c> 提供（docs/20-cicd.md §7.2）。
/// </summary>
public sealed class ClubSqlConnectionFactory : IClubSqlConnectionFactory
{
    private readonly string _connectionString;

    public ClubSqlConnectionFactory(IConfiguration configuration)
    {
        // 直接讀同名環境變數（ASP.NET Core 的 IConfiguration 預設就會吃進所有環境變數），
        // 不繞去 ConnectionStrings:Club 這個間接層——deploy/dev/club.env 與正式環境的
        // /opt/tcrfc/secrets/club.env 給的鍵名本來就是 CLUB_SQL_CONNECTION_STRING，
        // 兩邊用同一個鍵名，設定與文件才不會兜不起來。
        _connectionString = configuration["CLUB_SQL_CONNECTION_STRING"]
            ?? throw new InvalidOperationException(
                "找不到 CLUB_SQL_CONNECTION_STRING 設定值。請確認環境變數已提供" +
                "（本機開發見 deploy/dev/club.env；正式環境見 /opt/tcrfc/secrets/club.env）。");
    }

    public IDbConnection CreateConnection() => new SqlConnection(_connectionString);
}
