using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using Bridge.Core.Status;

namespace Bridge.Service;

public sealed class BridgeStatusPipeHostedService : BackgroundService
{
    private const string PipeName = "UsbMidiBridge.Status";
    private readonly ILogger<BridgeStatusPipeHostedService> _logger;
    private readonly BridgeStatusState _state;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    public BridgeStatusPipeHostedService(ILogger<BridgeStatusPipeHostedService> logger, BridgeStatusState state)
    {
        _logger = logger;
        _state = state;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var pipe = new NamedPipeServerStream(
                    PipeName,
                    PipeDirection.InOut,
                    NamedPipeServerStream.MaxAllowedServerInstances,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous
                );

                await pipe.WaitForConnectionAsync(stoppingToken).ConfigureAwait(false);
                await HandleConnectionAsync(pipe, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "IPC pipe error");
                await Task.Delay(500, stoppingToken).ConfigureAwait(false);
            }
        }
    }

    private async Task HandleConnectionAsync(Stream stream, CancellationToken stoppingToken)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, bufferSize: 4096, leaveOpen: true);
        await using var writer = new StreamWriter(stream, Encoding.UTF8, bufferSize: 4096, leaveOpen: true) { AutoFlush = true };

        var line = await reader.ReadLineAsync(stoppingToken).ConfigureAwait(false);
        if (!string.Equals(line, "GET_STATUS", StringComparison.Ordinal))
        {
            await writer.WriteLineAsync("ERR").ConfigureAwait(false);
            return;
        }

        var status = _state.Snapshot();
        var json = JsonSerializer.Serialize(status, _jsonOptions);
        await writer.WriteLineAsync(json).ConfigureAwait(false);
    }
}

