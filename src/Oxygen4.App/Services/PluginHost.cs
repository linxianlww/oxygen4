namespace Oxygen4.Services;

/// <summary>
/// 插件接口，所有 Oxygen4 插件必须实现此接口。
/// 对应 Oxygen3 中每个插件目录下的 main.py。
/// </summary>
public interface IOxygenPlugin
{
    string PluginId { get; }
    string PluginName { get; }
    string Version { get; }
    string Description { get; }

    void Start(IServiceProvider services);
    void Stop();
}

/// <summary>
/// 插件宿主，负责加载、启动和停止所有插件。
/// 对应 Oxygen3 main.py 中扫描 plugins 目录并启动各插件 main.py 的逻辑。
/// </summary>
public class PluginHost
{
    private readonly List<IOxygenPlugin> _plugins = new();
    private readonly LogService _log;
    private readonly IServiceProvider _services;

    public IReadOnlyList<IOxygenPlugin> Plugins => _plugins;

    public PluginHost(LogService log, IServiceProvider services)
    {
        _log = log;
        _services = services;
    }

    public void RegisterPlugin(IOxygenPlugin plugin)
    {
        _plugins.Add(plugin);
        _log.Info($"插件已注册：{plugin.PluginName} v{plugin.Version}");
    }

    public void StartAll()
    {
        foreach (var plugin in _plugins)
        {
            try
            {
                plugin.Start(_services);
                _log.Info($"插件已启动：{plugin.PluginName}");
            }
            catch (Exception ex)
            {
                _log.Error($"插件启动失败 {plugin.PluginName}：{ex.Message}");
            }
        }
    }

    public void StopAll()
    {
        foreach (var plugin in _plugins)
        {
            try
            {
                plugin.Stop();
                _log.Info($"插件已停止：{plugin.PluginName}");
            }
            catch (Exception ex)
            {
                _log.Error($"插件停止失败 {plugin.PluginName}：{ex.Message}");
            }
        }
    }
}
