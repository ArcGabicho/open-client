using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using OpenClient.Models.DTO;
using OpenClient.Models.Validators;
using OpenClient.Services;
using Xunit;

namespace OpenClient.Api.Tests;

public sealed class ContactMailerTests
{
    private static SmtpContactMailer Build(params (string Key, string Value)[] settings)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(settings.Select(s =>
                new KeyValuePair<string, string?>(s.Key, s.Value)))
            .Build();

        return new SmtpContactMailer(config, new ContactMessageValidator(), NullLogger<SmtpContactMailer>.Instance);
    }

    private static ContactMessage ValidMessage() => new()
    {
        Name = "Jhonny",
        Email = "jhonny@example.com",
        Subject = "Consulta sobre despliegue",
        Body = "Hola, me gustaría saber cómo desplegar Open Client."
    };

    [Fact]
    public void Recipient_is_the_fixed_project_address()
    {
        Assert.Equal("contact@gabicho.dev", SmtpContactMailer.Recipient);
    }

    [Fact]
    public async Task Invalid_message_is_rejected_before_sending()
    {
        var mailer = Build();

        var result = await mailer.SendAsync(new ContactMessage());

        Assert.Equal(ContactSendStatus.Invalid, result.Status);
        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public async Task Without_smtp_configured_it_reports_not_configured()
    {
        var mailer = Build();

        var result = await mailer.SendAsync(ValidMessage());

        Assert.Equal(ContactSendStatus.NotConfigured, result.Status);
        Assert.False(result.Ok);
    }

    [Fact]
    public async Task Unreachable_smtp_host_fails_gracefully()
    {
        var mailer = Build(
            ("Contact:Smtp:Host", "127.0.0.1"),
            ("Contact:Smtp:Port", "2"),
            ("Contact:Smtp:From", "web@openclient.local"),
            ("Contact:Smtp:UseSsl", "false"));

        var result = await mailer.SendAsync(ValidMessage());

        Assert.Equal(ContactSendStatus.Failed, result.Status);
    }
}
