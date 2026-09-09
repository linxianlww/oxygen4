using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Oxygen4.Services;

namespace Oxygen4;

/// <summary>
/// 悬浮窗，对应 Oxygen3 scripts/hover2.py 的 FloatingWindow。
/// 无边框、置顶、可拖拽、半透明背景。
/// 通过拦截 WM_GETMINMAXINFO 阻止贴边最大化（Aero Snap）；
/// 拖拽采用手动位置跟踪（CaptureMouse + MouseMove），
/// 规避 AllowsTransparency 分层窗口下 DragMove() 导致窗口消失的已知问题，
/// 并将窗口位置钳制在屏幕范围内，防止被拖出屏幕。
/// 中间按钮触发随机抽选并发送 ClassIsland 通知，右侧按钮打开主窗口。
/// </summary>
public partial class FloatingWindow : Window
{
    private const int WM_GETMINMAXINFO = 0x0024;
    private const int ScreenMargin = 40; // 窗口至少保留在屏幕内的像素

    // 获取鼠标的屏幕坐标（物理像素）。这是系统级 API，
    // 坐标语义无歧义，不受 WPF GetPosition 坐标系（相对窗口/相对屏幕）
    // 差异影响，是拖拽跟随可靠性的根本保障。
    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    private HwndSource? _hwndSource;

    // 手动拖拽状态
    private bool _isDragging;
    private Point _mouseDownScreenPos; // 按下时鼠标的屏幕坐标（DIP）
    private Point _windowStart;        // 按下时窗口位置（DIP）

    // DPI 缩放（物理像素 → DIP）
    private double _dpiScaleX = 1.0;
    private double _dpiScaleY = 1.0;

    public FloatingWindow()
    {
        InitializeComponent();
        Loaded += FloatingWindow_Loaded;
        SourceInitialized += FloatingWindow_SourceInitialized;

        // 手动拖拽：在 RootBorder 上处理，点击非按钮区域即可拖动
        RootBorder.MouseLeftButtonDown += RootBorder_MouseLeftButtonDown;
        RootBorder.MouseMove += RootBorder_MouseMove;
        RootBorder.MouseLeftButtonUp += RootBorder_MouseLeftButtonUp;
        RootBorder.LostMouseCapture += RootBorder_LostMouseCapture;
    }

    private void FloatingWindow_SourceInitialized(object? sender, EventArgs e)
    {
        _hwndSource = (HwndSource)PresentationSource.FromVisual(this)!;
        _hwndSource.AddHook(WndProc);

        // 记录 DPI 缩放（GetCursorPos 返回物理像素，Left/Top 为 DIP）
        var transform = _hwndSource.CompositionTarget.TransformToDevice;
        _dpiScaleX = transform.M11;
        _dpiScaleY = transform.M22;
    }

    /// <summary>
    /// 拦截 WM_GETMINMAXINFO：仅阻止贴边最大化（Aero Snap）。
    /// 注意：不锁定 ptMinTrackSize / ptMaxTrackSize，
    /// 否则 Windows 在拖动移动窗口时会以该值限制窗口轨迹，导致拖动异常。
    /// </summary>
    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_GETMINMAXINFO)
        {
            var mmi = Marshal.PtrToStructure<MINMAXINFO>(lParam);
            var w = (int)ActualWidth;
            var h = (int)ActualHeight;
            if (w < 1) w = (int)Width;
            if (h < 1) h = (int)Height;

            // 阻止贴边最大化：最大化尺寸固定为当前尺寸
            mmi.PtMaxSize = new POINT(w, h);
            mmi.PtMaxPosition = new POINT(0, 0);
            // 不修改 PtMinTrackSize / PtMaxTrackSize，保证窗口可正常拖动移动

            Marshal.StructureToPtr(mmi, lParam, true);
            handled = true;
        }
        return IntPtr.Zero;
    }

    private void FloatingWindow_Loaded(object sender, RoutedEventArgs e)
    {
        ResetPosition();
    }

    /// <summary>
    /// 恢复悬浮窗到初始位置（屏幕右下角，与 Oxygen3 hover2.py 一致）。
    /// </summary>
    public void ResetPosition()
    {
        var screenWidth = SystemParameters.PrimaryScreenWidth;
        var screenHeight = SystemParameters.PrimaryScreenHeight;
        Left = screenWidth - Width - 30;
        Top = screenHeight - Height - 45;
        // 复位后若处于拖拽状态则终止
        EndDrag();
    }

    // ---- 手动拖拽逻辑（基于屏幕坐标绝对映射，1:1 跟随鼠标） ----

    private void RootBorder_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // 按钮（抽选/设置）会标记事件为 Handled，这里只响应非按钮区域
        if (e.Handled) return;

        _isDragging = true;
        // 用系统 API 获取鼠标屏幕坐标（物理像素，除以 DPI 转 DIP），
        // 与窗口位置 (Left/Top) 同单位，且与窗口移动完全解耦。
        GetCursorPos(out var pt);
        _mouseDownScreenPos = new Point(pt.X / _dpiScaleX, pt.Y / _dpiScaleY);
        _windowStart = new Point(Left, Top);
        RootBorder.CaptureMouse();
        e.Handled = true;
    }

    private void RootBorder_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDragging || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        // 当前鼠标屏幕坐标（DIP，系统 API 获取，绝对可靠）
        GetCursorPos(out var pt);
        var screenPos = new Point(pt.X / _dpiScaleX, pt.Y / _dpiScaleY);

        // 绝对映射：窗口新位置 = 按下时窗口位置 + 鼠标总位移。
        // 屏幕坐标与窗口移动无关，因此 1:1 跟随、无累积误差、无抖动。
        var newLeft = _windowStart.X + (screenPos.X - _mouseDownScreenPos.X);
        var newTop = _windowStart.Y + (screenPos.Y - _mouseDownScreenPos.Y);

        // 钳制在虚拟屏幕范围内，防止窗口被拖出屏幕导致"消失"
        var vsLeft = SystemParameters.VirtualScreenLeft;
        var vsTop = SystemParameters.VirtualScreenTop;
        var vsWidth = SystemParameters.VirtualScreenWidth;
        var vsHeight = SystemParameters.VirtualScreenHeight;

        var minLeft = vsLeft - Width + ScreenMargin;
        var maxLeft = vsLeft + vsWidth - ScreenMargin;
        var minTop = vsTop;
        var maxTop = vsTop + vsHeight - ScreenMargin;

        if (newLeft < minLeft) newLeft = minLeft;
        if (newLeft > maxLeft) newLeft = maxLeft;
        if (newTop < minTop) newTop = minTop;
        if (newTop > maxTop) newTop = maxTop;

        Left = newLeft;
        Top = newTop;
    }

    private void RootBorder_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        EndDrag();
    }

    private void RootBorder_LostMouseCapture(object sender, MouseEventArgs e)
    {
        // 意外丢失鼠标捕获时终止拖拽，避免状态卡死
        EndDrag();
    }

    private void EndDrag()
    {
        _isDragging = false;
        if (RootBorder.IsMouseCaptured)
        {
            RootBorder.ReleaseMouseCapture();
        }
    }

    // ---- 按钮事件 ----

    /// <summary>
    /// 抽选按钮：调用 /rna 接口抽选 1 人，并发送 ClassIsland 通知。
    /// 对应 Oxygen3 hover2.py 的 post_send()。
    /// </summary>
    private async void ActionButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // 按钮反馈动画
            ActionButton.RenderTransform = new ScaleTransform(0.92, 0.92, ActionButton.Width / 2, ActionButton.Height / 2);
            await Task.Delay(80);
            ActionButton.RenderTransform = null;

            // 执行抽选
            var names = App.Draw.DrawMultiple(1, null, skipProtected: true);
            if (names.Count > 0)
            {
                var name = names[0].StartsWith("#") ? names[0][1..] : names[0];
                App.Logger.Info($"悬浮窗抽选结果：{name}");

                // 发送 ClassIsland 通知（与 hover2.py 一致）
                await App.Notify.SendAsync(
                    title: name,
                    titleDuration: 3,
                    titleVoice: name,
                    content: "",
                    contentDuration: 0,
                    contentVoice: "");
            }
        }
        catch (Exception ex)
        {
            App.Logger.Error($"悬浮窗抽选失败：{ex.Message}");
        }
    }

    /// <summary>
    /// 设置按钮：打开主窗口。
    /// </summary>
    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        App.ShowMainWindow();
    }

    /// <summary>
    /// 右键菜单：显示主窗口 / 复位悬浮窗 / 退出程序。
    /// </summary>
    protected override void OnMouseRightButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseRightButtonUp(e);
        var menu = new ContextMenu();
        var showMain = new MenuItem { Header = "显示主窗口" };
        showMain.Click += (s, _) => App.ShowMainWindow();
        var resetItem = new MenuItem { Header = "复位悬浮窗位置" };
        resetItem.Click += (s, _) => ResetPosition();
        var exitItem = new MenuItem { Header = "退出 Oxygen4" };
        exitItem.Click += (s, _) => App.ExitApplication();
        menu.Items.Add(showMain);
        menu.Items.Add(resetItem);
        menu.Items.Add(new Separator());
        menu.Items.Add(exitItem);
        menu.IsOpen = true;
    }

    protected override void OnClosed(EventArgs e)
    {
        if (_hwndSource != null)
        {
            _hwndSource.RemoveHook(WndProc);
            _hwndSource.Dispose();
        }
        base.OnClosed(e);
    }

    // ---- Win32 结构 ----

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;

        public POINT(int x, int y) { X = x; Y = y; }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MINMAXINFO
    {
        public POINT PtReserved;
        public POINT PtMaxSize;
        public POINT PtMaxPosition;
        public POINT PtMinTrackSize;
        public POINT PtMaxTrackSize;
    }
}
