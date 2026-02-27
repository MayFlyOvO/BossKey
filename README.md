# HideProcess

一个基于 `WPF + .NET 8` 的 Windows 桌面工具，用于通过全局热键快速隐藏/恢复已配置目标程序的窗口。

## 项目结构

- `HideProcess.App`：主程序（UI、托盘、热键、设置）
- `HideProcess.Core`：核心能力库（窗口枚举、隐藏/恢复、热键底层、配置存储）

## 核心功能

- 选择运行中窗口并添加为目标（按进程名/路径匹配）
- 全局热键隐藏/恢复目标窗口
- 支持“同一热键切换模式（Toggle）”
- 恢复窗口时尽量保持原显示状态（含最大化窗口）
- 目标列表持久化，重启后自动恢复
- 设置项导入/导出（JSON）
- 关闭窗口最小化到托盘
- 开机自启（`HKCU\Software\Microsoft\Windows\CurrentVersion\Run`）
- 单实例运行（重复启动会提示）
- 主窗口位置与大小记忆
- 运行日志面板（支持折叠/展开，折叠状态可持久化）
- 中英文切换（简体中文 / English）
- 异常退出检测提示（仅提示，不做全局扫描恢复）

## 使用说明

1. 启动程序后，在“运行中窗口”下拉框选择目标窗口并点击“添加目标”。
2. 在“设置”中配置隐藏/显示热键。
3. 按隐藏热键后，匹配目标窗口会被隐藏（非最小化）。
4. 按显示热键后，已隐藏窗口恢复显示。
5. 若隐藏热键与显示热键相同，程序自动进入 Toggle 模式。

## 设置文件

默认路径：

- `%APPDATA%\HideProcess\settings.json`

主要字段说明：

- `HideHotkey`：隐藏热键
- `ShowHotkey`：显示热键
- `Targets`：目标列表
- `StartWithWindows`：是否开机启动
- `MinimizeToTray`：关闭时是否最小化到托盘
- `IsLogPanelCollapsed`：日志面板是否折叠
- `Language`：界面语言
- `MainWindowPlacement`：主窗口位置/尺寸/状态

运行态文件：

- `%APPDATA%\HideProcess\session.json`：用于记录是否异常退出

## 构建与运行

### 本地调试

```powershell
dotnet build HideProcess.sln
dotnet run --project .\HideProcess.App\HideProcess.App.csproj
```

### Release + 单文件发布

双击根目录脚本：

- `Build-Release.bat`

输出目录：

- `artifacts\build`：Release 构建输出
- `artifacts\singlefile\HideProcess.App.exe`：单文件自包含发布（`win-x64`）

## 兼容性

- 操作系统：Windows 10/11
- 目标框架：`net8.0-windows`
- 单文件版本为自包含发布，无需预装 .NET Runtime

## 注意事项

- 仅处理可枚举的顶层可见窗口（有标题的窗口）。
- 为避免误触，建议热键包含 `Ctrl/Alt/Shift/Win` 修饰键。
- 程序正常退出时会尝试恢复本次由本程序隐藏的窗口。
- 若系统/应用异常退出，程序下次启动会给出提示，由用户自行确认窗口状态。
