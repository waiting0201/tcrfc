namespace Tcrfc.Api.Features.AdminClubs;

public abstract class AdminClubException(string message) : Exception(message);

public sealed class AdminClubValidationException(string message) : AdminClubException(message);

/// <summary><c>clubs.code</c> 全域唯一（<c>UQ_clubs_code</c>）已被使用。對應 409。</summary>
public sealed class AdminClubCodeConflictException(string code)
    : AdminClubException($"俱樂部代碼「{code}」已經被使用，請換一個。");

/// <summary><c>clubs.domain</c> 全域唯一（<c>UQ_clubs_domain</c>）已被使用。對應 409。</summary>
public sealed class AdminClubDomainConflictException(string domain)
    : AdminClubException($"網域「{domain}」已經被其他俱樂部使用。");
