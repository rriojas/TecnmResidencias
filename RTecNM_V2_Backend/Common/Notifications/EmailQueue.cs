using System.Threading.Channels;

namespace TecNM.Residency.Common.Notifications;

public interface IEmailQueue
{
    void Enqueue(EmailMessageDto message);
    ValueTask<EmailMessageDto> DequeueAsync(CancellationToken cancellationToken);
}

public class EmailQueue : IEmailQueue
{
    private readonly Channel<EmailMessageDto> _queue;

    public EmailQueue()
    {
        var options = new UnboundedChannelOptions
        {
            SingleReader = true
        };
        _queue = Channel.CreateUnbounded<EmailMessageDto>(options);
    }

    public void Enqueue(EmailMessageDto message)
    {
        if (message == null || string.IsNullOrWhiteSpace(message.ToEmail)) return;
        _queue.Writer.TryWrite(message);
    }

    public ValueTask<EmailMessageDto> DequeueAsync(CancellationToken cancellationToken)
    {
        return _queue.Reader.ReadAsync(cancellationToken);
    }
}
