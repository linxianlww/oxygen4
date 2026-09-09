using System.Text.Json.Serialization;

namespace Oxygen4.Models;

/// <summary>
/// NotifyIsland 通知请求模型。
/// 对应 POST http://localhost:5002/notify 的完整请求体。
/// 文档来源：https://github.com/linxianlww/NotifyIsland
/// </summary>
public class NotifyRequest
{
    // ---- 遮罩（标题）参数 ----
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("title_duration")]
    public double TitleDuration { get; set; } = 5;

    [JsonPropertyName("title_voice")]
    public string? TitleVoice { get; set; }

    [JsonPropertyName("title_speech_enabled")]
    public bool TitleSpeechEnabled { get; set; } = false;

    [JsonPropertyName("title_left_icon")]
    public string? TitleLeftIcon { get; set; } = "fluent(\uE9B0)";

    [JsonPropertyName("title_right_icon")]
    public string? TitleRightIcon { get; set; } = "fluent(\uE9B0)";

    [JsonPropertyName("title_has_right_icon")]
    public bool TitleHasRightIcon { get; set; } = false;

    [JsonPropertyName("title_color")]
    public string? TitleColor { get; set; }

    // ---- 正文（内容）参数 ----
    [JsonPropertyName("content")]
    public string? Content { get; set; }

    [JsonPropertyName("content_duration")]
    public double ContentDuration { get; set; } = 5;

    [JsonPropertyName("content_voice")]
    public string? ContentVoice { get; set; }

    [JsonPropertyName("content_speech_enabled")]
    public bool ContentSpeechEnabled { get; set; } = false;

    [JsonPropertyName("content_type")]
    public string ContentType { get; set; } = "simple";

    [JsonPropertyName("rolling_repeat_count")]
    public int RollingRepeatCount { get; set; } = 2;

    [JsonPropertyName("content_color")]
    public string? ContentColor { get; set; }

    // ---- 提醒级参数 ----
    [JsonPropertyName("speech_enabled")]
    public bool SpeechEnabled { get; set; } = false;

    [JsonPropertyName("effect_enabled")]
    public bool EffectEnabled { get; set; } = true;

    [JsonPropertyName("sound_enabled")]
    public bool SoundEnabled { get; set; } = true;

    [JsonPropertyName("topmost")]
    public bool Topmost { get; set; } = false;

    [JsonPropertyName("sound_path")]
    public string? SoundPath { get; set; }
}
