namespace Tcrfc.Api.Features.AdminStandings;

public abstract class AdminStandingException(string message) : Exception(message);

public sealed class AdminStandingValidationException(string message) : AdminStandingException(message);
