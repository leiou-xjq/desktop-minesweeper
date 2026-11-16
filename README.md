# Desktop Minesweeper · 桌面扫雷

一个轻量的 Windows 桌面小工具：把桌面图标变成扫雷格子。

> **当前状态：阶段 0（只读对齐探针）。** 尚未包含扫雷玩法。
> 阶段 0 仅验证桌面图标读取、DPI 坐标换算、透明置顶覆盖层和安全退出。

[English summary](#english-summary) · [安全边界](docs/safety.md) · [更新记录](CHANGELOG.md)

---

## 它是什么

- 读取桌面图标的位置与文本，在**透明置顶覆盖层**上绘制副本。
- 真实桌面图标**只读**：不移动、不重命名、不隐藏、不修改。
- Esc 退出或进程被强制结束后，覆盖层由系统销毁，桌面保持原样。
- 读取失败时自动降级，不影响桌面使用。

## 安全承诺

1. 不移动、删除、重命名、隐藏或修改任何真实桌面图标。
2. 不向 `explorer.exe` 发送任何改变图标位置的命令（无 `LVM_SETITEMPOSITION`、`LVM_ARRANGE`、`LVM_SORTITEMS`）。
3. 只发送 `LVM_GET*` 只读消息，并读取图标位置/文本。
4. 不写注册表、不注册开机启动、不改系统设置、不请求管理员权限。
5. 无网络请求、无遥测。
6. 覆盖层消失即恢复，不存在“需要还原”的桌面状态。

细节与审计说明见 [docs/safety.md](docs/safety.md)。

## 系统要求

- Windows 10 1809+ / Windows 11，x64
- 运行发布版：无需安装 .NET（自包含版本）
- 从源码构建：见下节，本地脚本会自动准备项目内 .NET 8 SDK

## 从源码构建（无需管理员权限）

```powershell
git clone https://github.com/leiou-xjq/desktop-minesweeper.git
cd desktop-minesweeper
.\scripts\setup-sdk.ps1     # 下载 .NET 8 SDK 到 .\.dotnet（不修改 PATH/注册表）
.\build.ps1                 # Release 构建
.\run.ps1                   # 启动
```

也可以手动：

```powershell
.\.dotnet\dotnet.exe run --project .\src\DesktopMinesweeper\DesktopMinesweeper.csproj
```

## 阶段 0 手动验收

1. 保持桌面图标可见（其他窗口可能遮挡，属正常现象）。
2. 运行 `.\run.ps1`，应看到每个桌面图标外有青色描边格子与文本标签。
3. 125% DPI 下描边应与图标左上角对齐。
4. 按 `Esc`，覆盖层立即消失。
5. 打开任务管理器强制结束 `dotnet` / `DesktopMinesweeper`，覆盖层同样消失。
6. 退出前后桌面图标位置不变。

只读诊断（输出图标坐标与文本）：

```powershell
.\.dotnet\dotnet.exe .\src\DesktopMinesweeper\bin\Release\net8.0-windows\DesktopMinesweeper.dll --dump "$env:TEMP\probe.txt"
```

## 技术要点

- C# + WPF + .NET 8，`net8.0-windows`
- Per-Monitor V2 DPI 感知（`app.manifest`）
- 通过 `Progman` / `WorkerW` → `SHELLDLL_DefView` → `SysListView32` 定位桌面图标列表
- 只读 Win32 消息：`LVM_GETITEMCOUNT` / `LVM_GETITEMPOSITION` / `LVM_GETITEMTEXT` / `LVM_GETITEMSPACING`
- 跨进程读取位置/文本需要 `VirtualAllocEx` + `ReadProcessMemory`，缓冲区在 `finally` 中释放
- 实际图标列距可能与 `LVM_GETITEMSPACING` 略有差异（125% DPI 下见过 93 vs 95），网格间距从真实坐标推导，避免累积偏移

## 路线图

- [x] 阶段 0：环境、只读探针、DPI 对齐验证
- [ ] 阶段 1：透明覆盖层骨架、状态栏、Esc/按钮退出
- [ ] 阶段 2：图标快照与图标锚定网格、降级策略
- [ ] 阶段 3：扫雷核心（布雷/翻格/插旗/胜负/重开）
- [ ] 阶段 4：踩雷抖动与所有退出路径的安全验收
- [ ] 阶段 5：GitHub Actions 发布自包含单文件

## 已知限制

- 独占全屏游戏之上不会显示覆盖层。
- Explorer 重启后，本次游戏的图标快照不会自动更新，重开游戏即可。
- 首次运行需要允许程序读取桌面窗口信息；安全软件可能对跨进程读取产生提示。

## 许可证

[MIT](LICENSE) © 2026 leiou-xjq

---

## English summary

Desktop Minesweeper is a lightweight Windows overlay game that turns desktop icons
into Minesweeper cells. It **only reads** icon metadata using Win32 `LVM_GET*`
messages and never moves, renames, hides, or modifies real desktop icons.

The project is currently at **phase 0**, a read-only alignment probe that validates
icon discovery, per-monitor DPI mapping, a transparent topmost overlay, and safe
exit paths. Build with `.\build.ps1`, run with `.\run.ps1`, quit with `Esc`.

MIT licensed.
