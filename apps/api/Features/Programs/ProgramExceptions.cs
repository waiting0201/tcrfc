namespace Tcrfc.Api.Features.Programs;

public abstract class ProgramPublicException(string message) : Exception(message);

public sealed class ProgramRegistrationValidationException(string message) : ProgramPublicException(message);

public sealed class ProgramSessionNotFoundException() : ProgramPublicException("找不到這個梯次，請重新整理頁面後再試一次。");
