using System.Net;
using System.Net.Mail;
using FluentValidation;
using OpenClient.Interfaces;
using OpenClient.Models.DTO;

namespace OpenClient.Services;

public sealed class SmtpContactMailer : IContactMailer
{
    // Destinatario fijo del formulario de contacto del proyecto.
    public const string Recipient = "contact@gabicho.dev";

    private readonly IConfiguration _configuration;
    private readonly IValidator<ContactMessage> _validator;
    private readonly ILogger<SmtpContactMailer> _logger;

    public SmtpContactMailer(
        IConfiguration configuration,
        IValidator<ContactMessage> validator,
        ILogger<SmtpContactMailer> logger)
    {
        _configuration = configuration;
        _validator = validator;
        _logger = logger;
    }

    public async Task<ContactSendResult> SendAsync(
        ContactMessage message,
        CancellationToken cancellationToken = default)
    {
        var validation = await _validator.ValidateAsync(message, cancellationToken);
        if (!validation.IsValid)
        {
            return ContactSendResult.Invalid(validation.Errors.Select(e => e.ErrorMessage));
        }

        var host = _configuration["Contact:Smtp:Host"];
        var fromAddress = FirstNonEmpty(_configuration["Contact:Smtp:From"], _configuration["Contact:Smtp:User"]);

        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(fromAddress))
        {
            // Sin SMTP configurado: no se pierde el mensaje, queda en el log.
            _logger.LogWarning(
                "CONTACT (SMTP no configurado) de {Name} <{Email}> · asunto: {Subject}\n{Body}",
                message.Name, message.Email, message.Subject, message.Body);
            return ContactSendResult.NotConfigured;
        }

        var port = _configuration.GetValue("Contact:Smtp:Port", 587);
        var useSsl = _configuration.GetValue("Contact:Smtp:UseSsl", true);
        var user = _configuration["Contact:Smtp:User"];
        var password = _configuration["Contact:Smtp:Password"];

        try
        {
            using var mail = new MailMessage
            {
                From = new MailAddress(fromAddress, "Open Client (web)"),
                Subject = $"[Open Client] {message.Subject}",
                Body =
                    $"Nombre: {message.Name}\n" +
                    $"Correo: {message.Email}\n" +
                    $"Asunto: {message.Subject}\n\n" +
                    message.Body,
                IsBodyHtml = false
            };

            mail.To.Add(Recipient);
            mail.ReplyToList.Add(new MailAddress(message.Email, message.Name));

            using var client = new SmtpClient(host, port)
            {
                EnableSsl = useSsl,
                DeliveryMethod = SmtpDeliveryMethod.Network
            };

            if (!string.IsNullOrWhiteSpace(user))
            {
                client.Credentials = new NetworkCredential(user, password);
            }

            await client.SendMailAsync(mail, cancellationToken);

            _logger.LogInformation(
                "CONTACT enviado a {Recipient} · de {Email} · asunto: {Subject}",
                Recipient, message.Email, message.Subject);

            return ContactSendResult.Sent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CONTACT: fallo al enviar el mensaje de {Email}.", message.Email);
            return ContactSendResult.Failed;
        }
    }

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
}