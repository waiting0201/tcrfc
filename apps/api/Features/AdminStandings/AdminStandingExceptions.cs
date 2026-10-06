namespace Tcrfc.Api.Features.AdminStandings;

public abstract class AdminStandingException(string message) : Exception(message);

public sealed class AdminStandingValidationException(string message, string? field = null) : AdminStandingException(message), Tcrfc.Api.Common.IFieldApiException
{
    public IReadOnlyDictionary<string, string> FieldErrors { get; } = Tcrfc.Api.Common.FieldKey.Single(field, message);
}