using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using Microsoft.Win32;
using Forms = System.Windows.Forms;
using Oxygen4.Plugins;
using Oxygen4.Services;

namespace Oxygen4;

/// <summary>
/// Oxygen4 应用程序入口。
/// 初始化所有服务、插件、API 服务器、悬浮窗和托盘图标。
/// 作者：咏叹调 Aria
/// 仓库：https://github.com/linxianlww/oxygen4
/// </summary>
public partial class App : System.Windows.Application
{
    public static LogService Logger { get; private set; } = null!;
    public static NamesbookService Namesbook { get; private set; } = null!;
    public static DrawService Draw { get; private set; } = null!;
    public static NotifyService Notify { get; private set; } = null!;
    public static ChartService Chart { get; private set; } = null!;
    public static ApiServer Api { get; private set; } = null!;
    public static PluginHost Plugins { get; private set; } = null!;
    public static FloatingWindow? FloatingWindow { get; set; }
    public static new MainWindow? MainWindow { get; private set; }

    private static Forms.NotifyIcon? _trayIcon;
    private static IServiceProvider? _serviceProvider;
    private static bool _isExiting;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 注册全局异常处理
        DispatcherUnhandledException += App_DispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;

        try
        {
            InitializeServices();
            InitializeTrayIcon();
            RegisterFileAssociations();
            StartServices();
            ShowWindows();
            HandleCommandLineArgs(e);
            CheckNotifyConnectivity();
        }
        catch (Exception ex)
        {
            try { Logger?.Error($"启动失败：{ex}"); } catch { }
            System.Windows.MessageBox.Show($"Oxygen4 启动失败：\n{ex.Message}", "Oxygen4 错误",
                MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    private void InitializeServices()
    {
        var baseDir = AppContext.BaseDirectory;
        Logger = new LogService(baseDir);
        Namesbook = new NamesbookService(baseDir, Logger);
        Draw = new DrawService(Namesbook, Logger);
        Notify = new NotifyService(Logger, baseDir);
        Chart = new ChartService(baseDir, Logger);
        Api = new ApiServer(Namesbook, Draw, Notify, Chart, Logger, baseDir);

        var services = new SimpleServiceProvider();
        services.Register<LogService>(Logger);
        services.Register<NamesbookService>(Namesbook);
        services.Register<DrawService>(Draw);
        services.Register<NotifyService>(Notify);
        services.Register<ChartService>(Chart);
        _serviceProvider = services;

        Plugins = new PluginHost(Logger, _serviceProvider);
        Plugins.RegisterPlugin(new UsbCautionPlugin());
        Plugins.RegisterPlugin(new CctvStreamPlugin());
        Plugins.RegisterPlugin(new ExamplePlugin());
    }

    private void InitializeTrayIcon()
    {
        try
        {
            _trayIcon = new Forms.NotifyIcon
            {
                Visible = true,
                Text = "Oxygen4 - 课堂效率工具"
            };

            try
            {
                var iconUri = new Uri("pack://application:,,,/Resources/favicon.ico");
                var iconStream = GetResourceStream(iconUri)?.Stream;
                if (iconStream != null)
                {
                    _trayIcon.Icon = new System.Drawing.Icon(iconStream);
                }
            }
            catch
            {
                _trayIcon.Icon = System.Drawing.SystemIcons.Application;
            }

            _trayIcon.MouseDoubleClick += (s, e) => ShowMainWindow();

            var menu = new Forms.ContextMenuStrip();
            var openItem = new Forms.ToolStripMenuItem("打开主界面");
            openItem.Click += (s, e) => ShowMainWindow();
            var exitItem = new Forms.ToolStripMenuItem("退出程序");
            exitItem.Click += (s, e) => ExitApplication();
            menu.Items.Add(openItem);
            menu.Items.Add(new Forms.ToolStripSeparator());
            menu.Items.Add(exitItem);
            _trayIcon.ContextMenuStrip = menu;
        }
        catch (Exception ex)
        {
            Logger?.Warning($"托盘图标初始化失败：{ex.Message}");
        }
    }

    /// <summary>
    /// 将 .namesbook 文件关联到本程序（HKCU 注册表，无需管理员权限）。
    /// 注册打开命令与文件图标（使用程序自身图标）。
    /// </summary>
    private void RegisterFileAssociations()
    {
        try
        {
            var exePath = Environment.ProcessPath
                ?? Path.Combine(AppContext.BaseDirectory, "Oxygen4.exe");

            // .namesbook 扩展名 → ProgID
            using (var extKey = Registry.CurrentUser.CreateSubKey(@"Software\Classes\.namesbook"))
            {
                extKey.SetValue("", "Oxygen4.Namesbook");
            }

            // ProgID 描述
            using (var progKey = Registry.CurrentUser.CreateSubKey(@"Software\Classes\Oxygen4.Namesbook"))
            {
                progKey.SetValue("", "Oxygen4 名单文件");
                progKey.SetValue("FriendlyTypeName", "Oxygen4 名单文件");
            }

            // 文件图标（使用程序图标）
            using (var iconKey = Registry.CurrentUser.CreateSubKey(@"Software\Classes\Oxygen4.Namesbook\DefaultIcon"))
            {
                iconKey.SetValue("", $"\"{exePath}\",0");
            }

            // 打开命令
            using (var cmdKey = Registry.CurrentUser.CreateSubKey(@"Software\Classes\Oxygen4.Namesbook\shell\open\command"))
            {
                cmdKey.SetValue("", $"\"{exePath}\" \"%1\"");
            }

            Logger.Info("已注册 .namesbook 文件关联。");
        }
        catch (Exception ex)
        {
            Logger.Warning($"注册文件关联失败：{ex.Message}");
        }
    }

    /// <summary>
    /// 处理命令行参数：检测 .namesbook 文件路径并执行导入流程。
    /// </summary>
    private void HandleCommandLineArgs(StartupEventArgs e)
    {
        var args = e.Args;
        if (args == null || args.Length == 0) return;

        var fileArg = args.FirstOrDefault(a =>
            a.EndsWith(".namesbook", StringComparison.OrdinalIgnoreCase) && File.Exists(a));
        if (fileArg != null)
        {
            // 延迟到窗口显示稳定后执行导入流程
            Dispatcher.BeginInvoke(() => ImportNamesbook(fileArg));
        }
    }

    /// <summary>
    /// 导入 .namesbook 名单文件。
    /// 流程：确认导入 → 同名名单存在时比较 MD5 → 不一致时确认覆盖。
    /// </summary>
    private void ImportNamesbook(string filePath)
    {
        try
        {
            var fileName = Path.GetFileNameWithoutExtension(filePath);
            var configsDir = Path.Combine(AppContext.BaseDirectory, "configs");
            Directory.CreateDirectory(configsDir);
            var targetPath = Path.Combine(configsDir, $"{fileName}.namesbook");

            // 解析文件内容（namesbook 为 base64 编码，每行 "name count"）
            var newContent = DecodeNamesbookContent(filePath);
            if (newContent.Length == 0)
            {
                CustomDialog.ShowInfo("该文件不是有效的 namesbook 名单文件。", "导入名单");
                return;
            }

            if (File.Exists(targetPath))
            {
                var existingContent = DecodeNamesbookContent(targetPath);
                var newMd5 = ComputeMd5(newContent);
                var existingMd5 = ComputeMd5(existingContent);

                if (newMd5 == existingMd5)
                {
                    CustomDialog.ShowInfo($"名单「{fileName}」已存在且内容一致，无需导入。", "导入名单");
                    return;
                }

                var overwrite = CustomDialog.ShowConfirm(
                    $"已存在同名名单「{fileName}」，且内容与当前文件不一致。\n是否覆盖原有名单？", "导入名单");
                if (overwrite != MessageBoxResult.Yes) return;
            }

            File.Copy(filePath, targetPath, true);
            App.Namesbook.ChangeFile(fileName);
            Logger.Info($"名单「{fileName}」已导入到 {targetPath}。");

            CustomDialog.ShowInfo($"名单「{fileName}」已导入并设为当前名单。", "导入成功");
            ShowMainWindow();
            MainWindow?.RefreshAfterImport();
        }
        catch (Exception ex)
        {
            Logger.Error($"导入名单失败：{ex.Message}");
            CustomDialog.ShowError($"导入名单失败：{ex.Message}", "错误");
        }
    }

    /// <summary>
    /// 解码 namesbook 文件为明文内容（用于 MD5 比较）。
    /// </summary>
    private static string DecodeNamesbookContent(string path)
    {
        var sb = new StringBuilder();
        foreach (var line in File.ReadAllLines(path))
        {
            try
            {
                sb.AppendLine(Encoding.UTF8.GetString(Convert.FromBase64String(line.Trim())));
            }
            catch
            {
                sb.AppendLine(line.Trim());
            }
        }
        return sb.ToString();
    }

    private static string ComputeMd5(string input)
    {
        var bytes = MD5.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    /// <summary>
    /// 启动后异步检查 NotifyIsland 服务连通性，不可用时仅提示，不做额外处理。
    /// </summary>
    private async void CheckNotifyConnectivity()
    {
        try
        {
            await Task.Delay(2000); // 等待窗口显示稳定
            var ok = await Notify.PingAsync();
            if (!ok)
            {
                Logger.Warning("NotifyIsland 服务不可用。");
                CustomDialog.ShowInfo(
                    "NotifyIsland 服务当前不可用，通知功能可能无法正常工作。\n" +
                    "请确认 ClassIsland 与 NotifyIsland 插件已启动。", "服务不可用");
            }
            else
            {
                Logger.Info("NotifyIsland 服务连通性检查通过。");
            }
        }
        catch (Exception ex)
        {
            Logger.Warning($"Notify 连通性检查异常：{ex.Message}");
        }
    }

    private void StartServices()
    {
        Logger.Info("Oxygen4 正在启动...");
        Logger.Info($"版本号 {ApiServer.Version} 源码作者 Github @linxianlww (咏叹调 Aria)");
        Logger.Info($"当前使用的 namesbook 文件：{Namesbook.CurrentFile}.namesbook");
        Logger.Info($"仓库地址：https://github.com/linxianlww/oxygen4");

        try
        {
            Api.Start();
        }
        catch (Exception ex)
        {
            Logger.Warning($"API 服务以 + 前缀启动失败，尝试 localhost：{ex.Message}");
            try
            {
                Api.StartWithLocalhost();
            }
            catch (Exception ex2)
            {
                Logger.Error($"API 服务启动失败：{ex2.Message}");
            }
        }

        try
        {
            Plugins.StartAll();
        }
        catch (Exception ex)
        {
            Logger.Error($"插件启动失败：{ex.Message}");
        }
    }

    private void ShowWindows()
    {
        FloatingWindow = new FloatingWindow();
        FloatingWindow.Show();

        MainWindow = new MainWindow();
        MainWindow.Show();
    }

    public static void ShowMainWindow()
    {
        if (MainWindow == null)
        {
            MainWindow = new MainWindow();
        }
        MainWindow.Show();
        MainWindow.WindowState = WindowState.Normal;
        MainWindow.Activate();
        MainWindow.Focus();
    }

    public static void ExitApplication()
    {
        if (_isExiting) return;
        _isExiting = true;

        try
        {
            _trayIcon?.Dispose();
            Api?.Stop();
            Plugins?.StopAll();
            FloatingWindow?.Close();
            if (MainWindow != null)
            {
                MainWindow.AllowClose = true;
                MainWindow.Close();
            }
            Logger?.Info("Oxygen4 已退出。");
        }
        catch { }

        Current.Shutdown(0);
    }

    private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        Logger?.Error($"UI 线程未处理异常：{e.Exception}");
        e.Handled = true;
        System.Windows.MessageBox.Show($"发生未处理的异常：\n{e.Exception.Message}\n\n程序将继续运行。",
            "Oxygen4 异常", MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            Logger?.Error($"非 UI 线程未处理异常：{ex}");
        }
    }

    private void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        Logger?.Error($"任务未观察异常：{e.Exception}");
        e.SetObserved();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try { _trayIcon?.Dispose(); } catch { }
        base.OnExit(e);
    }
}

internal class SimpleServiceProvider : IServiceProvider
{
    private readonly Dictionary<Type, object> _services = new();

    public void Register<T>(T instance) where T : class
    {
        _services[typeof(T)] = instance;
    }

    public object? GetService(Type serviceType)
    {
        return _services.TryGetValue(serviceType, out var instance) ? instance : null;
    }
}
