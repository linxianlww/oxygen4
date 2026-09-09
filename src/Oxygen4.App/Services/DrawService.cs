using Oxygen4.Models;

namespace Oxygen4.Services;

/// <summary>
/// 核心抽选算法服务，对应 Oxygen3 的 oxycore / oxyextra 类。
/// 加权抽选：根据出场次数与冷却状态计算权重，使用指数加权随机选择。
/// </summary>
public class DrawService
{
    // 算法常量（与 Oxygen3 一致）
    public const double PunishBase = 0.1;
    public const double PunishGrowth = 0.02;
    public const double RnaAlpha = 0.6;
    public const int CooldownRounds = 20;

    private readonly NamesbookService _namesbook;
    private readonly LogService _log;
    private readonly Random _random = new();

    public DrawService(NamesbookService namesbook, LogService log)
    {
        _namesbook = namesbook;
        _log = log;
    }

    /// <summary>
    /// 根据出场次数与冷却状态进行加权抽选。
    /// </summary>
    /// <param name="entries">当前名单（会被修改冷却状态）</param>
    /// <param name="excludeIds">排除的索引集合</param>
    /// <returns>选中的索引，未选中返回 -1</returns>
    public int WeightedDraw(List<StudentEntry> entries, HashSet<int>? excludeIds = null)
    {
        excludeIds ??= new HashSet<int>();
        if (entries.Count == 0)
        {
            _log.Error("基础池数据为空，无法进行抽选。");
            return -1;
        }

        var counts = entries.Select(e => e.Count).ToList();
        var maxCount = counts.Max();
        var minCount = counts.Min();
        var diff = maxCount - minCount;
        var punish = PunishBase + PunishGrowth * diff;
        var limit = maxCount + 1;

        // 计算每个成员的分数
        var scores = new double[entries.Count];
        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i].Cooldown == 0 && !excludeIds.Contains(i))
            {
                scores[i] = Math.Pow(limit - entries[i].Count, RnaAlpha);
            }
            else
            {
                scores[i] = 0;
            }
        }

        // 如果所有分数为 0，重置冷却状态并重新计算
        if (scores.All(s => s == 0))
        {
            _log.Warning("所有成员均处于冷却状态，重置冷却状态。");
            for (int i = 0; i < entries.Count; i++)
                entries[i].Cooldown = 0;

            for (int i = 0; i < entries.Count; i++)
            {
                scores[i] = !excludeIds.Contains(i)
                    ? Math.Pow(limit - entries[i].Count, RnaAlpha)
                    : 0;
            }
        }

        // 计算权重（指数加权）
        var weights = new double[entries.Count];
        double weightsSum = 0;
        for (int i = 0; i < entries.Count; i++)
        {
            weights[i] = Math.Exp(scores[i] * punish);
            weightsSum += weights[i];
        }

        if (weightsSum == 0)
        {
            _log.Error("权重总和为零，无法进行抽选。");
            return -1;
        }

        // 归一化并随机选择
        var r = _random.NextDouble() * weightsSum;
        double cumulative = 0;
        for (int i = 0; i < entries.Count; i++)
        {
            cumulative += weights[i];
            if (r <= cumulative)
            {
                return i;
            }
        }
        return entries.Count - 1;
    }

    /// <summary>
    /// 抽选后更新次数并设置冷却，然后持久化到文件。
    /// </summary>
    public void Pushback(List<StudentEntry> entries, int selectedId)
    {
        if (selectedId < 0 || selectedId >= entries.Count)
        {
            _log.Warning("无效的 RNA_ID，无法更新出场次数。");
            return;
        }

        entries[selectedId].Count += 1;
        entries[selectedId].Cooldown = CooldownRounds;

        try
        {
            _namesbook.WriteFile(entries);
            if (!entries[selectedId].IsProtected)
            {
                _log.Info($"成员 {entries[selectedId].Name} 的出场次数已更新为 {entries[selectedId].Count} 次。");
            }
        }
        catch (Exception ex)
        {
            _log.Error($"更新出场次数时发生错误：{ex.Message}");
        }
    }

    /// <summary>
    /// 冷却时间递减。
    /// </summary>
    public void CooldownTick(List<StudentEntry> entries)
    {
        for (int i = 0; i < entries.Count; i++)
        {
            entries[i].Cooldown = Math.Max(0, entries[i].Cooldown - 1);
        }
    }

    /// <summary>
    /// 执行多人不重复抽选。
    /// </summary>
    /// <param name="pcs">抽选人数</param>
    /// <param name="seed">随机种子（null 则随机）</param>
    /// <param name="skipProtected">是否跳过保护标识符（#开头）的输出，但仍计入抽选</param>
    /// <returns>抽中姓名列表</returns>
    public List<string> DrawMultiple(int pcs, int? seed = null, bool skipProtected = true)
    {
        if (seed.HasValue)
        {
            // 使用指定种子
            var rng = new Random(seed.Value);
            // 通过反射设置 _random 的种子不可行，改用本地随机
            return DrawMultipleWithRng(pcs, new Random(seed.Value), skipProtected);
        }
        return DrawMultipleWithRng(pcs, _random, skipProtected);
    }

    private List<string> DrawMultipleWithRng(int pcs, Random rng, bool skipProtected)
    {
        var entries = _namesbook.ReadFile();
        var okName = new List<string>();
        var usedIds = new HashSet<int>();

        while (okName.Count < pcs)
        {
            // 检查可用对象
            var available = entries
                .Select((e, i) => new { e, i })
                .Where(x => x.e.Cooldown == 0 && !usedIds.Contains(x.i))
                .Select(x => x.i)
                .ToList();

            if (available.Count == 0)
            {
                // 重置冷却
                if (entries.Count > 0)
                {
                    _log.Warning("无可用抽选对象，重置冷却状态。");
                    for (int i = 0; i < entries.Count; i++)
                        entries[i].Cooldown = 0;
                }
                available = entries.Select((e, i) => i).Where(i => !usedIds.Contains(i)).ToList();
            }

            if (available.Count == 0)
            {
                _log.Error("所有成员均已被抽选，无法继续抽选。");
                break;
            }

            // 加权抽选（使用当前 rng）
            int selectedId = WeightedDrawWithRng(entries, usedIds, rng);
            if (selectedId < 0) break;

            var selected = entries[selectedId];

            if (skipProtected && selected.IsProtected)
            {
                // 保护标识符：更新次数但不加入结果，继续抽选
                Pushback(entries, selectedId);
                CooldownTick(entries);
                continue;
            }

            if (!usedIds.Contains(selectedId))
            {
                okName.Add(selected.Name);
                usedIds.Add(selectedId);
                Pushback(entries, selectedId);
            }
            CooldownTick(entries);
        }

        return okName;
    }

    private int WeightedDrawWithRng(List<StudentEntry> entries, HashSet<int> excludeIds, Random rng)
    {
        if (entries.Count == 0) return -1;

        var counts = entries.Select(e => e.Count).ToList();
        var maxCount = counts.Max();
        var minCount = counts.Min();
        var diff = maxCount - minCount;
        var punish = PunishBase + PunishGrowth * diff;
        var limit = maxCount + 1;

        var scores = new double[entries.Count];
        for (int i = 0; i < entries.Count; i++)
        {
            scores[i] = (entries[i].Cooldown == 0 && !excludeIds.Contains(i))
                ? Math.Pow(limit - entries[i].Count, RnaAlpha)
                : 0;
        }

        if (scores.All(s => s == 0))
        {
            for (int i = 0; i < entries.Count; i++)
                entries[i].Cooldown = 0;
            for (int i = 0; i < entries.Count; i++)
            {
                scores[i] = !excludeIds.Contains(i)
                    ? Math.Pow(limit - entries[i].Count, RnaAlpha)
                    : 0;
            }
        }

        var weights = new double[entries.Count];
        double weightsSum = 0;
        for (int i = 0; i < entries.Count; i++)
        {
            weights[i] = Math.Exp(scores[i] * punish);
            weightsSum += weights[i];
        }

        if (weightsSum == 0) return -1;

        var r = rng.NextDouble() * weightsSum;
        double cumulative = 0;
        for (int i = 0; i < entries.Count; i++)
        {
            cumulative += weights[i];
            if (r <= cumulative) return i;
        }
        return entries.Count - 1;
    }

    /// <summary>
    /// 获取出场次数最少的成员。
    /// </summary>
    public (string Name, int Count) GetLeastUsed()
    {
        var entries = _namesbook.ReadFile();
        if (entries.Count == 0) return (string.Empty, 0);
        var min = entries.MinBy(e => e.Count)!;
        return (min.Name, min.Count);
    }

    /// <summary>
    /// 计算指定成员的当前权重分数。
    /// </summary>
    public double? GetWeight(List<StudentEntry> entries, int index)
    {
        if (index < 0 || index >= entries.Count) return null;
        if (entries[index].Cooldown > 0) return 0;

        var maxCount = entries.Max(e => e.Count);
        var minCount = entries.Min(e => e.Count);
        var diff = maxCount - minCount;
        var punish = PunishBase + PunishGrowth * diff;
        var limit = maxCount + 1;

        return Math.Pow(limit - entries[index].Count, RnaAlpha) * punish;
    }
}
