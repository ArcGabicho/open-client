using OpenClient.Models.DTO;

namespace OpenClient.Interfaces;

// Envía el mensaje del formulario de contacto público a la dirección fija del
// proyecto. La configuración SMTP se toma de "Contact:Smtp:*"; si falta, el
// mensaje se registra en el log y no se envía.
public interface IContactMailer
{
    Task<ContactSendResult> SendAsync(ContactMessage message, CancellationToken cancellationToken = default);
}