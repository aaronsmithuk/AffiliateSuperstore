using System.Threading.Channels;

namespace AffiliateSuperstore.Web.Services;

public sealed class CatalogueAutomationWakeSignal
{
    private readonly Channel<bool> _channel = Channel.CreateBounded<bool>(new BoundedChannelOptions(1)
    {
        FullMode = BoundedChannelFullMode.DropWrite,
        SingleReader = true,
        SingleWriter = false
    });

    public bool Signal() => _channel.Writer.TryWrite(true);

    public async Task WaitAsync(CancellationToken cancellationToken)
    {
        await _channel.Reader.ReadAsync(cancellationToken);
    }
}
