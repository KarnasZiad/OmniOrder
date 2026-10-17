namespace OmniOrder.ServiceDefaults;

/// <summary>A business rule was violated. <see cref="Code"/> is a stable, machine-readable identifier.</summary>
public class DomainException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

public sealed class NotFoundException(string resource, object key) : Exception($"{resource} '{key}' was not found.");
