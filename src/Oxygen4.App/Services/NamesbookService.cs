using System.IO;
using System.Text;
using Oxygen4.Models;

namespace Oxygen4.Services;

/// <summary>
/// 名单文件服务，对应 Oxygen3 的 oxyfile 类。
/// namesbook 文件为 base64 编码的多行文本，每行解码后为 "name count"。
/// 解码后临时写入 configs/config.bin，操作完成后重新编码写回并删除临时文件。
/// </summary>
public class NamesbookService
{
    private readonly string _configDir;
    private readonly LogService _log;
    private readonly string _settingsPath;

    public string CurrentFile { get; private set; } = "default";

    public NamesbookService(string? baseDir = null, LogService? log = null)
    {
        var root = baseDir ?? AppContext.BaseDirectory;
        _configDir = Path.Combine(root, "configs");
        _settingsPath = Path.Combine(_configDir, "settings.json");
        _log = log ?? new LogService(root);
        Directory.CreateDirectory(_configDir);
        Directory.CreateDirectory(Path.Combine(_configDir, "bin"));
        LoadSettings();
    }

    private void LoadSettings()
    {
        try
        {
            if (File.Exists(_settingsPath))
            {
                var json = File.ReadAllText(_settingsPath);
                var settings = System.Text.Json.JsonSerializer.Deserialize<AppSettings>(json);
                CurrentFile = settings?.File ?? "default";
            }
            else
            {
                SaveSettings("default");
            }
        }
        catch (Exception ex)
        {
            _log.Error($"读取设置文件失败：{ex.Message}");
            CurrentFile = "default";
        }
    }

    private void SaveSettings(string file)
    {
        var settings = new AppSettings { File = file };
        var json = System.Text.Json.JsonSerializer.Serialize(settings, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_settingsPath, json);
    }

    private string NamesbookPath(string name) => Path.Combine(_configDir, $"{name}.namesbook");
    private string ConfigBinPath => Path.Combine(_configDir, "config.bin");

    /// <summary>
    /// 解码当前 namesbook 至 config.bin。
    /// </summary>
    public void DecodeFile()
    {
        var path = NamesbookPath(CurrentFile);
        if (!File.Exists(path))
        {
            _log.Error("配置文件不存在，无法解码。");
            return;
        }

        var lines = File.ReadAllLines(path);
        var decoded = new List<string>();
        foreach (var line in lines)
        {
            try
            {
                var bytes = Convert.FromBase64String(line.Trim());
                decoded.Add(Encoding.UTF8.GetString(bytes) + "\n");
            }
            catch (Exception ex)
            {
                _log.Error($"解码失败：{ex.Message}");
                decoded.Add(line + "\n");
            }
        }
        File.WriteAllText(ConfigBinPath, string.Concat(decoded));
    }

    /// <summary>
    /// 将 config.bin 重新编码写回当前 namesbook 并删除临时文件。
    /// </summary>
    public void EncodeFile()
    {
        if (!File.Exists(ConfigBinPath))
        {
            _log.Error("配置基础池不存在，无法编码。");
            return;
        }

        var lines = File.ReadAllLines(ConfigBinPath);
        var encoded = new List<string>();
        foreach (var line in lines)
        {
            var bytes = Encoding.UTF8.GetBytes(line.Trim());
            encoded.Add(Convert.ToBase64String(bytes) + "\n");
        }
        File.WriteAllText(NamesbookPath(CurrentFile), string.Concat(encoded));
        File.Delete(ConfigBinPath);
    }

    /// <summary>
    /// 读取当前 namesbook 内容到内存列表。
    /// </summary>
    public List<StudentEntry> ReadFile()
    {
        var entries = new List<StudentEntry>();
        DecodeFile();
        if (!File.Exists(ConfigBinPath))
        {
            _log.Error("配置基础池不存在，无法读取。");
            return entries;
        }

        foreach (var line in File.ReadAllLines(ConfigBinPath))
        {
            var parts = line.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 2)
            {
                if (int.TryParse(parts[1], out var count))
                {
                    entries.Add(new StudentEntry { Name = parts[0], Count = count, Cooldown = 0 });
                }
                else
                {
                    _log.Error($"读取基础池时发生错误，跳过无效行：{line.Trim()}");
                }
            }
        }
        return entries;
    }

    /// <summary>
    /// 将内存列表写回 config.bin 并编码保存。
    /// </summary>
    public void WriteFile(List<StudentEntry> entries)
    {
        DecodeFile();
        var sb = new StringBuilder();
        foreach (var e in entries)
        {
            sb.AppendLine($"{e.Name} {e.Count}");
        }
        File.WriteAllText(ConfigBinPath, sb.ToString());
        EncodeFile();
    }

    /// <summary>
    /// 创建新的 namesbook 文件。
    /// </summary>
    public void CreateFile(string filename, IEnumerable<string> names)
    {
        var path = NamesbookPath(filename);
        Directory.CreateDirectory(_configDir);
        var sb = new StringBuilder();
        foreach (var name in names)
        {
            var bytes = Encoding.UTF8.GetBytes($"{name} 0");
            sb.AppendLine(Convert.ToBase64String(bytes));
        }
        File.WriteAllText(path, sb.ToString());
        _log.Info($"名单已创建成功，文件名为 {path}。");
    }

    /// <summary>
    /// 获取所有 namesbook 文件名（不含扩展名）。
    /// </summary>
    public List<string> ListFiles()
    {
        var files = new List<string>();
        if (Directory.Exists(_configDir))
        {
            foreach (var f in Directory.GetFiles(_configDir, "*.namesbook"))
            {
                files.Add(Path.GetFileNameWithoutExtension(f));
            }
        }
        return files;
    }

    /// <summary>
    /// 切换当前使用的 namesbook。
    /// </summary>
    public void ChangeFile(string filename)
    {
        CurrentFile = filename;
        SaveSettings(filename);
        _log.Info($"当前使用的基础池已切换为 {filename}.namesbook。");
    }

    /// <summary>
    /// 重命名 namesbook 文件。
    /// </summary>
    public bool RenameFile(string oldName, string newName)
    {
        var oldPath = NamesbookPath(oldName);
        var newPath = NamesbookPath(newName);
        if (!File.Exists(oldPath)) return false;
        File.Move(oldPath, newPath);
        if (CurrentFile == oldName)
        {
            CurrentFile = newName;
            SaveSettings(newName);
        }
        _log.Info($"基础池文件 {oldName}.namesbook 已重命名为 {newName}.namesbook。");
        return true;
    }

    /// <summary>
    /// 删除 namesbook 文件并保存备份到 configs/bin。
    /// </summary>
    public bool RemoveFile(string filename)
    {
        var path = NamesbookPath(filename);
        if (!File.Exists(path)) return false;

        var backupsPath = Path.Combine(_configDir, "bin");
        Directory.CreateDirectory(backupsPath);
        var backupPath = Path.Combine(backupsPath, $"{filename}.namesbook.bak");
        File.Copy(path, backupPath, true);
        File.Delete(path);

        if (CurrentFile == filename)
        {
            CurrentFile = "default";
            SaveSettings("default");
        }
        _log.Info($"基础池 {filename}.namesbook 已被删除，备份文件已保存至 {backupPath}。");
        return true;
    }

    /// <summary>
    /// 清空备份文件夹。
    /// </summary>
    public void ClearBackups()
    {
        var backupsPath = Path.Combine(_configDir, "bin");
        if (!Directory.Exists(backupsPath)) return;
        foreach (var f in Directory.GetFiles(backupsPath, "*.namesbook.bak"))
        {
            File.Delete(f);
        }
        _log.Info("所有备份已被删除。");
    }

    /// <summary>
    /// 重置所有成员的出场次数。
    /// </summary>
    public void ResetAll()
    {
        var entries = ReadFile();
        foreach (var e in entries)
        {
            e.Count = 0;
            e.Cooldown = 0;
        }
        WriteFile(entries);
        _log.Info("名单已重置为初始状态。");
    }
}
