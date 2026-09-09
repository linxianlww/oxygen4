using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;

namespace Oxygen4;

/// <summary>
/// 自定义消息对话框，UI 风格与主界面一致。
/// 替代 System.Windows.MessageBox，提供统一的圆角卡片、按钮和动画。
/// </summary>
public class CustomDialog : Window
{
    public MessageBoxResult Result { get; private set; } = MessageBoxResult.None;

    private CustomDialog(string title, string message, MessageBoxButton buttons, MessageBoxImage icon)
    {
        Title = title;
        Width = 400;
        Height = 200;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Owner = Application.Current.MainWindow;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        FontFamily = new FontFamily("Microsoft YaHei UI");

        var root = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF)),
            CornerRadius = new CornerRadius(14),
            Effect = new DropShadowEffect
            {
                Color = Colors.Black,
                Opacity = 0.12,
                BlurRadius = 20,
                ShadowDepth = 0
            }
        };

        var panel = new StackPanel { Margin = new Thickness(28) };

        // 标题
        var titleText = new TextBlock
        {
            Text = title,
            FontSize = 17,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(0x11, 0x11, 0x11)),
            Margin = new Thickness(0, 0, 0, 12)
        };
        panel.Children.Add(titleText);

        // 消息内容
        var messageText = new TextBlock
        {
            Text = message,
            FontSize = 13,
            Foreground = new SolidColorBrush(Color.FromRgb(0x5C, 0x5C, 0x6B)),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 24)
        };
        panel.Children.Add(messageText);

        // 按钮区域
        var btnPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };

        if (buttons == MessageBoxButton.OK || buttons == MessageBoxButton.OKCancel)
        {
            btnPanel.Children.Add(CreateButton("确定", true, () =>
            {
                Result = MessageBoxResult.OK;
                Close();
            }));
        }

        if (buttons == MessageBoxButton.OKCancel || buttons == MessageBoxButton.YesNoCancel)
        {
            btnPanel.Children.Add(CreateButton("取消", false, () =>
            {
                Result = MessageBoxResult.Cancel;
                Close();
            }));
        }

        if (buttons == MessageBoxButton.YesNo || buttons == MessageBoxButton.YesNoCancel)
        {
            btnPanel.Children.Add(CreateButton("是", true, () =>
            {
                Result = MessageBoxResult.Yes;
                Close();
            }));
            btnPanel.Children.Add(CreateButton("否", false, () =>
            {
                Result = MessageBoxResult.No;
                Close();
            }));
        }

        panel.Children.Add(btnPanel);
        root.Child = panel;
        Content = root;

        // 淡入动画
        Loaded += (s, e) =>
        {
            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200));
            BeginAnimation(OpacityProperty, fadeIn);
        };
    }

    private Button CreateButton(string text, bool isPrimary, Action onClick)
    {
        var btn = new Button
        {
            Content = text,
            MinWidth = 76,
            Padding = new Thickness(18, 8, 18, 8),
            Margin = new Thickness(8, 0, 0, 0),
            FontSize = 13,
            Cursor = System.Windows.Input.Cursors.Hand,
            BorderThickness = new Thickness(0)
        };

        if (isPrimary)
        {
            btn.Background = new SolidColorBrush(Color.FromRgb(0x11, 0x11, 0x11));
            btn.Foreground = Brushes.White;
            btn.Template = CreateButtonTemplate("#111111", "#2C2C33");
        }
        else
        {
            btn.Background = new SolidColorBrush(Color.FromRgb(0xEF, 0xEF, 0xEF));
            btn.Foreground = new SolidColorBrush(Color.FromRgb(0x11, 0x11, 0x11));
            btn.Template = CreateButtonTemplate("#EFEFEF", "#E2E2E6");
        }

        btn.Click += (s, e) => onClick();
        return btn;
    }

    private ControlTemplate CreateButtonTemplate(string normalColor, string hoverColor)
    {
        var template = new ControlTemplate(typeof(Button));
        var border = new FrameworkElementFactory(typeof(Border));
        border.Name = "border";
        border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(BackgroundProperty));
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(10));
        border.SetValue(Border.PaddingProperty, new TemplateBindingExtension(PaddingProperty));

        var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
        presenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        presenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
        border.AppendChild(presenter);

        template.VisualTree = border;

        var hoverTrigger = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
        hoverTrigger.Setters.Add(new Setter
        {
            TargetName = "border",
            Property = Border.BackgroundProperty,
            Value = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hoverColor))
        });
        template.Triggers.Add(hoverTrigger);

        return template;
    }

    /// <summary>
    /// 显示信息对话框。
    /// </summary>
    public static MessageBoxResult ShowInfo(string message, string title = "提示")
    {
        var dlg = new CustomDialog(title, message, MessageBoxButton.OK, MessageBoxImage.Information);
        dlg.ShowDialog();
        return dlg.Result;
    }

    /// <summary>
    /// 显示确认对话框（是/否）。
    /// </summary>
    public static MessageBoxResult ShowConfirm(string message, string title = "确认")
    {
        var dlg = new CustomDialog(title, message, MessageBoxButton.YesNo, MessageBoxImage.Question);
        dlg.ShowDialog();
        return dlg.Result;
    }

    /// <summary>
    /// 显示错误对话框。
    /// </summary>
    public static MessageBoxResult ShowError(string message, string title = "错误")
    {
        var dlg = new CustomDialog(title, message, MessageBoxButton.OK, MessageBoxImage.Error);
        dlg.ShowDialog();
        return dlg.Result;
    }
}
