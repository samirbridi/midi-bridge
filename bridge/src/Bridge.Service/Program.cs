using Bridge.Service;

var builder = Host.CreateApplicationBuilder(args);
if (OperatingSystem.IsWindows())
{
    builder.Services.AddWindowsService(options => { options.ServiceName = "USB MIDI Bridge"; });
}
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
