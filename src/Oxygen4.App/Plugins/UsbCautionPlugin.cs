using System.Management;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Oxygen4.Services;

namespace Oxygen4.Plugins;

/// <summary>
/// USB 提醒插件：当检测到可移动存储设备时，在屏幕左下角和左侧显示警告图标。
/// 对应 Oxygen3 plugins/USBcaution/main.py。
/// 作者：咏叹调 Aria
/// </summary>
public class UsbCautionPlugin : IOxygenPlugin
{
    public string PluginId => "USBcaution";
    public string PluginName => "USBcaution";
    public string Version => "1.2.0";
    public string Description => "当检测到可移动存储设备时，在屏幕左下角和左侧显示警告图标";

    private LogService? _log;
    private Window? _bottomWindow;
    private Window? _sideWindow;
    private System.Windows.Threading.DispatcherTimer? _timer;
    private int _status = 0;

    public void Start(IServiceProvider services)
    {
        _log = services.GetService(typeof(LogService)) as LogService ?? new LogService();
        _log.Info("USBcaution 插件已挂载。");

        var thread = new Thread(() =>
        {
            CreateWindows();
            _timer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _timer.Tick += (s, e) => CheckUsb();
            _timer.Start();
            System.Windows.Threading.Dispatcher.Run();
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
    }

    private void CreateWindows()
    {
        // 底部警告窗口
        _bottomWindow = new Window
        {
            WindowStyle = WindowStyle.None,
            AllowsTransparency = true,
            Background = Brushes.Transparent,
            Topmost = true,
            ShowInTaskbar = false,
            Width = 100,
            Height = 100,
            IsHitTestVisible = false
        };
        var bottomImg = new Image
        {
            Source = LoadImage("pack://application:,,,/Resources/cau.png"),
            Stretch = Stretch.Uniform,
            Opacity = 0.9
        };
        _bottomWindow.Content = bottomImg;
        var screenWidth = SystemParameters.PrimaryScreenWidth;
        var screenHeight = SystemParameters.PrimaryScreenHeight;
        _bottomWindow.Left = 20;
        _bottomWindow.Top = screenHeight - 120;
        _bottomWindow.Hide();

        // 侧边警告窗口
        _sideWindow = new Window
        {
            WindowStyle = WindowStyle.None,
            AllowsTransparency = true,
            Background = Brushes.Transparent,
            Topmost = true,
            ShowInTaskbar = false,
            Width = 80,
            Height = screenHeight,
            IsHitTestVisible = false
        };
        var sideImg = new Image
        {
            Source = LoadImage("pack://application:,,,/Resources/sidecau.png"),
            Stretch = Stretch.UniformToFill,
            Opacity = 0.7
        };
        _sideWindow.Content = sideImg;
        _sideWindow.Left = 0;
        _sideWindow.Top = 0;
        _sideWindow.Hide();
    }

    private static ImageSource? LoadImage(string uri)
    {
        try
        {
            return new BitmapImage(new Uri(uri));
        }
        catch
        {
            return null;
        }
    }

    private void CheckUsb()
    {
        try
        {
            bool hasRemovable = false;
            // 使用 WMI 查询可移动磁盘
            using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_LogicalDisk WHERE DriveType=2");
            foreach (var _ in searcher.Get())
            {
                hasRemovable = true;
                break;
            }

            if (hasRemovable)
            {
                if (_status == 0)
                {
                    _status = 1;
                    _log?.Info("检测到可移动存储设备，警告窗口已显示。");
                }
                _bottomWindow?.Dispatcher.Invoke(() => _bottomWindow.Show());
                _sideWindow?.Dispatcher.Invoke(() => _sideWindow.Show());
            }
            else
            {
                if (_status == 1)
                {
                    _status = 0;
                    _log?.Info("未检测到可移动存储设备，警告窗口已隐藏。");
                }
                _bottomWindow?.Dispatcher.Invoke(() => _bottomWindow.Hide());
                _sideWindow?.Dispatcher.Invoke(() => _sideWindow.Hide());
            }
        }
        catch (Exception ex)
        {
            _log?.Error($"USB 检测出错：{ex.Message}");
        }
    }

    public void Stop()
    {
        _timer?.Stop();
        _bottomWindow?.Dispatcher.InvokeShutdown();
        _sideWindow?.Dispatcher.InvokeShutdown();
        _log?.Info("USBcaution 插件已停止。");
    }
}
