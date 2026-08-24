namespace TecNM.Residency.Common.Notifications;

public class EmailAttachmentDto
{
    public string FileName { get; set; } = "documento.pdf";
    public byte[] Content { get; set; } = Array.Empty<byte>();
    public string ContentType { get; set; } = "application/pdf";
}

public class EmailMessageDto
{
    public string ToEmail { get; set; } = string.Empty;
    public string ToName { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string BodyHtml { get; set; } = string.Empty;
    public List<EmailAttachmentDto>? Attachments { get; set; }
}
