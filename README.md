# Oxygen4 - 课堂效率工具

![Oxygen4](https://github.com/linxianlww/oxygen4)

Oxygen4 是一款基于 **.NET 8 / WPF** 开发的课堂效率工具合集，旨在为高效课堂提供新颖且实用的工具。Oxygen4 是 Oxygen3 的全面重构版本，从 Python 完整移植到 C# (.NET) 平台，抛弃了原有的 HTML 基础 UI 层，采用原生 WPF 本地窗口化界面。

> 如果你有好的问题与建议，欢迎提交 issue。
> 该项目系开发者个人作品，由于精力有限，还请包涵。

---

## 与 Oxygen3 的区别

| 特性 | Oxygen3 | Oxygen4 |
|------|---------|---------|
| 开发语言 | Python 3.10+ | C# / .NET 8 |
| UI 技术 | HTML + PyQt5 悬浮窗 | 原生 WPF 窗口（主窗口 + 悬浮窗） |
| Web 框架 | Flask | HttpListener（内置） |
| 图表库 | matplotlib | WPF RenderTargetBitmap |
| 部署方式 | 需安装 Python 环境 | 独立可执行文件，无需运行时 |
| 插件系统 | Python 脚本托管 | C# 插件接口 |

---

## 功能

- **悬浮窗一键随机抽取学生** — 桌面悬浮 widget，点击即抽
- **ClassIsland 通知推送** — 兼容 NotifyIsland API（localhost:5002/notify）
- **随机数据统计图** — 柱状图 + 概率折线图，原生渲染
- **不重复多人抽选** — 加权随机算法，支持冷却机制
- **名单管理** — 多名单切换、新建、重命名、删除、备份
- **C# 插件系统** — 内置 USB 提醒、CCTV 直播源等插件
- **本地 HTTP API** — 5001 端口开放全部接口，支持外部访问
- **日志系统** — 按日期记录运行日志

---

## 特性

- **轻量简洁：** 无需安装 .NET 运行时（自包含发布），下载解压即可使用
- **原生界面：** WPF 本地窗口，流畅响应，无需浏览器
- **简单配置：** 在 ClassIsland 中安装 NotifyIsland 插件即可连接使用
- **可配置性：** 接口开放，可根据接口自行设计客户端及外部访问

---

## 使用方法

0. **编译运行：** 需要 .NET 8 SDK，执行 `dotnet build -c Release` 或直接使用发布包
1. **下载插件：** 安装 ClassIsland，并从插件商店下载 `NotifyIsland` 插件
2. **开启悬浮窗：** 在 NotifyIsland 设置中启用端口（默认 `5002`）
3. **创建班级名单：** 启动 Oxygen4，在主窗口「名单管理」中创建基础池名单
4. **运行程序：** 启动 `Oxygen4.exe`，悬浮窗自动出现，点击中间按钮即可抽选

---

## API 接口文档

Oxygen4 在 `http://localhost:5001` 提供以下 HTTP 接口（与 Oxygen3 完全兼容）：

### 抽选相关
| 接口 | 方法 | 说明 |
|------|------|------|
| `/rna` | GET | 随机抽选，参数 `pcs`（人数）、`seed`（种子） |
| `/rnafromweb` | GET | 抽选并发送 ClassIsland 通知 |
| `/check` | GET | 查看成员出场次数与权重，参数 `RNA_ID` |
| `/less` | GET | 获取出场次数最少的成员 |
| `/chart` | GET | 生成出场次数统计图（PNG） |

### 名单管理
| 接口 | 方法 | 说明 |
|------|------|------|
| `/filesetup` | GET | 创建名单，参数 `names`（用0分隔）、`filename` |
| `/viewfiles` | GET | 查看所有名单文件 |
| `/changefile` | GET | 切换当前名单，参数 `filename` |
| `/currentfile` | GET | 获取当前使用的名单 |
| `/renamefile` | GET | 重命名名单，参数 `filename`、`newname` |
| `/removefile` | GET | 删除名单（保留备份），参数 `filename` |
| `/clearfilebackups` | GET | 清空备份文件 |
| `/resetnamesbook` | GET | 重置出场次数，参数 `key`（密钥） |

### 通知与系统
| 接口 | 方法 | 说明 |
|------|------|------|
| `/msg` | GET | 发送 ClassIsland 通知，参数 `title`、`content` 等 |
| `/status` | GET | 获取服务状态与版本信息 |
| `/kill` | GET | 终止程序 |
| `/miku` | GET | MIKU 彩蛋 |

### NotifyIsland 通知 API
Oxygen4 通过 `POST http://localhost:5002/notify` 向 NotifyIsland 发送通知，完整参数请参考 [NotifyIsland API 文档](https://github.com/linxianlww/NotifyIsland)。

---

## 项目结构

```
Oxygen4/
├── src/Oxygen4.App/          # 主程序
│   ├── App.xaml(.cs)         # 应用入口与服务初始化
│   ├── MainWindow.xaml(.cs)  # 主管理窗口
│   ├── FloatingWindow.xaml   # 悬浮窗
│   ├── Models/                # 数据模型
│   ├── Services/              # 核心服务（API、抽选、通知、图表、日志）
│   ├── Plugins/               # 插件实现
│   └── Resources/             # 图片与图标资源
├── configs/                   # 配置与名单文件
├── logs/                      # 运行日志
├── plugins/                   # 插件目录
└── static/                    # 静态资源
```

---

## 注意事项

- 确保 Oxygen4 及 ClassIsland 以管理员身份运行，否则可能出现端口注册失败的问题
- 确保 NotifyIsland 与程序中的端口设置匹配（默认 5002）
- Oxygen4 的本地 API 监听 5001 端口

---

## 致谢

本项目依赖于以下项目：

- [ClassIsland](https://github.com/HelloWRC/ClassIsland) - 宿主
- [NotifyIsland](https://github.com/linxianlww/NotifyIsland) - API 提供

---

## 开发者

**咏叹调 Aria**

- GitHub: [@linxianlww](https://github.com/linxianlww)
- 仓库: https://github.com/linxianlww/oxygen4

---

## 许可证

本项目遵循 AGPLv3 开源许可。

Copyright © 咏叹调 Aria 2025-2027
