using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using Oxygen4.Models;

namespace Oxygen4.Services;

/// <summary>
/// NotifyIsland 通知服务，对应 Oxygen3 的 oxynotify 类。
/// 通过 HTTP POST 向 NotifyIsland 插件（默认 localhost:5002/notify）发送通知。
/// API 文档：https://github.com/linxianlww/NotifyIsland
/// 配置持久化到 configs/settings.json。
/// </summary>
public class NotifyService
{
    private readonly HttpClient _httpClient;
    private readonly LogService _log;
    private readonly string _settingsPath;
    private string _islandUrl = "http://localhost:5002/notify";
    private bool _noClassIslandDebug = false;

    public string IslandUrl
    {
        get => _islandUrl;
        set => _islandUrl = value;
    }

    public bool NoClassIslandDebug
    {
        get => _noClassIslandDebug;
        set => _noClassIslandDebug = value;
    }

    public NotifyService(LogService log, string? baseDir = null)
    {
        _log = log;
        var dir = baseDir ?? AppContext.BaseDirectory;
        _settingsPath = Path.Combine(dir, "configs", "settings.json");
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(10)
        };
        LoadSettings();
    }

    /// <summary>
    /// 从 configs/settings.json 加载配置。
    /// </summary>
    public void LoadSettings()
    {
        try
        {
            if (File.Exists(_settingsPath))
            {
                var json = File.ReadAllText(_settingsPath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json);
                if (settings != null)
                {
                    if (!string.IsNullOrWhiteSpace(settings.NotifyIslandUrl))
                        _islandUrl = settings.NotifyIslandUrl;
                    _noClassIslandDebug = settings.NoClassIslandDebug;
                    _log.Info($"Notify 配置已加载：URL={_islandUrl}, Debug={_noClassIslandDebug}");
                }
            }
            else
            {
                _log.Info("settings.json 不存在，使用默认 Notify 配置。");
            }
        }
        catch (Exception ex)
        {
            _log.Warning($"加载 Notify 配置失败，使用默认值：{ex.Message}");
        }
    }

    /// <summary>
    /// 保存当前配置到 configs/settings.json。
    /// </summary>
    public void SaveSettings()
    {
        try
        {
            var dir = Path.GetDirectoryName(_settingsPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            AppSettings settings;
            if (File.Exists(_settingsPath))
            {
                var json = File.ReadAllText(_settingsPath);
                settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
            else
            {
                settings = new AppSettings();
            }

            settings.NotifyIslandUrl = _islandUrl;
            settings.NoClassIslandDebug = _noClassIslandDebug;

            var options = new JsonSerializerOptions { WriteIndented = true };
            File.WriteAllText(_settingsPath, JsonSerializer.Serialize(settings, options));
            _log.Info($"Notify 配置已保存：URL={_islandUrl}, Debug={_noClassIslandDebug}");
        }
        catch (Exception ex)
        {
            _log.Error($"保存 Notify 配置失败：{ex.Message}");
        }
    }

    /// <summary>
    /// 向 NotifyIsland 发送通知。
    /// </summary>
    public async Task SendNotificationAsync(NotifyRequest request)
    {
        if (_noClassIslandDebug)
        {
            _log.Info("NO_CLASSISLAND_DEBUG 模式已启用，跳过通知发送。");
            return;
        }

        try
        {
            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(_islandUrl, content);
            _log.Info($"NotifyIsland Status Code: {(int)response.StatusCode}");
            var body = await response.Content.ReadAsStringAsync();
            _log.Info($"NotifyIsland Response Body: {body}");
        }
        catch (Exception ex)
        {
            _log.Error($"发送通知失败：{ex.Message}");
        }
    }

    /// <summary>
    /// 简化版通知发送（兼容 Oxygen3 调用方式）。
    /// </summary>
    public Task SendAsync(string title, double titleDuration, string titleVoice,
        string? content, double contentDuration, string contentVoice)
    {
        var request = new NotifyRequest
        {
            Title = title,
            TitleDuration = titleDuration,
            TitleVoice = titleVoice,
            Content = content,
            ContentDuration = contentDuration,
            ContentVoice = contentVoice
        };
        return SendNotificationAsync(request);
    }

    /// <summary>
    /// 检测 NotifyIsland 服务连通性（/ping）。
    /// </summary>
    public async Task<bool> PingAsync()
    {
        try
        {
            var pingUrl = _islandUrl.Replace("/notify", "/ping");
            var response = await _httpClient.GetAsync(pingUrl);
            var body = await response.Content.ReadAsStringAsync();
            return body.Trim() == "pong";
        }
        catch
        {
            return false;
        }
    }
}
