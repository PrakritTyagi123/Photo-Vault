using PhotoVault.Core.Data;

namespace PhotoVault.Services;

public class ScanPipelineService
{
    private readonly DatabaseService _db;
    private readonly ThumbnailService _thumbService;
    private readonly ExifService _exifService;
    private readonly SearchService _searchService;
    private readonly VideoService _videoService;
    private readonly DuplicateDetectionService _dupService;
    private readonly GeocodingService _geoService;
    private readonly QualityScoringService _qualService;
    private readonly AiSidecarService _aiService;
    private readonly LogService _log;
    private CancellationTokenSource? _cts;
    private bool _isPaused;

    public List<ScanStep> Steps { get; } = new();
    public bool IsRunning { get; private set; }
    public bool IsPaused => _isPaused;
    public event Action? StepsUpdated;
    public event Action<string>? StatusChanged;

    public ScanPipelineService(DatabaseService db, ThumbnailService thumbService, ExifService exifService,
        SearchService searchService, VideoService videoService, DuplicateDetectionService dupService,
        GeocodingService geoService, QualityScoringService qualService, AiSidecarService aiService, LogService log)
    {
        _db = db; _thumbService = thumbService; _exifService = exifService; _searchService = searchService;
        _videoService = videoService; _dupService = dupService; _geoService = geoService;
        _qualService = qualService; _aiService = aiService; _log = log;
        InitSteps();
    }

    private void InitSteps()
    {
        Steps.AddRange(new[]
        {
            new ScanStep { Id="exif", Name="Photo EXIF", Description="Read camera, date, GPS from photos", DependsOn="None", StepNumber=1, IsImplemented=true },
            new ScanStep { Id="vidmeta", Name="Video Metadata", Description="Read date, GPS, resolution from videos", DependsOn="None", StepNumber=2, IsImplemented=true },
            new ScanStep { Id="geo", Name="Reverse Geocoding", Description="Convert GPS to city/country (1 req/sec)", DependsOn="EXIF + Video", StepNumber=3, IsImplemented=true },
            new ScanStep { Id="thumb", Name="Photo Thumbnails", Description="Create 150/400/1080px WebP thumbnails", DependsOn="None", StepNumber=4, IsImplemented=true },
            new ScanStep { Id="vidthumb", Name="Video Thumbnails", Description="Extract keyframe from videos", DependsOn="Video Meta", StepNumber=5, IsImplemented=true },
            new ScanStep { Id="dup", Name="Duplicate Detection", Description="SHA-256 hash for exact duplicates", DependsOn="None", StepNumber=6, IsImplemented=true },
            new ScanStep { Id="qual", Name="Quality Scoring", Description="Blur detection + sharpness scoring", DependsOn="Thumbnails", StepNumber=7, IsImplemented=true },
            new ScanStep { Id="face", Name="Face Detection", Description="Detect faces (requires AI server)", DependsOn="Thumbnails", StepNumber=8, IsImplemented=false, NeedsAi=true },
            new ScanStep { Id="fgrp", Name="Face Grouping", Description="Cluster faces into people", DependsOn="Faces", StepNumber=9, IsImplemented=false, NeedsAi=true },
            new ScanStep { Id="tag", Name="AI Tagging", Description="Object/scene tags (requires AI server)", DependsOn="Thumbnails", StepNumber=10, IsImplemented=false, NeedsAi=true },
            new ScanStep { Id="clip", Name="CLIP Embeddings", Description="Semantic search vectors", DependsOn="Thumbnails", StepNumber=11, IsImplemented=false, NeedsAi=true },
            new ScanStep { Id="vibe", Name="Vibe Detection", Description="Mood/atmosphere classification", DependsOn="Tags", StepNumber=12, IsImplemented=false, NeedsAi=true },
            new ScanStep { Id="cache", Name="Search Index", Description="Rebuild FTS5 search index", DependsOn="All above", StepNumber=13, IsImplemented=true },
        });
        LoadStates();
    }

    private void LoadStates()
    {
        try
        {
            using var cmd = _db.Connection.CreateCommand();
            cmd.CommandText = "SELECT step_id, status, progress, last_run FROM scan_records";
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                var s = Steps.FirstOrDefault(x => x.Id == r.GetString(0));
                if (s != null) { s.Status = r.IsDBNull(1) ? "idle" : r.GetString(1); s.Progress = r.IsDBNull(2) ? 0 : r.GetInt32(2); s.LastRun = r.IsDBNull(3) ? null : r.GetString(3); if (s.Status == "running") s.Status = "idle"; }
            }
        } catch { }
    }

    private void SaveState(ScanStep s)
    {
        try { using var cmd = _db.Connection.CreateCommand(); cmd.CommandText = "INSERT OR REPLACE INTO scan_records (step_id,status,progress,last_run) VALUES(@i,@s,@p,@l)"; cmd.Parameters.AddWithValue("@i", s.Id); cmd.Parameters.AddWithValue("@s", s.Status); cmd.Parameters.AddWithValue("@p", s.Progress); cmd.Parameters.AddWithValue("@l", (object?)s.LastRun ?? DBNull.Value); cmd.ExecuteNonQuery(); } catch { }
    }

    private void SetStatus(string s) { StatusChanged?.Invoke(s); }

    public async Task RunStepAsync(string id)
    {
        var step = Steps.FirstOrDefault(s => s.Id == id); if (step == null) return;
        _cts = new CancellationTokenSource(); IsRunning = true;
        await ExecuteAsync(step, _cts.Token);
        IsRunning = false; _cts = null;
    }

    public async Task RunAllAsync()
    {
        _cts = new CancellationTokenSource(); IsRunning = true; _isPaused = false;
        _log.Info("Scan", "Starting full pipeline");
        foreach (var step in Steps)
        {
            if (_cts.Token.IsCancellationRequested) break;
            while (_isPaused) { await Task.Delay(200); if (_cts.Token.IsCancellationRequested) break; }
            await ExecuteAsync(step, _cts.Token);
        }
        _log.Info("Scan", "Pipeline complete"); SetStatus("All scans complete");
        IsRunning = false; _cts = null;
    }

    public void Pause() { _isPaused = true; SetStatus("Paused"); }
    public void Resume() { _isPaused = false; SetStatus("Running..."); }
    public void Cancel()
    {
        _cts?.Cancel(); _isPaused = false;
        foreach (var s in Steps.Where(s => s.Status == "running")) { s.Status = "idle"; s.Progress = 0; SaveState(s); }
        SetStatus("Cancelled"); StepsUpdated?.Invoke();
    }

    private async Task ExecuteAsync(ScanStep step, CancellationToken ct)
    {
        if (!step.IsImplemented)
        {
            if (step.NeedsAi && !_aiService.IsRunning)
            {
                step.Status = "idle"; step.Progress = 0;
                SetStatus($"{step.Name}: Start AI server first");
                _log.Debug("Scan", $"{step.Name}: AI server not running");
                SaveState(step); StepsUpdated?.Invoke(); return;
            }
            step.Status = "done"; step.Progress = 100; step.LastRun = DateTime.Now.ToString("dd MMM yyyy, HH:mm");
            SetStatus($"{step.Name}: not yet available"); SaveState(step); StepsUpdated?.Invoke(); return;
        }

        step.Status = "running"; step.Progress = 0; StepsUpdated?.Invoke();
        _log.Info("Scan", $"Starting: {step.Name}"); SetStatus($"Running: {step.Name}...");

        try
        {
            switch (step.Id)
            {
                case "exif":
                    await Task.Run(() => { var p = new Progress<(int done, int total)>(v => { step.Progress = v.total > 0 ? (int)(v.done * 100.0 / v.total) : 0; SetStatus($"Photo EXIF: {v.done}/{v.total}"); StepsUpdated?.Invoke(); }); _exifService.ExtractAllAsync(p, ct).Wait(); }, ct);
                    break;
                case "vidmeta":
                    var vp = new Progress<(int done, int total)>(v => { step.Progress = v.total > 0 ? (int)(v.done * 100.0 / v.total) : 0; SetStatus($"Video metadata: {v.done}/{v.total}"); StepsUpdated?.Invoke(); });
                    await _videoService.ExtractAllMetadataAsync(vp, ct); break;
                case "geo":
                    var gp = new Progress<(int done, int total)>(v => { step.Progress = v.total > 0 ? (int)(v.done * 100.0 / v.total) : 0; SetStatus($"Geocoding: {v.done}/{v.total}"); StepsUpdated?.Invoke(); });
                    await _geoService.GeocodeAllAsync(gp, ct); break;
                case "thumb":
                    await Task.Run(() => { var p = new Progress<(int done, int total, string status)>(v => { step.Progress = v.total > 0 ? (int)(v.done * 100.0 / v.total) : 0; SetStatus($"Photo thumbnails: {v.done}/{v.total}"); StepsUpdated?.Invoke(); }); _thumbService.GenerateAllAsync(p, ct).Wait(); }, ct);
                    break;
                case "vidthumb":
                    var vtp = new Progress<(int done, int total)>(v => { step.Progress = v.total > 0 ? (int)(v.done * 100.0 / v.total) : 0; SetStatus($"Video thumbnails: {v.done}/{v.total}"); StepsUpdated?.Invoke(); });
                    await _videoService.GenerateAllThumbnailsAsync(vtp, ct); break;
                case "dup":
                    await Task.Run(() => { var p = new Progress<(int done, int total)>(v => { step.Progress = v.total > 0 ? (int)(v.done * 100.0 / v.total) : 0; SetStatus($"Hashing: {v.done}/{v.total}"); StepsUpdated?.Invoke(); }); _dupService.ComputeHashesAsync(p, ct).Wait(); }, ct);
                    SetStatus($"Duplicates: {_dupService.GetTotalDuplicateCount()} found"); break;
                case "qual":
                    await Task.Run(() => { var p = new Progress<(int done, int total)>(v => { step.Progress = v.total > 0 ? (int)(v.done * 100.0 / v.total) : 0; SetStatus($"Quality: {v.done}/{v.total}"); StepsUpdated?.Invoke(); }); _qualService.ScoreAllAsync(p, ct).Wait(); }, ct);
                    break;
                case "cache":
                    SetStatus("Rebuilding search index...");
                    await Task.Run(() => _searchService.RebuildIndex(), ct);
                    step.Progress = 100; StepsUpdated?.Invoke(); break;
            }
            step.Status = "done"; step.LastRun = DateTime.Now.ToString("dd MMM yyyy, HH:mm"); step.Progress = 100;
            _log.Info("Scan", $"Complete: {step.Name}");
        }
        catch (OperationCanceledException) { step.Status = "idle"; step.Progress = 0; }
        catch (Exception ex) { step.Status = "error"; _log.Error("Scan", $"Failed: {step.Name} — {ex.Message}"); }
        SaveState(step); StepsUpdated?.Invoke();
    }

    public Dictionary<string, int> GetPendingCounts()
    {
        var c = new Dictionary<string, int>();
        try
        {
            using var cmd = _db.Connection.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM media WHERE has_exif=0 AND media_type NOT IN ('Video','SlowMotion')"; c["exif"] = Convert.ToInt32(cmd.ExecuteScalar());
            cmd.CommandText = "SELECT COUNT(*) FROM media WHERE has_exif=0 AND media_type IN ('Video','SlowMotion')"; c["vidmeta"] = Convert.ToInt32(cmd.ExecuteScalar());
            cmd.CommandText = "SELECT COUNT(*) FROM media WHERE latitude IS NOT NULL AND (city IS NULL OR city='')"; c["geo"] = Convert.ToInt32(cmd.ExecuteScalar());
            cmd.CommandText = "SELECT COUNT(*) FROM media WHERE has_thumbnail=0 AND media_type NOT IN ('Video','SlowMotion')"; c["thumb"] = Convert.ToInt32(cmd.ExecuteScalar());
            cmd.CommandText = "SELECT COUNT(*) FROM media WHERE has_thumbnail=0 AND media_type IN ('Video','SlowMotion')"; c["vidthumb"] = Convert.ToInt32(cmd.ExecuteScalar());
            cmd.CommandText = "SELECT COUNT(*) FROM media WHERE (file_hash IS NULL OR file_hash='')"; c["dup"] = Convert.ToInt32(cmd.ExecuteScalar());
            cmd.CommandText = "SELECT COUNT(*) FROM media WHERE quality_score IS NULL AND media_type NOT IN ('Video','SlowMotion')"; c["qual"] = Convert.ToInt32(cmd.ExecuteScalar());
            cmd.CommandText = "SELECT COUNT(*) FROM media"; c["cache"] = Convert.ToInt32(cmd.ExecuteScalar());
        } catch { }
        return c;
    }
}

public class ScanStep
{
    public string Id { get; set; } = ""; public string Name { get; set; } = ""; public string Description { get; set; } = "";
    public string DependsOn { get; set; } = ""; public int StepNumber { get; set; }
    public string Status { get; set; } = "idle"; public int Progress { get; set; }
    public string? LastRun { get; set; } public bool IsImplemented { get; set; } public bool NeedsAi { get; set; }
}
