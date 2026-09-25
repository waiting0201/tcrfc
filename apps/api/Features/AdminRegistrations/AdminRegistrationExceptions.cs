namespace Tcrfc.Api.Features.AdminRegistrations;

public abstract class AdminRegistrationException(string message) : Exception(message);

public sealed class AdminRegistrationValidationException(string message) : AdminRegistrationException(message);

/// <summary><c>SessionId</c> 指向的梯次不存在，或不屬於這個俱樂部。</summary>
public sealed class SessionNotFoundForRegistrationException()
    : AdminRegistrationException("找不到這個俱樂部的梯次，請確認梯次是否存在。");
