using Bridge.Service;

var builder = Host.CreateApplicationBuilder(args);
if (OperatingSystem.IsWindows())
{
    builder.Services.AddWindowsService(options => { options.ServiceName = "UsbMidiBridge"; });
}
builder.Services.AddSingleton<BridgeStatusState>();
builder.Services.AddHostedService<BridgeStatusPipeHostedService>();
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
