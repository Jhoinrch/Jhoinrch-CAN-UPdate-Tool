using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace DfuTool;

/// <summary>Runs dfu-util CLI: locate or extract bundled tools, start process, stream output.</summary>
public class DfuUtilRunner
{
    private const string NativeVersion = "0.11";

    private static readonly string[] EmbeddedNames =
    {
        "DfuTool.native.dfu-util.exe",
        "DfuTool.native.dfu-prefix.exe",
        "DfuTool.native.dfu-suffix.exe",
        "DfuTool.native.libusb-1.0.dll",
        "DfuTool.native.libstdc++-6.dll",
        "DfuTool.native.libgcc_s_seh-1.dll",
        "DfuTool.native.libwinpthread-1.dll",
        "DfuTool.native.libatomic-1.dll",
        "DfuTool.native.libgomp-1.dll",
        "DfuTool.native.libquadmath-0.dll",
        "DfuTool.native.zadig.exe",
    };

    private const string BundleStamp = "0.11+zadig+robust1";
    private const long MinDfuUtilBytes = 32 * 1024;

    /// <summary>dfu-util executable path.</summary>
    public string ExePath { get; set; } = "dfu-util";

    /// <summary>Directory containing dfu-util and its DLLs (used as WorkingDirectory).</summary>
    public string? WorkingDirectory { get; set; }

    /// <summary>Last locate/extract failure detail (for UI log).</summary>
    public static string? LastLocateError { get; private set; }

    public bool IsBusy { get; private set; }

    public event Action<string>? OutputReceived;

    public event Action<int>? Exited;

    public async Task<int> RunAsync(string arguments, CancellationToken ct = default)
    {
        if (IsBusy)
            throw new InvalidOperationException("dfu-util is already running");
        IsBusy = true;
        try
        {
            if (!UsableDfuUtil(ExePath) || WorkingDirectory is null)
            {
                var located = LocateOrExtract();
                if (located is null)
                    throw new FileNotFoundException(
                        $"dfu-util.exe not found. {LastLocateError}",
                        ExePath);
                ExePath = located;
                WorkingDirectory = Path.GetDirectoryName(located);
            }

            if (!UsableDfuUtil(ExePath))
                throw new FileNotFoundException($"dfu-util path is not usable: '{ExePath}'", ExePath);

            var psi = new ProcessStartInfo
            {
                FileName = ExePath,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = WorkingDirectory ?? Path.GetDirectoryName(ExePath) ?? "",
            };
            using var proc = new Process { StartInfo = psi };
            proc.OutputDataReceived += (_, e) => { if (!string.IsNullOrEmpty(e.Data)) OutputReceived?.Invoke(e.Data); };
            proc.ErrorDataReceived += (_, e) => { if (!string.IsNullOrEmpty(e.Data)) OutputReceived?.Invoke(e.Data); };
            try
            {
                proc.Start();
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                throw new FileNotFoundException(
                    $"Failed to start dfu-util at '{ExePath}' (Win32 {ex.NativeErrorCode}). {ex.Message}",
                    ExePath,
                    ex);
            }
            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();
            try
            {
                await proc.WaitForExitAsync(ct);
            }
            catch (OperationCanceledException)
            {
                try { proc.Kill(entireProcessTree: true); } catch { /* already exited */ }
                throw;
            }
            Exited?.Invoke(proc.ExitCode);
            return proc.ExitCode;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static bool UsableDfuUtil(string path) =>
        !string.IsNullOrWhiteSpace(path)
        && Path.IsPathRooted(path)
        && path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
        && File.Exists(path)
        && new FileInfo(path).Length >= MinDfuUtilBytes;

    /// <summary>Side-by-side → embedded extract (multi-dir) → PATH.</summary>
    public static string? LocateOrExtract()
    {
        LastLocateError = null;

        foreach (var c in SideBySideCandidates())
        {
            if (UsableDfuUtil(c)) return Path.GetFullPath(c);
        }

        var extracted = EnsureEmbeddedExtracted();
        if (extracted is not null) return extracted;

        foreach (var c in PathCandidates())
        {
            if (UsableDfuUtil(c)) return Path.GetFullPath(c);
        }

        LastLocateError ??= "dfu-util.exe not found beside the app, in the extract cache, or on PATH.";
        return null;
    }

    public static string? LocateExe() => LocateOrExtract();

    /// <summary>Locate/extract bundled Zadig (driver helper).</summary>
    public static string? LocateZadig()
    {
        var side = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "zadig.exe"),
            Path.Combine(Environment.CurrentDirectory, "zadig.exe"),
        };
        foreach (var c in side)
        {
            if (File.Exists(c) && new FileInfo(c).Length > 512 * 1024)
                return Path.GetFullPath(c);
        }

        EnsureEmbeddedExtracted();
        foreach (var dir in BundleDirCandidates())
        {
            var inBundle = Path.Combine(dir, "zadig.exe");
            if (File.Exists(inBundle) && new FileInfo(inBundle).Length > 512 * 1024)
                return inBundle;
        }
        return null;
    }

    private static IEnumerable<string> SideBySideCandidates()
    {
        yield return Path.Combine(AppContext.BaseDirectory, "dfu-util.exe");
        yield return Path.Combine(Environment.CurrentDirectory, "dfu-util.exe");
        yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "dfu-util", "dfu-util.exe");
    }

    private static IEnumerable<string> PathCandidates()
    {
        var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var dir in pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            string full;
            try { full = Path.Combine(dir.Trim(), "dfu-util.exe"); }
            catch (ArgumentException) { continue; }
            yield return full;
        }
    }

    private static IEnumerable<string> BundleDirCandidates()
    {
        var localApp = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (!string.IsNullOrEmpty(localApp))
            yield return Path.Combine(localApp, "DfuTool", "native", NativeVersion);

        var temp = Path.GetTempPath();
        if (!string.IsNullOrEmpty(temp))
            yield return Path.Combine(temp, "DfuTool", "native", NativeVersion);

        string? baseDir = null;
        try
        {
            baseDir = Path.Combine(AppContext.BaseDirectory, "dfu-native", NativeVersion);
        }
        catch (Exception)
        {
            // invalid base directory
        }
        if (!string.IsNullOrEmpty(baseDir))
            yield return baseDir;
    }

    private static string? FirstWritableBundleDir()
    {
        foreach (var dir in BundleDirCandidates())
        {
            try
            {
                Directory.CreateDirectory(dir);
                var probe = Path.Combine(dir, ".writetest");
                File.WriteAllText(probe, "ok");
                File.Delete(probe);
                return dir;
            }
            catch (Exception)
            {
                // try next location
            }
        }
        return null;
    }

    private static string? EnsureEmbeddedExtracted()
    {
        // Already complete in any cache dir?
        foreach (var existing in BundleDirCandidates())
        {
            if (BundleLooksComplete(existing))
                return Path.Combine(existing, "dfu-util.exe");
        }

        var dir = FirstWritableBundleDir();
        if (dir is null)
        {
            LastLocateError = "Cannot create a folder to extract dfu-util (LocalAppData/Temp both blocked).";
            return null;
        }

        try
        {
            Directory.CreateDirectory(dir);

            var asm = Assembly.GetExecutingAssembly()
                ?? Assembly.GetEntryAssembly()
                ?? typeof(DfuUtilRunner).Assembly;

            var names = asm.GetManifestResourceNames();
            if (!names.Contains("DfuTool.native.dfu-util.exe"))
            {
                LastLocateError =
                    "Bundled resource DfuTool.native.dfu-util.exe missing from this build. "
                    + $"Resources: [{string.Join(", ", names)}]";
                return null;
            }

            foreach (var logical in EmbeddedNames)
            {
                var idx = logical.IndexOf(".native.", StringComparison.Ordinal);
                var fileName = idx >= 0 ? logical[(idx + ".native.".Length)..] : logical;
                var dest = Path.Combine(dir, fileName);

                using var src = asm.GetManifestResourceStream(logical);
                if (src is null)
                {
                    if (fileName.Equals("dfu-util.exe", StringComparison.OrdinalIgnoreCase))
                    {
                        LastLocateError = $"Resource stream null for {logical}.";
                        return null;
                    }
                    continue;
                }

                var tmp = dest + ".tmp";
                using (var fs = File.Create(tmp))
                {
                    src.CopyTo(fs);
                }
                if (File.Exists(dest)) File.Delete(dest);
                File.Move(tmp, dest);
            }

            var exe = Path.Combine(dir, "dfu-util.exe");
            if (!File.Exists(exe) || new FileInfo(exe).Length < MinDfuUtilBytes)
            {
                LastLocateError = $"Extracted dfu-util.exe is missing or too small in {dir}.";
                return null;
            }

            File.WriteAllText(Path.Combine(dir, ".ok"), BundleStamp);
            return exe;
        }
        catch (Exception ex)
        {
            LastLocateError = $"Extract failed: {ex.GetType().Name}: {ex.Message}";
            return null;
        }
    }

    private static bool BundleLooksComplete(string dir)
    {
        try
        {
            var exe = Path.Combine(dir, "dfu-util.exe");
            var marker = Path.Combine(dir, ".ok");
            var zadig = Path.Combine(dir, "zadig.exe");
            if (!File.Exists(marker)) return false;
            if (!File.Exists(exe) || new FileInfo(exe).Length < MinDfuUtilBytes) return false;
            // Prefer stamp match; if old stamp but good exe+zadig, still accept.
            var stamp = File.ReadAllText(marker).Trim();
            if (stamp == BundleStamp) return File.Exists(zadig);
            return File.Exists(zadig);
        }
        catch
        {
            return false;
        }
    }
}
