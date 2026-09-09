using System.IO;

namespace Oxygen4.Services;

/// <summary>
/// 日志服务，对应 Oxygen3 的 oxylog 类。
/// 日志按日期写入 logs/YYYY-MM-DD.log，同时输出到控制台。
/// </summary>
public class LogService
{
    private readonly string _logDir;
    private readonly object _lock = new();

    public LogService(string? baseDir = null)
    {
        _logDir = Path.Combine(baseDir ?? AppContext.BaseDirectory, "logs");
        Directory.CreateDirectory(_logDir);
    }

    public void Log(string message, string level = "INFO")
    {
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd=HH:mm:ss");
        var line = $"[Oxygen4][{level.ToUpperInvariant()}][{timestamp}] \n > {message}";

        Console.WriteLine(line);

        lock (_lock)
        {
            var logPath = Path.Combine(_logDir, $"{DateTime.Now:yyyy-MM-dd}.log");
            File.AppendAllText(logPath, line + "\n");
        }
    }

    public void Info(string message) => Log(message, "INFO");
    public void Warning(string message) => Log(message, "WARNING");
    public void Error(string message) => Log(message, "ERROR");
}
