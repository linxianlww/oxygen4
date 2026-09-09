namespace Oxygen4.Models;

/// <summary>
/// 名单条目：姓名与出场次数。
/// 对应 Oxygen3 中 config.bin 的 "name count" 行。
/// </summary>
public class StudentEntry
{
    public string Name { get; set; } = string.Empty;
    public int Count { get; set; }

    /// <summary>
    /// 冷却剩余轮次。
    /// </summary>
    public int Cooldown { get; set; }

    /// <summary>
    /// 是否为保护标识符（以 # 开头）。
    /// </summary>
    public bool IsProtected => Name.StartsWith("#");

    /// <summary>
    /// 显示用姓名（去除 # 前缀）。
    /// </summary>
    public string DisplayName => IsProtected ? Name[1..] : Name;
}
