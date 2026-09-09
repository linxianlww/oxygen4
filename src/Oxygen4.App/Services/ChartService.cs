using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Oxygen4.Models;
using IOPath = System.IO.Path;

namespace Oxygen4.Services;

/// <summary>
/// 图表生成服务，对应 Oxygen3 的 /chart 路由（matplotlib 生成柱状图+折线图）。
/// 使用 WPF RenderTargetBitmap 在 STA 线程上渲染 PNG，无需额外依赖。
/// </summary>
public class ChartService
{
    private readonly LogService _log;
    private readonly string _staticDir;

    public ChartService(string? baseDir = null, LogService? log = null)
    {
        var root = baseDir ?? AppContext.BaseDirectory;
        _staticDir = IOPath.Combine(root, "static");
        Directory.CreateDirectory(_staticDir);
        _log = log ?? new LogService(root);
    }

    /// <summary>
    /// 生成成员出场次数统计图（柱状图 + 概率折线图），返回 PNG 字节数组。
    /// </summary>
    public byte[] GenerateChart(List<StudentEntry> entries)
    {
        if (entries.Count == 0)
        {
            _log.Warning("名单为空，无法生成图表。");
            return Array.Empty<byte>();
        }

        byte[] result = Array.Empty<byte>();
        var thread = new Thread(() =>
        {
            try
            {
                result = RenderChart(entries);
            }
            catch (Exception ex)
            {
                _log.Error($"生成图表失败：{ex.Message}");
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        return result;
    }

    private byte[] RenderChart(List<StudentEntry> entries)
    {
        const double width = 1000;
        const double height = 600;
        const double marginLeft = 60;
        const double marginRight = 60;
        const double marginTop = 40;
        const double marginBottom = 120;

        var plotWidth = width - marginLeft - marginRight;
        var plotHeight = height - marginTop - marginBottom;

        // 计算概率分数（与 Oxygen3 /chart 一致）
        var maxCount = entries.Max(e => e.Count);
        var limit = maxCount + 1;
        var scores = new double[entries.Count];
        for (int i = 0; i < entries.Count; i++)
        {
            scores[i] = entries[i].Cooldown == 0
                ? Math.Pow(limit - entries[i].Count, DrawService.RnaAlpha)
                : 0;
        }
        var sumScores = scores.Sum();
        var percentages = scores.Select(s => sumScores != 0 ? Math.Round(s / sumScores * 100, 2) : 0).ToArray();

        // 显示名称（去除 # 前缀）
        var displayNames = entries.Select(e => e.DisplayName).ToArray();

        var canvas = new Canvas
        {
            Width = width,
            Height = height,
            Background = Brushes.White
        };

        // 标题
        var title = new TextBlock
        {
            Text = "成员抽取次数统计",
            FontSize = 16,
            FontWeight = FontWeights.Bold,
            Foreground = Brushes.Black
        };
        Canvas.SetLeft(title, (width - 150) / 2);
        Canvas.SetTop(title, 10);
        canvas.Children.Add(title);

        if (entries.Count == 0)
        {
            return EncodeCanvas(canvas, width, height);
        }

        var maxDrawCount = entries.Max(e => e.Count);
        if (maxDrawCount == 0) maxDrawCount = 1;
        var maxPercent = percentages.Max();
        if (maxPercent == 0) maxPercent = 1;

        var barWidth = plotWidth / entries.Count * 0.6;
        var barGap = plotWidth / entries.Count;

        // Y 轴（左侧 - 抽取次数）
        var yAxisLine = new Line
        {
            X1 = marginLeft,
            Y1 = marginTop,
            X2 = marginLeft,
            Y2 = marginTop + plotHeight,
            Stroke = Brushes.Black,
            StrokeThickness = 1
        };
        canvas.Children.Add(yAxisLine);

        // X 轴
        var xAxisLine = new Line
        {
            X1 = marginLeft,
            Y1 = marginTop + plotHeight,
            X2 = marginLeft + plotWidth,
            Y2 = marginTop + plotHeight,
            Stroke = Brushes.Black,
            StrokeThickness = 1
        };
        canvas.Children.Add(xAxisLine);

        // 右侧 Y 轴（概率）
        var y2AxisLine = new Line
        {
            X1 = marginLeft + plotWidth,
            Y1 = marginTop,
            X2 = marginLeft + plotWidth,
            Y2 = marginTop + plotHeight,
            Stroke = Brushes.Red,
            StrokeThickness = 1
        };
        canvas.Children.Add(y2AxisLine);

        // 绘制柱状图
        for (int i = 0; i < entries.Count; i++)
        {
            var barHeight = (entries[i].Count / (double)maxDrawCount) * plotHeight;
            var x = marginLeft + i * barGap + (barGap - barWidth) / 2;
            var y = marginTop + plotHeight - barHeight;

            var bar = new Rectangle
            {
                Width = barWidth,
                Height = barHeight,
                Fill = Brushes.SkyBlue,
                Stroke = Brushes.SteelBlue,
                StrokeThickness = 0.5
            };
            Canvas.SetLeft(bar, x);
            Canvas.SetTop(bar, y);
            canvas.Children.Add(bar);

            // X 轴标签
            var label = new TextBlock
            {
                Text = displayNames[i],
                FontSize = 9,
                Foreground = Brushes.Black,
                TextAlignment = TextAlignment.Center,
                Width = barGap,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            Canvas.SetLeft(label, marginLeft + i * barGap);
            Canvas.SetTop(label, marginTop + plotHeight + 5);
            canvas.Children.Add(label);

            // 次数标签
            if (entries[i].Count > 0)
            {
                var countLabel = new TextBlock
                {
                    Text = entries[i].Count.ToString(),
                    FontSize = 8,
                    Foreground = Brushes.Blue,
                    TextAlignment = TextAlignment.Center,
                    Width = barWidth
                };
                Canvas.SetLeft(countLabel, x);
                Canvas.SetTop(countLabel, y - 14);
                canvas.Children.Add(countLabel);
            }
        }

        // 绘制折线图（概率 %）
        var polyline = new Polyline
        {
            Stroke = Brushes.Red,
            StrokeThickness = 2
        };
        for (int i = 0; i < entries.Count; i++)
        {
            var px = marginLeft + i * barGap + barGap / 2;
            var py = marginTop + plotHeight - (percentages[i] / maxPercent) * plotHeight * 0.9;
            polyline.Points.Add(new Point(px, py));

            // 数据点
            var dot = new Ellipse
            {
                Width = 6,
                Height = 6,
                Fill = Brushes.Red
            };
            Canvas.SetLeft(dot, px - 3);
            Canvas.SetTop(dot, py - 3);
            canvas.Children.Add(dot);
        }
        canvas.Children.Add(polyline);

        // 图例
        var legendBar = new Rectangle { Width = 16, Height = 12, Fill = Brushes.SkyBlue };
        Canvas.SetLeft(legendBar, marginLeft + 20);
        Canvas.SetTop(legendBar, height - 35);
        canvas.Children.Add(legendBar);
        var legendBarText = new TextBlock { Text = "抽取次数", FontSize = 10, Foreground = Brushes.Blue };
        Canvas.SetLeft(legendBarText, marginLeft + 40);
        Canvas.SetTop(legendBarText, height - 36);
        canvas.Children.Add(legendBarText);

        var legendLine = new Line { X1 = 0, Y1 = 6, X2 = 16, Y2 = 6, Stroke = Brushes.Red, StrokeThickness = 2 };
        Canvas.SetLeft(legendLine, marginLeft + 120);
        Canvas.SetTop(legendLine, height - 30);
        canvas.Children.Add(legendLine);
        var legendLineText = new TextBlock { Text = "当前概率(%)", FontSize = 10, Foreground = Brushes.Red };
        Canvas.SetLeft(legendLineText, marginLeft + 140);
        Canvas.SetTop(legendLineText, height - 36);
        canvas.Children.Add(legendLineText);

        // 轴标签
        var yLabel = new TextBlock { Text = "抽取次数", FontSize = 10, Foreground = Brushes.Blue };
        Canvas.SetLeft(yLabel, 5);
        Canvas.SetTop(yLabel, marginTop + plotHeight / 2 - 20);
        canvas.Children.Add(yLabel);

        var xLabel = new TextBlock { Text = "姓名", FontSize = 10, Foreground = Brushes.Black };
        Canvas.SetLeft(xLabel, (width - 30) / 2);
        Canvas.SetTop(xLabel, height - 20);
        canvas.Children.Add(xLabel);

        return EncodeCanvas(canvas, width, height);
    }

    private byte[] EncodeCanvas(Canvas canvas, double width, double height)
    {
        canvas.Measure(new Size(width, height));
        canvas.Arrange(new Rect(0, 0, width, height));
        canvas.UpdateLayout();

        var rtb = new RenderTargetBitmap((int)width, (int)height, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(canvas);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(rtb));

        using var ms = new MemoryStream();
        encoder.Save(ms);
        return ms.ToArray();
    }

    /// <summary>
    /// 生成图表并保存到 static/attendance.png，返回文件路径。
    /// </summary>
    public string? GenerateAndSaveChart(List<StudentEntry> entries)
    {
        var data = GenerateChart(entries);
        if (data.Length == 0) return null;
        var path = IOPath.Combine(_staticDir, "attendance.png");
        File.WriteAllBytes(path, data);
        _log.Info("成员出场次数统计图已生成并保存。");
        return path;
    }
}
