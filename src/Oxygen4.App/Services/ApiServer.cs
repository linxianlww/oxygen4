using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Oxygen4.Models;

namespace Oxygen4.Services;

/// <summary>
/// HTTP API 服务器，监听 0.0.0.0:5001，完整移植 Oxygen3 的所有路由。
/// 使用 HttpListener 实现，支持 CORS 和 OPTIONS 预检。
/// </summary>
public class ApiServer
{
    public const string Version = "4.0.260909 (Tetra)";
    public const string VersionCode = "20260909";
    private const string ResetAuthHash = "71b72a4634334ae3b09c14d6761d288b"; // 与 Oxygen3 一致

    private readonly HttpListener _listener = new();
    private readonly NamesbookService _namesbook;
    private readonly DrawService _draw;
    private readonly NotifyService _notify;
    private readonly ChartService _chart;
    private readonly LogService _log;
    private readonly string _baseDir;
    private CancellationTokenSource? _cts;
    private Task? _listenTask;

    public bool IsRunning { get; private set; }

    public ApiServer(NamesbookService namesbook, DrawService draw, NotifyService notify,
        ChartService chart, LogService log, string? baseDir = null)
    {
        _namesbook = namesbook;
        _draw = draw;
        _notify = notify;
        _chart = chart;
        _log = log;
        _baseDir = baseDir ?? AppContext.BaseDirectory;
        // 前缀在 Start() 中添加，以便支持降级到 localhost
    }

    public void Start()
    {
        if (IsRunning) return;
        _listener.Prefixes.Clear();
        _listener.Prefixes.Add("http://+:5001/");
        _cts = new CancellationTokenSource();
        _listener.Start();
        IsRunning = true;
        _listenTask = Task.Run(() => ListenLoop(_cts.Token));
        _log.Info("Oxygen4 API 服务已启动，监听端口 5001（+ 前缀）。");
    }

    /// <summary>
    /// 以 localhost 前缀启动（降级模式，无需管理员权限）。
    /// </summary>
    public void StartWithLocalhost()
    {
        if (IsRunning) return;
        _listener.Prefixes.Clear();
        _listener.Prefixes.Add("http://localhost:5001/");
        _cts = new CancellationTokenSource();
        _listener.Start();
        IsRunning = true;
        _listenTask = Task.Run(() => ListenLoop(_cts.Token));
        _log.Info("Oxygen4 API 服务已启动，监听端口 5001（localhost 降级模式）。");
    }

    public void Stop()
    {
        if (!IsRunning) return;
        _cts?.Cancel();
        _listener.Stop();
        IsRunning = false;
        _log.Info("Oxygen4 API 服务已停止。");
    }

    private async Task ListenLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var context = await _listener.GetContextAsync();
                _ = Task.Run(() => HandleRequest(context), ct);
            }
            catch (OperationCanceledException) { break; }
            catch (HttpListenerException) { break; }
            catch (Exception ex)
            {
                _log.Error($"API 监听异常：{ex.Message}");
            }
        }
    }

    private void HandleRequest(HttpListenerContext context)
    {
        var request = context.Request;
        var response = context.Response;

        try
        {
            // CORS 头
            response.Headers["Access-Control-Allow-Origin"] = "*";
            response.Headers["Access-Control-Allow-Methods"] = "GET, POST, OPTIONS";
            response.Headers["Access-Control-Allow-Headers"] = "Content-Type, Authorization";

            if (request.HttpMethod == "OPTIONS")
            {
                response.StatusCode = 200;
                response.Close();
                return;
            }

            var path = request.Url?.AbsolutePath ?? "/";
            var query = request.QueryString;

            _log.Info($"API 请求：{request.HttpMethod} {path}{request.Url?.Query}");

            switch (path)
            {
                case "/":
                case "/home":
                    SendText(response, "<html><body><h1>Oxygen4 API</h1><p>Version: " + Version + "</p></body></html>", "text/html");
                    break;

                case "/filesetup":
                    HandleFileSetup(query, response);
                    break;

                case "/viewfiles":
                    HandleViewFiles(response);
                    break;

                case "/changefile":
                    HandleChangeFile(query, response);
                    break;

                case "/currentfile":
                    HandleCurrentFile(response);
                    break;

                case "/renamefile":
                    HandleRenameFile(query, response);
                    break;

                case "/removefile":
                    HandleRemoveFile(query, response);
                    break;

                case "/clearfilebackups":
                    HandleClearBackups(response);
                    break;

                case "/rna":
                    HandleRna(query, response);
                    break;

                case "/resetnamesbook":
                    HandleResetNamesbook(query, response);
                    break;

                case "/check":
                    HandleCheck(query, response);
                    break;

                case "/less":
                    HandleLess(response);
                    break;

                case "/status":
                    HandleStatus(response);
                    break;

                case "/msg":
                    HandleMsg(query, response);
                    break;

                case "/chart":
                    HandleChart(response);
                    break;

                case "/rnafromweb":
                    HandleRnaFromWeb(query, response);
                    break;

                case "/kill":
                    HandleKill(response);
                    break;

                case "/settings":
                    SendText(response, "<html><body><h1>Oxygen4 设置</h1><p>请使用 Oxygen4 主窗口进行设置。</p></body></html>", "text/html");
                    break;

                case "/charthtml":
                    SendText(response, "<html><body><h1>Oxygen4 图表</h1><img src='/chart' /></body></html>", "text/html");
                    break;

                case "/miku":
                    HandleMiku(response);
                    break;

                default:
                    SendJson(response, new { error = "Not Found" }, 404);
                    break;
            }
        }
        catch (Exception ex)
        {
            _log.Error($"API 处理异常：{ex.Message}");
            try
            {
                SendJson(response, new { error = ex.Message }, 500);
            }
            catch { }
        }
        finally
        {
            response.Close();
        }
    }

    // ---- 路由处理方法 ----

    private void HandleFileSetup(System.Collections.Specialized.NameValueCollection query, HttpListenerResponse response)
    {
        var namesStr = query["names"] ?? "";
        var names = namesStr.Split('0', StringSplitOptions.RemoveEmptyEntries);
        var filename = query["filename"] ?? DateTime.Now.ToString("yyyyMMddHHmmss");
        _namesbook.CreateFile(filename, names);
        SendJson(response, new { code = "200", status = "success", message = $"名单已创建成功，文件名为{filename}。" });
    }

    private void HandleViewFiles(HttpListenerResponse response)
    {
        var files = _namesbook.ListFiles();
        _log.Info($"已通过路由获取已有的基础池文件列表：[{string.Join(", ", files)}]。");
        SendJson(response, new { code = "200", status = "success", data = new { files } });
    }

    private void HandleChangeFile(System.Collections.Specialized.NameValueCollection query, HttpListenerResponse response)
    {
        var filename = query["filename"] ?? "default";
        _namesbook.ChangeFile(filename);
        SendJson(response, new { code = "200", status = "success", message = $"当前使用的基础池已切换为 {filename}.namesbook。" });
    }

    private void HandleCurrentFile(HttpListenerResponse response)
    {
        _log.Info($"已通过路由获取当前使用的基础池文件：{_namesbook.CurrentFile}。");
        SendJson(response, new { code = "200", status = "success", data = new { current_file = _namesbook.CurrentFile } });
    }

    private void HandleRenameFile(System.Collections.Specialized.NameValueCollection query, HttpListenerResponse response)
    {
        var oldName = query["filename"] ?? "";
        var newName = query["newname"] ?? "";
        if (!_namesbook.RenameFile(oldName, newName))
        {
            SendJson(response, new { error = "原文件不存在" }, 400);
            return;
        }
        SendJson(response, new { code = "200", status = "success", message = $"文件已重命名为 {newName}.namesbook。" });
    }

    private void HandleRemoveFile(System.Collections.Specialized.NameValueCollection query, HttpListenerResponse response)
    {
        var filename = query["filename"] ?? "";
        if (!_namesbook.RemoveFile(filename))
        {
            SendJson(response, new { error = "文件不存在" }, 400);
            return;
        }
        SendJson(response, new { code = "200", status = "success", message = $"基础池 {filename}.namesbook 已被删除。" });
    }

    private void HandleClearBackups(HttpListenerResponse response)
    {
        _namesbook.ClearBackups();
        SendJson(response, new { code = "200", status = "success", message = "所有备份已被删除。" });
    }

    private void HandleRna(System.Collections.Specialized.NameValueCollection query, HttpListenerResponse response)
    {
        var pcs = int.TryParse(query["pcs"], out var p) ? p : 1;
        var seed = int.TryParse(query["seed"], out var s) ? s : new Random().Next(1, 1000000);
        if (pcs < 1)
        {
            SendJson(response, new { error = "pcs 参数必须大于等于 1" }, 400);
            return;
        }
        var names = _draw.DrawMultiple(pcs, seed, skipProtected: true);
        // 去除保护标识符前缀
        names = names.Select(n => n.StartsWith("#") ? n[1..] : n).ToList();
        SendJson(response, new { code = "200", status = "success", data = new { name = names, seed, pcs } });
    }

    private void HandleResetNamesbook(System.Collections.Specialized.NameValueCollection query, HttpListenerResponse response)
    {
        var key = query["key"] ?? "0";
        var md5 = MD5.HashData(Encoding.UTF8.GetBytes(key));
        var hash = Convert.ToHexString(md5).ToLowerInvariant();
        if (hash != ResetAuthHash)
        {
            SendJson(response, new { _403 = "", error = "密钥错误，无法执行重置操作。" }, 403);
            return;
        }
        _namesbook.ResetAll();
        SendJson(response, new { _200 = "", code = "200", status = "success", message = "名单已重置为初始状态。" });
    }

    private void HandleCheck(System.Collections.Specialized.NameValueCollection query, HttpListenerResponse response)
    {
        var entries = _namesbook.ReadFile();
        var rnaId = int.TryParse(query["RNA_ID"], out var id) ? id : 0;
        var num = rnaId - 1;

        if (num == -1)
        {
            SendJson(response, new
            {
                code = "200",
                status = "success",
                data = new
                {
                    names = entries.Select(e => e.Name).ToArray(),
                    times = entries.Select(e => e.Count).ToArray()
                }
            });
        }
        else if (num < -1 || num >= entries.Count)
        {
            SendJson(response, new { error = "RNA_ID 参数无效" }, 400);
        }
        else
        {
            var weight = _draw.GetWeight(entries, num);
            SendJson(response, new
            {
                code = "200",
                status = "success",
                data = new
                {
                    name = entries[num].Name,
                    time = entries[num].Count,
                    weight = weight ?? 0
                }
            });
        }
    }

    private void HandleLess(HttpListenerResponse response)
    {
        var (name, count) = _draw.GetLeastUsed();
        SendJson(response, new { code = "200", status = "success", data = new { name, time = count } });
    }

    private void HandleStatus(HttpListenerResponse response)
    {
        _log.Info($"已通过路由获取当前服务状态信息。当前版本：{Version}");
        SendJson(response, new
        {
            code = "200",
            status = "success",
            data = new
            {
                copyright = "咏叹调 Aria © 2025-2027",
                version = Version,
                author = "咏叹调 Aria"
            }
        });
    }

    private async void HandleMsg(System.Collections.Specialized.NameValueCollection query, HttpListenerResponse response)
    {
        _log.Info("收到消息通知请求，正在处理...");
        var title = query["title"] ?? "通知";
        var titleDuration = int.TryParse(query["title_duration"], out var td) ? td : 3;
        var titleVoice = query["title_voice"] ?? "";
        var content = query["content"] ?? "";
        var contentDuration = int.TryParse(query["content_duration"], out var cd) ? cd : 0;
        var contentVoice = query["content_voice"] ?? "";

        await _notify.SendAsync(title, titleDuration, titleVoice, content, contentDuration, contentVoice);
        _log.Info("消息通知请求处理完毕，通知已发送。");
        SendText(response, "", "text/html");
    }

    private void HandleChart(HttpListenerResponse response)
    {
        var entries = _namesbook.ReadFile();
        var data = _chart.GenerateChart(entries);
        if (data.Length == 0)
        {
            SendJson(response, new { error = "图表生成失败" }, 500);
            return;
        }
        response.ContentType = "image/png";
        response.ContentLength64 = data.Length;
        response.Headers["Content-Disposition"] = "inline; filename=\"attendance.png\"";
        response.OutputStream.Write(data, 0, data.Length);
        _log.Info("成员出场次数统计图已生成并返回。");
    }

    private async void HandleRnaFromWeb(System.Collections.Specialized.NameValueCollection query, HttpListenerResponse response)
    {
        var pcs = int.TryParse(query["pcs"], out var p) ? p : 1;
        var seed = int.TryParse(query["seed"], out var s) ? s : new Random().Next(1, 1000000);
        if (pcs < 1)
        {
            SendJson(response, new { error = "pcs 参数必须大于等于 1" }, 400);
            return;
        }
        var names = _draw.DrawMultiple(pcs, seed, skipProtected: false);
        // 去除保护标识符
        names = names.Select(n => n.StartsWith("#") ? n[1..] : n).ToList();
        var outprint = string.Join("\n", names).Replace(",", "\n").Replace("[", "").Replace("]", "");
        _log.Info($"通过网页请求完成抽选。结果：{outprint}。");
        await _notify.SendAsync("批量抽取结果", 2, "", $"[{string.Join(", ", names)}]", 10, "");
        SendText(response, "", "text/html");
    }

    private void HandleKill(HttpListenerResponse response)
    {
        SendJson(response, new { _200 = "", code = "200", status = "success", message = "Oxygen4 正在退出..." });
        _log.Info("收到 /kill 请求，正在退出程序。");
        // 在另一个线程延迟退出，确保响应已发送
        Task.Run(async () =>
        {
            await Task.Delay(500);
            Environment.Exit(0);
        });
    }

    private void HandleMiku(HttpListenerResponse response)
    {
        var mikuPath = Path.Combine(_baseDir, "MIKU");
        if (File.Exists(mikuPath))
        {
            var lines = File.ReadAllLines(mikuPath);
            _log.Log("Printing MIKU...", "INFO");
            foreach (var line in lines)
                Console.WriteLine(line);
        }
        SendJson(response, new { message = "HATSUNE MIKU" });
    }

    // ---- 辅助方法 ----

    private static void SendJson(HttpListenerResponse response, object data, int statusCode = 200)
    {
        var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
        var bytes = Encoding.UTF8.GetBytes(json);
        response.StatusCode = statusCode;
        response.ContentType = "application/json; charset=utf-8";
        response.ContentLength64 = bytes.Length;
        response.OutputStream.Write(bytes, 0, bytes.Length);
    }

    private static void SendText(HttpListenerResponse response, string text, string contentType = "text/plain")
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        response.StatusCode = 200;
        response.ContentType = $"{contentType}; charset=utf-8";
        response.ContentLength64 = bytes.Length;
        response.OutputStream.Write(bytes, 0, bytes.Length);
    }
}
