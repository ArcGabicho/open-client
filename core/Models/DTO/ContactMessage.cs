namespace OpenClient.Models.DTO;

// Mensaje del formulario de contacto público.
public sealed class ContactMessage
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
}

public enum ContactSendStatus
{
    Sent,
    Invalid,
    NotConfigured,
    Failed
}

public sealed class ContactSendResult
{
    public ContactSendStatus Status { get; init; }
    public IReadOnlyList<string> Errors { get; init; } = [];

    public bool Ok => Status == ContactSendStatus.Sent;

    public static ContactSendResult Sent() => new() { Status = ContactSendStatus.Sent };

    public static ContactSendResult Invalid(IEnumerable<string> errors) =>
        new() { Status = ContactSendStatus.Invalid, Errors = errors.ToList() };

    public static readonly ContactSendResult NotConfigured = new() { Status = ContactSendStatus.NotConfigured };
    public static readonly ContactSendResult Failed = new() { Status = ContactSendStatus.Failed };
}