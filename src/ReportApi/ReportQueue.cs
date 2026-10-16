using System.Threading.Channels;

public sealed class ReportQueue
{
    readonly Channel<string> _channel = Channel.CreateUnbounded<string>();

    public ChannelReader<string> Reader => _channel.Reader;

    public ValueTask EnqueueAsync(string id) => _channel.Writer.WriteAsync(id);
}
