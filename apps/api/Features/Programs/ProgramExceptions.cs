namespace Tcrfc.Api.Features.Programs;

public abstract class ProgramPublicException(string message) : Exception(message);

public sealed class ProgramRegistrationValidationException(string message, string? field = null) : ProgramPublicException(message), Tcrfc.Api.Common.IFieldApiException
{
    public IReadOnlyDictionary<string, string> FieldErrors { get; } = Tcrfc.Api.Common.FieldKey.Single(field, message);
}

public sealed class ProgramSessionNotFoundException() : ProgramPublicException("找不到這個梯次，請重新整理頁面後再試一次。");
