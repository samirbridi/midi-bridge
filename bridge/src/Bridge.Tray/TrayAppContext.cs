using System.Diagnostics;
using System.Drawing;
using System.IO.Pipes;
using System.ServiceProcess;
using System.Text;
using System.Text.Json;
using System.Windows.Forms;
using Bridge.Core.Status;

namespace Bridge.Tray;

public sealed class TrayAppContext : ApplicationContext
{
    private const string ServiceName = "UsbMidiBridge";
    private const string PipeName = "UsbMidiBridge.Status";

    private readonly NotifyIcon _notifyIcon;
    private readonly ToolStripMenuItem _statusItem;
    private readonly ToolStripMenuItem _serviceItem;
    private readonly ToolStripMenuItem _openLogsItem;
    private readonly System.Windows.Forms.Timer _timer;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    private BridgeStatus? _lastStatus;

    public TrayAppContext()
    {
        _statusItem = new ToolStripMenuItem("Status: ...") { Enabled = false };
        _serviceItem = new ToolStripMenuItem("Service: ...") { Enabled = false };
        _openLogsItem = new ToolStripMenuItem("Open logs");
        _openLogsItem.Click += (_, _) => OpenLogs();

        var start = new ToolStripMenuItem("Start service");
        start.Click += (_, _) => StartService();

        var stop = new ToolStripMenuItem("Stop service");
        stop.Click += (_, _) => StopService();

        var exit = new ToolStripMenuItem("Exit");
        exit.Click += (_, _) => ExitThread();

        var menu = new ContextMenuStrip();
        menu.Items.Add(_statusItem);
        menu.Items.Add(_serviceItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(start);
        menu.Items.Add(stop);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_openLogsItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(exit);

        _notifyIcon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "USB MIDI Bridge",
            Visible = true,
            ContextMenuStrip = menu
        };

        _notifyIcon.DoubleClick += (_, _) => OpenLogs();

        _timer = new System.Windows.Forms.Timer { Interval = 1500 };
        _timer.Tick += (_, _) => Refresh();
        _timer.Start();

        Refresh();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _timer.Stop();
            _timer.Dispose();
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
        }
        base.Dispose(disposing);
    }

    private void Refresh()
    {
        var svcState = GetServiceState();
        _serviceItem.Text = $"Service: {svcState}";
        _openLogsItem.Enabled = true;

        var status = TryGetBridgeStatus();
        _lastStatus = status;

        var line = status is null
            ? "Status: offline"
            : $"Status: {status.ServiceState}";

        if (status?.ProfileId is not null)
        {
            line += $" | {status.ProfileId}";
        }

        _statusItem.Text = line;
        _notifyIcon.Text = TruncateTooltip(BuildTooltip(status, svcState));
    }

    private static string TruncateTooltip(string s)
    {
        if (s.Length <= 63)
        {
            return s;
        }

        return s[..63];
    }

    private static string BuildTooltip(BridgeStatus? status, string serviceState)
    {
        if (status is null)
        {
            return $"USB MIDI Bridge ({serviceState})";
        }

        var baseText = $"USB MIDI Bridge ({status.ServiceState})";
        if (!string.IsNullOrWhiteSpace(status.ProfileId))
        {
            baseText += $" {status.ProfileId}";
        }

        return baseText;
    }

    private string GetServiceState()
    {
        try
        {
            using var sc = new ServiceController(ServiceName);
            return sc.Status.ToString();
        }
        catch
        {
            return "NotInstalled";
        }
    }

    private BridgeStatus? TryGetBridgeStatus()
    {
        try
        {
            using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut);
            client.Connect(timeout: 250);
            using var reader = new StreamReader(client, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, bufferSize: 4096, leaveOpen: true);
            using var writer = new StreamWriter(client, Encoding.UTF8, bufferSize: 4096, leaveOpen: true) { AutoFlush = true };
            writer.WriteLine("GET_STATUS");
            var json = reader.ReadLine();
            if (string.IsNullOrWhiteSpace(json) || string.Equals(json, "ERR", StringComparison.Ordinal))
            {
                return null;
            }

            return JsonSerializer.Deserialize<BridgeStatus>(json, _jsonOptions);
        }
        catch
        {
            return null;
        }
    }

    private static void StartService()
    {
        try
        {
            using var sc = new ServiceController(ServiceName);
            if (sc.Status == ServiceControllerStatus.Running || sc.Status == ServiceControllerStatus.StartPending)
            {
                return;
            }
            sc.Start();
        }
        catch
        {
        }
    }

    private static void StopService()
    {
        try
        {
            using var sc = new ServiceController(ServiceName);
            if (sc.Status == ServiceControllerStatus.Stopped || sc.Status == ServiceControllerStatus.StopPending)
            {
                return;
            }
            sc.Stop();
        }
        catch
        {
        }
    }

    private void OpenLogs()
    {
        var p = BridgeDiagnosticsPath();
        try
        {
            if (Directory.Exists(p))
            {
                Process.Start(new ProcessStartInfo("explorer.exe", p) { UseShellExecute = true });
            }
        }
        catch
        {
        }
    }

    private static string BridgeDiagnosticsPath()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(localAppData, "UsbMidiBridge");
    }
}

