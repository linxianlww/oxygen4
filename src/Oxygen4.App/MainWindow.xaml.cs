using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using Oxygen4.Models;
using Oxygen4.Services;

namespace Oxygen4;

/// <summary>
/// Oxygen4 主窗口代码逻辑。
/// 提供随机抽选、名单管理、统计图表、通知测试、设置等功能。
/// </summary>
public partial class MainWindow : Window
{
    private ObservableCollection<StudentDisplay> _students = new();
    private List<string> _currentNames = new();
    private readonly Grid[] _pages;
    private string _currentPageTag = "draw";

    public MainWindow()
    {
        InitializeComponent();
        Loaded += MainWindow_Loaded;
        _pages = new[] { PageDraw, PageNames, PageChart, PageNotify, PageSettings, PageAbout };
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        // 默认选中第一个导航项
        NavList.SelectedIndex = 0;
        RefreshNamesbookList();
        RefreshStudentList();
        UpdateStatusText();

        // 加载已保存的设置到 UI
        NotifyIslandUrl.Text = App.Notify.IslandUrl;
        NoClassIslandDebug.IsChecked = App.Notify.NoClassIslandDebug;

        // 窗口淡入动画
        var fadeIn = new System.Windows.Media.Animation.DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(400));
        this.BeginAnimation(OpacityProperty, fadeIn);
    }

    // ---- 侧边栏导航切换 ----

    private void NavList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (NavList.SelectedItem is not ListBoxItem item || item.Tag is not string tag)
            return;

        _currentPageTag = tag;
        Grid? targetPage = tag switch
        {
            "draw" => PageDraw,
            "names" => PageNames,
            "chart" => PageChart,
            "notify" => PageNotify,
            "settings" => PageSettings,
            "about" => PageAbout,
            _ => null
        };

        if (targetPage == null) return;

        foreach (var page in _pages)
        {
            page.Visibility = Visibility.Collapsed;
        }
        targetPage.Visibility = Visibility.Visible;

        // 页面淡入上移动画
        targetPage.RenderTransform = new TranslateTransform(0, 10);
        var fadeIn = new System.Windows.Media.Animation.DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(280));
        var slideUp = new System.Windows.Media.Animation.DoubleAnimation(10, 0, TimeSpan.FromMilliseconds(280))
        {
            EasingFunction = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut }
        };
        targetPage.BeginAnimation(OpacityProperty, fadeIn);
        ((TranslateTransform)targetPage.RenderTransform).BeginAnimation(TranslateTransform.YProperty, slideUp);

        // 切换到图表页时自动刷新
        if (tag == "chart" && ChartImage.Source == null)
        {
            RefreshChart_Click(this, new RoutedEventArgs());
        }
    }

    private void UpdateStatusText()
    {
        CurrentNamesbookText.Text = App.Namesbook.CurrentFile;
        var entries = App.Namesbook.ReadFile();
        StudentCountText.Text = $"{entries.Count} 人";
    }

    // ---- 抽选功能 ----

    private void DrawOne_Click(object sender, RoutedEventArgs e)
    {
        DoDraw(1);
    }

    private void DrawMultiple_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(DrawCount.Text, out var count) || count < 1)
        {
            CustomDialog.ShowInfo("请输入有效的抽选人数（≥1）", "提示");
            return;
        }
        DoDraw(count);
    }

    private void DoDraw(int count)
    {
        int? seed = null;
        if (!string.IsNullOrWhiteSpace(DrawSeed.Text) && int.TryParse(DrawSeed.Text, out var s))
        {
            seed = s;
        }

        var names = App.Draw.DrawMultiple(count, seed, skipProtected: true);
        // 去除保护标识符前缀
        names = names.Select(n => n.StartsWith("#") ? n[1..] : n).ToList();
        _currentNames = names;
        DrawResultList.ItemsSource = names;

        App.Logger.Info($"抽选完成：{string.Join(", ", names)}");

        if (DrawNotify.IsChecked == true && names.Count > 0)
        {
            var title = names.Count == 1 ? names[0] : "批量抽取结果";
            var content = names.Count > 1 ? $"[{string.Join(", ", names)}]" : "";
            _ = App.Notify.SendAsync(title, 3, title, content, names.Count > 1 ? 10 : 0, "");
        }

        UpdateStatusText();
    }

    private void ClearResult_Click(object sender, RoutedEventArgs e)
    {
        _currentNames.Clear();
        DrawResultList.ItemsSource = null;
    }

    // ---- 名单管理 ----

    private void RefreshNamesbookList()
    {
        var files = App.Namesbook.ListFiles();
        NamesbookList.Items.Clear();
        foreach (var f in files)
        {
            var item = new ListBoxItem { Content = f, Tag = f };
            if (f == App.Namesbook.CurrentFile)
                item.FontWeight = FontWeights.Bold;
            NamesbookList.Items.Add(item);
        }
    }

    private void NamesbookList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (NamesbookList.SelectedItem is ListBoxItem item && item.Tag is string filename)
        {
            App.Namesbook.ChangeFile(filename);
            RefreshStudentList();
            UpdateStatusText();
            // 不调用 RefreshNamesbookList()，避免清空列表导致选中项丢失
            // 仅更新现有项的加粗状态
            foreach (ListBoxItem i in NamesbookList.Items)
            {
                i.FontWeight = (i.Tag as string == App.Namesbook.CurrentFile)
                    ? FontWeights.Bold : FontWeights.Normal;
            }
        }
    }

    private void RefreshStudentList()
    {
        var entries = App.Namesbook.ReadFile();
        _students.Clear();
        for (int i = 0; i < entries.Count; i++)
        {
            _students.Add(new StudentDisplay
            {
                Index = i + 1,
                Name = entries[i].DisplayName,
                Count = entries[i].Count,
                Cooldown = entries[i].Cooldown
            });
        }
        StudentListView.ItemsSource = _students;
        NamesbookStatusText.Text = $"当前名单：{App.Namesbook.CurrentFile}.namesbook，共 {entries.Count} 人";
    }

    private void NewNamesbook_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new InputDialog("新建名单", "请输入名单名称：", "");
        if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.InputText))
        {
            App.Namesbook.CreateFile(dialog.InputText, Array.Empty<string>());
            App.Namesbook.ChangeFile(dialog.InputText);
            RefreshNamesbookList();
            RefreshStudentList();
            UpdateStatusText();
        }
    }

    private void RenameNamesbook_Click(object sender, RoutedEventArgs e)
    {
        if (NamesbookList.SelectedItem is not ListBoxItem item || item.Tag is not string oldName)
        {
            CustomDialog.ShowInfo("请先选择要重命名的名单", "提示");
            return;
        }
        var dialog = new InputDialog("重命名名单", "请输入新名称：", oldName);
        if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.InputText))
        {
            if (App.Namesbook.RenameFile(oldName, dialog.InputText))
            {
                RefreshNamesbookList();
                RefreshStudentList();
                UpdateStatusText();
            }
            else
            {
                CustomDialog.ShowError("重命名失败，原文件不存在", "错误");
            }
        }
    }

    private void DeleteNamesbook_Click(object sender, RoutedEventArgs e)
    {
        if (NamesbookList.SelectedItem is not ListBoxItem item || item.Tag is not string name)
        {
            CustomDialog.ShowInfo("请先选择要删除的名单", "提示");
            return;
        }
        var result = CustomDialog.ShowConfirm($"确定要删除名单「{name}」吗？删除后会保留备份。", "确认删除");
        if (result == MessageBoxResult.Yes)
        {
            App.Namesbook.RemoveFile(name);
            RefreshNamesbookList();
            RefreshStudentList();
            UpdateStatusText();
        }
    }

    private void AddStudent_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new InputDialog("添加学生", "请输入学生姓名（多个用空格或换行分隔）：", "");
        if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.InputText))
        {
            var entries = App.Namesbook.ReadFile();
            var names = dialog.InputText.Split(new[] { ' ', '\n', '\r', ',' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var n in names)
            {
                if (!entries.Any(x => x.Name == n))
                {
                    entries.Add(new StudentEntry { Name = n, Count = 0, Cooldown = 0 });
                }
            }
            App.Namesbook.WriteFile(entries);
            RefreshStudentList();
            UpdateStatusText();
        }
    }

    private void DeleteStudent_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is int index)
        {
            var entries = App.Namesbook.ReadFile();
            if (index >= 1 && index <= entries.Count)
            {
                var studentName = entries[index - 1].DisplayName;
                var result = CustomDialog.ShowConfirm($"确定要删除学生「{studentName}」吗？", "确认删除");
                if (result == MessageBoxResult.Yes)
                {
                    entries.RemoveAt(index - 1);
                    App.Namesbook.WriteFile(entries);
                    RefreshStudentList();
                    UpdateStatusText();
                }
            }
        }
    }

    private void ResetCounts_Click(object sender, RoutedEventArgs e)
    {
        var result = CustomDialog.ShowConfirm("确定要重置当前名单所有学生的出场次数吗？", "确认重置");
        if (result == MessageBoxResult.Yes)
        {
            App.Namesbook.ResetAll();
            RefreshStudentList();
            UpdateStatusText();
            App.Logger.Info("名单出场次数已重置。");
        }
    }

    // ---- 统计图表 ----

    private void RefreshChart_Click(object sender, RoutedEventArgs e)
    {
        var entries = App.Namesbook.ReadFile();
        var data = App.Chart.GenerateChart(entries);
        if (data.Length > 0)
        {
            var bi = new BitmapImage();
            using (var ms = new MemoryStream(data))
            {
                bi.BeginInit();
                bi.CacheOption = BitmapCacheOption.OnLoad;
                bi.StreamSource = ms;
                bi.EndInit();
            }
            ChartImage.Source = bi;
        }
    }

    private void SaveChart_Click(object sender, RoutedEventArgs e)
    {
        var entries = App.Namesbook.ReadFile();
        var path = App.Chart.GenerateAndSaveChart(entries);
        if (path != null)
        {
            CustomDialog.ShowInfo($"图表已保存到：{path}", "保存成功");
        }
    }

    // ---- 通知测试 ----

    private async void SendTestNotify_Click(object sender, RoutedEventArgs e)
    {
        var contentType = NotifyContentType.SelectedIndex == 1 ? "rolling" : "simple";
        var request = new NotifyRequest
        {
            Title = NotifyTitle.Text,
            TitleDuration = double.TryParse(NotifyTitleDuration.Text, out var td) ? td : 3,
            TitleVoice = NotifyTitleVoice.Text,
            TitleSpeechEnabled = NotifyTitleSpeech.IsChecked == true,
            Content = NotifyContent.Text,
            ContentDuration = double.TryParse(NotifyContentDuration.Text, out var cd) ? cd : 5,
            ContentType = contentType,
            ContentSpeechEnabled = NotifyContentSpeech.IsChecked == true,
            SpeechEnabled = NotifyTitleSpeech.IsChecked == true || NotifyContentSpeech.IsChecked == true,
            EffectEnabled = NotifyEffect.IsChecked == true,
            SoundEnabled = NotifySound.IsChecked == true
        };
        await App.Notify.SendNotificationAsync(request);
        CustomDialog.ShowInfo("通知已发送", "完成");
    }

    // ---- 设置 ----

    private async void TestNotifyIsland_Click(object sender, RoutedEventArgs e)
    {
        App.Notify.IslandUrl = NotifyIslandUrl.Text;
        var ok = await App.Notify.PingAsync();
        NotifyIslandStatus.Text = ok ? "✓ 连接成功 (pong)" : "✗ 连接失败";
        NotifyIslandStatus.Foreground = ok ? System.Windows.Media.Brushes.Green : System.Windows.Media.Brushes.Red;
    }

    private void SaveSettings_Click(object sender, RoutedEventArgs e)
    {
        App.Notify.IslandUrl = NotifyIslandUrl.Text;
        App.Notify.NoClassIslandDebug = NoClassIslandDebug.IsChecked == true;
        App.Notify.SaveSettings();
        App.Logger.Info($"设置已保存：NotifyIsland URL={NotifyIslandUrl.Text}, Debug={NoClassIslandDebug.IsChecked}");
        CustomDialog.ShowInfo("设置已保存", "完成");
    }

    // ---- 悬浮窗复位 ----

    private void ResetFloating_Click(object sender, RoutedEventArgs e)
    {
        if (App.FloatingWindow != null)
        {
            App.FloatingWindow.ResetPosition();
            App.Logger.Info("悬浮窗已复位到初始位置。");
            CustomDialog.ShowInfo("悬浮窗已恢复到初始位置", "完成");
        }
    }

    // ---- 超链接 ----

    private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(e.Uri.ToString())
            {
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            App.Logger.Error($"打开链接失败：{ex.Message}");
        }
        e.Handled = true;
    }

    /// <summary>
    /// 名单文件导入完成后刷新界面并切换到名单页。
    /// </summary>
    public void RefreshAfterImport()
    {
        RefreshNamesbookList();
        RefreshStudentList();
        UpdateStatusText();

        // 切换到名单管理页
        foreach (var item in NavList.Items)
        {
            if (item is ListBoxItem li && li.Tag as string == "names")
            {
                NavList.SelectedItem = li;
                break;
            }
        }
    }

    // ---- 窗口控制 ----

    /// <summary>
    /// 设为 true 时允许窗口真正关闭（用于程序退出）。
    /// </summary>
    public bool AllowClose { get; set; }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (!AllowClose)
        {
            // 关闭主窗口时不退出应用，最小化到托盘，保持悬浮窗和 API 服务运行
            e.Cancel = true;
            this.Hide();
        }
        base.OnClosing(e);
    }
}

/// <summary>
/// 学生显示模型。
/// </summary>
public class StudentDisplay
{
    public int Index { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Count { get; set; }
    public int Cooldown { get; set; }
}

/// <summary>
/// 简单输入对话框。
/// </summary>
public class InputDialog : Window
{
    public string InputText { get; private set; } = string.Empty;
    private System.Windows.Controls.TextBox _textBox = null!;

    public InputDialog(string title, string prompt, string defaultValue)
    {
        Title = title;
        Width = 360;
        Height = 160;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Owner = Application.Current.MainWindow;
        Background = System.Windows.Media.Brushes.White;
        FontFamily = new System.Windows.Media.FontFamily("Microsoft YaHei UI");

        var panel = new StackPanel { Margin = new Thickness(20) };
        var label = new TextBlock { Text = prompt, Margin = new Thickness(0, 0, 0, 8), FontSize = 12 };
        _textBox = new System.Windows.Controls.TextBox { Text = defaultValue, Margin = new Thickness(0, 0, 0, 16) };
        var btnPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var okBtn = new Button { Content = "确定", Width = 70, Margin = new Thickness(0, 0, 8, 0), Padding = new Thickness(12, 6, 12, 6) };
        var cancelBtn = new Button { Content = "取消", Width = 70, Padding = new Thickness(12, 6, 12, 6) };
        okBtn.Click += (s, e) => { InputText = _textBox.Text; DialogResult = true; };
        cancelBtn.Click += (s, e) => { DialogResult = false; };
        btnPanel.Children.Add(okBtn);
        btnPanel.Children.Add(cancelBtn);
        panel.Children.Add(label);
        panel.Children.Add(_textBox);
        panel.Children.Add(btnPanel);
        Content = panel;
        _textBox.Focus();
        _textBox.SelectAll();
    }
}
