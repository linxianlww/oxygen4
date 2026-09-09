using Oxygen4.Services;

namespace Oxygen4.Plugins;

/// <summary>
/// 示例插件：当插件加载时打印一条日志消息，作为模板使用。
/// 对应 Oxygen3 plugins/example-plugin/main.py。
/// </summary>
public class ExamplePlugin : IOxygenPlugin
{
    public string PluginId => "examplePlugin";
    public string PluginName => "Sample";
    public string Version => "0.0.1";
    public string Description => "示例插件模板";

    private LogService? _log;

    public void Start(IServiceProvider services)
    {
        _log = services.GetService(typeof(LogService)) as LogService ?? new LogService();
        _log.Info("示例插件已成功加载！");
    }

    public void Stop()
    {
        _log?.Info("示例插件已停止。");
    }
}
