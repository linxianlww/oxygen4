using Oxygen4.Services;

namespace Oxygen4.Plugins;

/// <summary>
/// CCTV 直播源插件：提供获取/展示 CCTV 直播源的方法。
/// 对应 Oxygen3 plugins/CCTVStream/main.py。
/// 作者：咏叹调 Aria
/// </summary>
public class CctvStreamPlugin : IOxygenPlugin
{
    public string PluginId => "CCTVStream";
    public string PluginName => "CCTVStream";
    public string Version => "0.0.1";
    public string Description => "提供一个获取/展示 CCTV 直播源的方法";

    private LogService? _log;

    public void Start(IServiceProvider services)
    {
        _log = services.GetService(typeof(LogService)) as LogService ?? new LogService();
        _log.Info("CCTVStream 模块已挂载。");
        // 插件主循环（与主程序同时启动）
        // 在此处编写插件的主循环
    }

    public void Stop()
    {
        _log?.Info("CCTVStream 插件已停止。");
    }
}
