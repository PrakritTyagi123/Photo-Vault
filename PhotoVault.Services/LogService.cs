using System.Collections.ObjectModel;

namespace PhotoVault.Services;

public class LogService
{
    private readonly List<LogEntry> _entries = new();
    private readonly object _lock = new();

    public IReadOnlyList<LogEntry> Entries { get { lock (_lock) return _entries.ToList(); } }

    public void Info(string source, string message) => Add("INFO", source, message);
    public void Warn(string source, string message) => Add("WARN", source, message);
    public void Error(string source, string message) => Add("ERROR", source, message);
    public void Debug(string source, string message) => Add("DEBUG", source, message);

    private void Add(string level, string source, string message)
    {
        lock (_lock)
        {
            _entries.Insert(0, new LogEntry
            {
                Time = DateTime.Now, LevelString = level, Source = source, Message = message
            });
            if (_entries.Count > 2000) _entries.RemoveAt(_entries.Count - 1);
        }
    }

    public List<LogEntry> GetFiltered(string? level = null, int limit = 500)
    {
        lock (_lock)
        {
            var query = _entries.AsEnumerable();
            if (!string.IsNullOrEmpty(level) && level != "All") query = query.Where(e => e.LevelString == level);
            return query.Take(limit).ToList();
        }
    }

    public void Clear() { lock (_lock) _entries.Clear(); }
}

public class LogEntry
{
    public DateTime Time { get; set; }
    public string LevelString { get; set; } = "INFO";
    public string Source { get; set; } = "";
    public string Message { get; set; } = "";
    public string TimeString => Time.ToString("HH:mm:ss.ff");
}
