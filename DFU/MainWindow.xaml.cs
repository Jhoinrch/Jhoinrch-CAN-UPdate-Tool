using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using Microsoft.Win32;

namespace DfuTool;

/// <summary>Main window: device refresh, firmware parse, download/upload command construction and execution.</summary>
public partial class MainWindow : Window
{
    private readonly DfuUtilRunner _runner = new();
    private DfuFileInfo? _fileInfo;
    private CancellationTokenSource? _cts;
    private bool _trackProgress;
    private static readonly System.Text.RegularExpressions.Regex PercentRegex =
        new(@"(\d{1,3})\s*%", System.Text.RegularExpressions.RegexOptions.Compiled);
    private static readonly System.Text.RegularExpressions.Regex XferRegex =
        new(@"(\d+)\s*/\s*(\d+)", System.Text.RegularExpressions.RegexOptions.Compiled);

    public MainWindow()
    {
        InitializeComponent();
        _runner.OutputReceived += line => Dispatcher.Invoke(() => AppendLog(line));
        _runner.Exited += code => Dispatcher.Invoke(() => AppendLog($"dfu-util exited with code {code}"));

        var exe = DfuUtilRunner.LocateOrExtract();
        if (exe is null)
        {
            var detail = DfuUtilRunner.LastLocateError ?? "unknown reason";
            AppendLog($"ERROR: dfu-util could not be located or extracted.");
            AppendLog($"Detail: {detail}");
            SetStatus("dfu-util missing", ok: false);
        }
        else
        {
            _runner.ExePath = exe;
            _runner.WorkingDirectory = Path.GetDirectoryName(exe);
            AppendLog($"Using dfu-util: {exe}");
            AppendLog("Hint: If no device appears, click ZADIG to bind WinUSB (Options → List All Devices → DFU in FS Mode → WinUSB → Replace Driver).");
        }
    }

    private void ZadigBtn_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var path = DfuUtilRunner.LocateZadig();
            if (path is null)
            {
                AppendLog("Hint: zadig.exe not found in bundle.");
                SetStatus("Zadig missing", ok: false);
                return;
            }

            AppendLog($"Opening Zadig: {path}");
            AppendLog("In Zadig: Options → List All Devices → select DFU in FS Mode → WinUSB → Replace Driver.");

            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true,
                Verb = "runas", // elevate for driver install
                WorkingDirectory = Path.GetDirectoryName(path) ?? "",
            };
            System.Diagnostics.Process.Start(psi);
            SetStatus("Zadig launched", ok: true);
        }
        catch (Exception ex)
        {
            AppendLog($"Failed to open Zadig: {ex.Message}");
            SetStatus("Zadig failed", ok: false);
        }
    }

    private const string MoreProductsUrl =
        "https://www.amazon.com/stores/page/BEC6FB30-6DA6-421E-80B6-1904FA0C0783?ingress=2&lp_context_asin=B0CRB8KXWL&lp_context_query=Jhoinrch&visitId=18c01741-4212-48c3-b0e8-be1effe3e467&store_ref=bl_ast_dp_brandlogo_sto&ref_=ast_bln";

    private void MoreProductsBtn_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = MoreProductsUrl,
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            AppendLog($"Open More Products failed: {ex.Message}");
        }
    }

    private void GetFirmwareBtn_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "https://canable.io/builds/",
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            AppendLog($"Open Get Firmware failed: {ex.Message}");
        }
    }

    private void SiteLinkBtn_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "https://Jhoinrch.net",
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            AppendLog($"Open link failed: {ex.Message}");
        }
    }

    private void SiteLink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = e.Uri?.ToString() ?? "https://Jhoinrch.net",
                UseShellExecute = true,
            });
            e.Handled = true;
        }
        catch (Exception ex)
        {
            AppendLog($"Open link failed: {ex.Message}");
        }
    }

    private void AppendLog(string line)
    {
        LogBox.AppendText(line + Environment.NewLine);
        LogBox.ScrollToEnd();
        if (_trackProgress)
            TryUpdateProgress(line);
    }

    private void SetProgress(int percent)
    {
        if (percent < 0) percent = 0;
        if (percent > 100) percent = 100;
        ProgressFill.Value = percent;
        ProgressPercent.Text = $"{percent}%";
    }

    /// <summary>Parse dfu-util progress (NN% or transferred/total) and update the bar.</summary>
    private void TryUpdateProgress(string line)
    {
        if (string.IsNullOrWhiteSpace(line)) return;

        foreach (System.Text.RegularExpressions.Match m in PercentRegex.Matches(line))
        {
            if (int.TryParse(m.Groups[1].Value, out var p) && p is >= 0 and <= 100)
            {
                SetProgress(p);
                return;
            }
        }

        var xm = XferRegex.Match(line);
        if (xm.Success
            && long.TryParse(xm.Groups[1].Value, out var done)
            && long.TryParse(xm.Groups[2].Value, out var total)
            && total > 0
            && done >= 0
            && done <= total)
        {
            SetProgress((int)Math.Round(done * 100.0 / total));
        }
    }

    private void SetStatus(string text, bool ok = true)
    {
        StatusText.Text = text;
        StatusText.Foreground = (System.Windows.Media.Brush)
            (ok ? TryFindResource("OkBrush") : TryFindResource("DangerBrush"))
            ?? StatusText.Foreground;
        StatusDot.Fill = (System.Windows.Media.Brush)
            (ok ? TryFindResource("OkBrush") : TryFindResource("DangerBrush"))
            ?? StatusDot.Fill;
    }

    private void SetBusy(bool busy)
    {
        FlashBtn.IsEnabled = !busy;
        ReadBtn.IsEnabled = !busy;
        RefreshBtn.IsEnabled = !busy;
        ZadigBtn.IsEnabled = !busy;
        BrowseBtn.IsEnabled = !busy;
        ParseBtn.IsEnabled = !busy;
        CancelBtn.IsEnabled = busy;
    }

    /// <summary>Extract vid:pid from device combo item (format "[0483:df11] ...").</summary>
    private string? GetDeviceFilter()
    {
        var m = Regex.Match(DeviceCombo.Text, @"\[([0-9a-fA-F]{4}):([0-9a-fA-F]{4})\]");
        if (!m.Success) return null;
        return $"{m.Groups[1].Value.ToLower()}:{m.Groups[2].Value.ToLower()}";
    }

    /// <summary>Extract USB path from a "Found DFU:" line as device key (multiple alts share the same path).</summary>
    private static string GetRowKey(string line)
    {
        var m = Regex.Match(line, @"path=""([^""]+)""");
        return m.Success ? m.Groups[1].Value : line;
    }

    /// <summary>Extract "[vid:pid]" device id from a "Found DFU:" line.</summary>
    private static string GetDeviceId(string line)
    {
        var m = Regex.Match(line, @"\[([0-9a-fA-F]{4}):([0-9a-fA-F]{4})\]");
        return m.Success ? m.Value : line;
    }

    /// <summary>Parse hex address; supports optional "0x" prefix.</summary>
    private static uint ParseHex(string s, uint def)
    {
        s = s.Trim();
        if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) s = s[2..];
        return uint.TryParse(s, NumberStyles.HexNumber, null, out var v) ? v : def;
    }

    // ---------- Device refresh ----------
    private async void RefreshBtn_Click(object sender, RoutedEventArgs e)
    {
        DeviceCombo.Items.Clear();
        var lines = new List<string>();
        void OnLine(string l) => lines.Add(l);
        _runner.OutputReceived += OnLine;   // for parsing only; global log callback still shows all output
        SetBusy(true);
        SetStatus("Enumerating devices...");
        try
        {
            await _runner.RunAsync("-l");

            // STM32 lists one line per alternate setting (Internal Flash/OTP/Option Bytes);
            // group by USB path so one physical device appears once.
            var grouped = lines
                .Where(l => l.StartsWith("Found DFU:"))
                .Select(l => l.Trim())
                .GroupBy(GetRowKey);
            foreach (var g in grouped)
            {
                DeviceCombo.Items.Add($"{GetDeviceId(g.First())} | USB {g.Key} | {g.Count()} alt(s) (Flash/OTP/OB)");
            }
            var found = DeviceCombo.Items.Count > 0;
            SetStatus(found
                ? $"Found {DeviceCombo.Items.Count} DFU device(s)"
                : "No DFU device found (check BOOT0 / driver)", found);
            if (found) DeviceCombo.SelectedIndex = 0;
        }
        catch (Exception ex)
        {
            SetStatus("Enumeration failed", ok: false);
            AppendLog($"Error: {ex.Message}");
        }
        finally
        {
            _runner.OutputReceived -= OnLine;
            SetBusy(false);
        }
    }

    // ---------- File select / parse ----------
    private void BrowseBtn_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = "Select firmware file",
            Filter = "Firmware (*.dfu;*.bin)|*.dfu;*.bin|DfuSe file (*.dfu)|*.dfu|Binary file (*.bin)|*.bin|All files (*.*)|*.*",
        };
        if (dlg.ShowDialog() == true)
        {
            FilePathBox.Text = dlg.FileName;
            ParseFile();
        }
    }

    private void ParseBtn_Click(object sender, RoutedEventArgs e) => ParseFile();

    private void ParseFile()
    {
        var path = FilePathBox.Text.Trim();
        if (!File.Exists(path))
        {
            SetStatus("File not found", ok: false);
            return;
        }
        try
        {
            _fileInfo = DfuFileParser.Parse(path);
            var b = new StringBuilder($"Format: {_fileInfo.FormatName}");
            if (_fileInfo.IsDfuFile)
            {
                if (_fileInfo.VendorId is not null)
                    b.Append($" | VID:PID {_fileInfo.VendorId:x4}:{_fileInfo.ProductId:x4}");
                if (_fileInfo.CrcValid is not null)
                    b.Append(_fileInfo.CrcValid.Value ? " | CRC: OK" : " | CRC: FAIL");
                foreach (var img in _fileInfo.Images)
                    b.Append($"\n  Image: addr 0x{img.Address:x8}, size {img.Size} bytes, alt={img.AltSetting}, {img.Name}");
                AddressBox.IsEnabled = false;   // address is embedded in .dfu
            }
            else
            {
                b.Append($" | size {_fileInfo.Images[0].Size} bytes");
                AddressBox.IsEnabled = true;    // bin requires manual address
            }
            FileInfoText.Text = b.ToString();
            SetStatus("File parsed");
        }
        catch (Exception ex)
        {
            SetStatus("Parse failed", ok: false);
            AppendLog($"Parse failed: {ex.Message}");
        }
    }

    // ---------- Download / Upload ----------
    private async void FlashBtn_Click(object sender, RoutedEventArgs e)
    {
        var info = _fileInfo;
        if (info is null || !File.Exists(info.FilePath))
        {
            MessageBox.Show("Please select and parse a firmware file first.", "Hint", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var args = new List<string>();
        var dev = GetDeviceFilter();
        if (dev is not null) args.Add($"-d {dev}");

        // Always leave DFU mode after download so the device boots the new firmware.
        if (info.IsDfuFile)
        {
            var img = info.Images.FirstOrDefault();
            if (img is not null) args.Add($"-a {img.AltSetting}");
            args.Add("-s :leave");   // keep address from file; leave after download
            args.Add($"-D \"{info.FilePath}\"");
        }
        else
        {
            uint addr = ParseHex(AddressBox.Text, 0x08000000u);
            args.Add("-a 0");
            args.Add($"-s 0x{addr:x8}:leave");
            args.Add($"-D \"{info.FilePath}\"");
        }

        await RunCommand(string.Join(' ', args), withProgress: true);
    }

    private async void ReadBtn_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new SaveFileDialog { Title = "Save read firmware", Filter = "Binary (*.bin)|*.bin", FileName = "firmware.bin" };
        if (dlg.ShowDialog() != true) return;

        uint addr = ParseHex(AddressBox.Text, 0x08000000u);
        uint len = uint.TryParse(LengthBox.Text, out var l) && l > 0 ? l : 65536;
        var args = new List<string>();
        var dev = GetDeviceFilter();
        if (dev is not null) args.Add($"-d {dev}");
        args.Add($"-a 0 -s 0x{addr:x8}:{len} -U \"{dlg.FileName}\"");

        await RunCommand(string.Join(' ', args), withProgress: true);
    }

    private void CancelBtn_Click(object sender, RoutedEventArgs e) => _cts?.Cancel();

    /// <summary>Run dfu-util and update status text.</summary>
    private async Task RunCommand(string arguments, bool withProgress = false)
    {
        AppendLog($"> dfu-util {arguments}");
        _cts = new CancellationTokenSource();
        SetBusy(true);
        SetStatus("Running...");
        _trackProgress = withProgress;
        if (withProgress)
            SetProgress(0);
        try
        {
            int code = await _runner.RunAsync(arguments, _cts.Token);
            if (withProgress && code == 0)
                SetProgress(100);
            SetStatus(code == 0 ? "Success" : $"Failed (exit code {code})", code == 0);
        }
        catch (OperationCanceledException)
        {
            SetStatus("Cancelled", ok: false);
            AppendLog("Operation cancelled");
        }
        catch (Exception ex)
        {
            SetStatus("Execution error", ok: false);
            AppendLog($"Error: {ex.Message}");
        }
        finally
        {
            _trackProgress = false;
            _cts.Dispose();
            _cts = null;
            SetBusy(false);
        }
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        _cts?.Cancel();
        base.OnClosing(e);
    }
}
