using System.Text.Json.Serialization;

namespace Oxygen4.Models;

/// <summary>
/// 应用设置，对应 configs/settings.json。
/// </summary>
public class AppSettings
{
    [JsonPropertyName("file")]
    public string File { get; set; } = "default";

    [JsonPropertyName("language")]
    public string Language { get; set; } = "zh-cn";

    [JsonPropertyName("notify_island_url")]
    public string NotifyIslandUrl { get; set; } = "http://localhost:5002/notify";

    [JsonPropertyName("no_class_island_debug")]
    public bool NoClassIslandDebug { get; set; } = false;
}
