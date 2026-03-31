using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace PhotoVault.Services;

public class AiSidecarService
{
    private readonly LogService _log;
    private readonly HttpClient _http;
    private Process? _sidecarProcess;
    private const string BaseUrl = "http://localhost:8100";

    public bool IsRunning { get; private set; }
    public string Status { get; private set; } = "Stopped";

    public AiSidecarService(LogService log)
    {
        _log = log;
        _http = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
    }

    public async Task<bool> StartAsync(string? pythonPath = null)
    {
        if (IsRunning) return true;

        var sidecarDir = FindSidecarDir();
        if (sidecarDir == null) { _log.Error("AI", "python_sidecar directory not found"); Status = "Sidecar not found"; return false; }

        var python = pythonPath ?? "python";
        var serverPy = Path.Combine(sidecarDir, "server.py");

        try
        {
            _sidecarProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = python, Arguments = $"\"{serverPy}\"",
                    WorkingDirectory = sidecarDir,
                    UseShellExecute = false, CreateNoWindow = true,
                    RedirectStandardOutput = true, RedirectStandardError = true,
                }
            };
            _sidecarProcess.Start();
            _log.Info("AI", "Starting Python sidecar...");

            // Wait for server to be ready
            for (int i = 0; i < 30; i++)
            {
                await Task.Delay(1000);
                if (await HealthCheckAsync()) { IsRunning = true; Status = "Running"; _log.Info("AI", "Sidecar ready"); return true; }
            }

            _log.Error("AI", "Sidecar failed to start within 30 seconds");
            Status = "Failed to start";
            return false;
        }
        catch (Exception ex) { _log.Error("AI", $"Start failed: {ex.Message}"); Status = $"Error: {ex.Message}"; return false; }
    }

    public void Stop()
    {
        try { _sidecarProcess?.Kill(); _sidecarProcess?.Dispose(); } catch { }
        _sidecarProcess = null; IsRunning = false; Status = "Stopped";
        _log.Info("AI", "Sidecar stopped");
    }

    public async Task<bool> HealthCheckAsync()
    {
        try { var r = await _http.GetAsync($"{BaseUrl}/health"); return r.IsSuccessStatusCode; } catch { return false; }
    }

    public async Task<string?> CaptionAsync(string imagePath)
    {
        return await PostImageAsync("/caption", imagePath);
    }

    public async Task<List<string>> TagAsync(string imagePath)
    {
        var result = await PostImageAsync("/tag", imagePath);
        if (result == null) return new();
        try { return JsonSerializer.Deserialize<List<string>>(result) ?? new(); } catch { return new(); }
    }

    public async Task<List<FaceResult>> DetectFacesAsync(string imagePath)
    {
        var result = await PostImageAsync("/faces", imagePath);
        if (result == null) return new();
        try { return JsonSerializer.Deserialize<List<FaceResult>>(result) ?? new(); } catch { return new(); }
    }

    public async Task<float[]?> ClipEmbedAsync(string imagePath)
    {
        var result = await PostImageAsync("/clip", imagePath);
        if (result == null) return null;
        try { return JsonSerializer.Deserialize<float[]>(result); } catch { return null; }
    }

    public async Task<string?> OcrAsync(string imagePath) => await PostImageAsync("/ocr", imagePath);
    public async Task<string?> DepthAsync(string imagePath, string outputPath) => await PostImageAsync($"/depth?output={Uri.EscapeDataString(outputPath)}", imagePath);
    public async Task<string?> SuperResAsync(string imagePath, string outputPath) => await PostImageAsync($"/superres?output={Uri.EscapeDataString(outputPath)}", imagePath);
    public async Task<bool> NsfwCheckAsync(string imagePath) { var r = await PostImageAsync("/nsfw", imagePath); return r == "true"; }

    public async Task<bool> DownloadModelsAsync(IProgress<string>? progress = null)
    {
        var sidecarDir = FindSidecarDir();
        if (sidecarDir == null) return false;

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "python", Arguments = $"\"{Path.Combine(sidecarDir, "download_models.py")}\"",
                WorkingDirectory = sidecarDir, UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true,
            };
            var proc = Process.Start(psi);
            if (proc == null) return false;

            while (!proc.StandardOutput.EndOfStream)
            {
                var line = await proc.StandardOutput.ReadLineAsync();
                if (line != null) { progress?.Report(line); _log.Info("AI", line); }
            }
            await proc.WaitForExitAsync();
            return proc.ExitCode == 0;
        }
        catch (Exception ex) { _log.Error("AI", $"Download failed: {ex.Message}"); return false; }
    }

    private async Task<string?> PostImageAsync(string endpoint, string imagePath)
    {
        if (!IsRunning) return null;
        try
        {
            using var content = new MultipartFormDataContent();
            content.Add(new ByteArrayContent(await File.ReadAllBytesAsync(imagePath)), "file", Path.GetFileName(imagePath));
            var response = await _http.PostAsync($"{BaseUrl}{endpoint}", content);
            if (response.IsSuccessStatusCode) return await response.Content.ReadAsStringAsync();
            _log.Error("AI", $"{endpoint}: {response.StatusCode}");
        }
        catch (Exception ex) { _log.Error("AI", $"{endpoint}: {ex.Message}"); }
        return null;
    }

    private static string? FindSidecarDir()
    {
        var dir = AppDomain.CurrentDomain.BaseDirectory;
        for (int i = 0; i < 5; i++)
        {
            var candidate = Path.Combine(dir, "python_sidecar");
            if (Directory.Exists(candidate)) return candidate;
            dir = Path.GetDirectoryName(dir) ?? dir;
        }
        return null;
    }
}

public class FaceResult
{
    public float X { get; set; } public float Y { get; set; } public float W { get; set; } public float H { get; set; }
    public float Confidence { get; set; } public float[]? Embedding { get; set; }
}
