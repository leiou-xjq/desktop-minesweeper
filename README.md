# Desktop Minesweeper · 桌面扫雷

一个轻量的 Windows 桌面小工具：把桌面图标变成扫雷格子。

> **当前状态：1.2.0（带安装包）。**
> 透明覆盖层、图标锚定网格、固定网格降级、完整扫雷规则、玻璃拟态界面与退出安全均已完成。
> 提供仅当前用户、无需管理员的 `setup.exe` 安装包；便携 zip 与单文件 exe 继续保留。
> 阶段 0 的只读对齐探针保留为 `-Probe` 诊断模式。

[English summary](#english-summary) · [安全边界](docs/safety.md) · [更新记录](CHANGELOG.md)

---

## 它是什么

- 读取桌面图标的位置与文本，在**透明置顶覆盖层**上绘制副本。
- 真实桌面图标**只读**：不移动、不重命名、不隐藏、不修改。
- 以图标槽位生成棋盘网格，每个格子显示该图标的只读副本。
- 完整扫雷规则：左键翻开、右键插旗、数字提示、首击安全、胜负判定。
- 玻璃拟态界面：未翻开格是模糊透出的毛玻璃，已翻开格清晰通透，状态栏为玻璃胶囊。
- 悬停高光、翻开淡入缩放；踩雷时爆炸格抖动、所有地雷脉冲闪烁——动画只作用于覆盖层副本。
- 图标读取失败或数量不足时，自动降级为 10×8 固定网格。
- 状态栏提供剩余雷数、计时、重开与退出。
- Esc 退出或进程被强制结束后，覆盖层由系统销毁，桌面保持原样。
- 提供安装包：开始菜单快捷方式、可选桌面快捷方式、安装后启动、标准卸载。

## 安全承诺

1. 不移动、删除、重命名、隐藏或修改任何真实桌面图标。
2. 不向 `explorer.exe` 发送任何改变图标位置的命令（无 `LVM_SETITEMPOSITION`、`LVM_ARRANGE`、`LVM_SORTITEMS`）。
3. 只发送 `LVM_GET*` 只读消息，并读取图标位置/文本。
4. 程序运行时不写注册表、不注册开机启动、不改系统设置、不请求管理员权限。
5. 无网络请求、无遥测。
6. 覆盖层消失即恢复，不存在“需要还原”的桌面状态。
7. 安装器只写入开始菜单/桌面快捷方式与 HKCU 卸载信息，卸载时清理；不修改桌面图标。

细节与审计说明见 [docs/safety.md](docs/safety.md)。

## 系统要求

- Windows 10 1809+ / Windows 11，x64
- 安装版与便携版：无需安装 .NET（自包含）
- 从源码构建：见下节，本地脚本会自动准备项目内 .NET 8 SDK

## 下载与安装（推荐）

1. 打开 [Releases](https://github.com/leiou-xjq/desktop-minesweeper/releases)。
2. 下载 `DesktopMinesweeper-<版本>-setup.exe`，双击安装。
3. 安装完成页可直接启动；之后从开始菜单启动。
4. 不想安装时，仍可下载 `-win-x64.zip` 解压运行，或直接运行单文件 exe。

安装说明：

- 仅当前用户安装，**不需要管理员权限**，不弹 UAC
- 默认路径：`%LOCALAPPDATA%\Programs\DesktopMinesweeper`，安装时可修改
- 开始菜单会创建程序项与卸载项；桌面快捷方式为可选任务，默认不勾选
- 卸载：设置 → 应用 → Desktop Minesweeper，或开始菜单中的卸载项
- 安装器约 63 MB；便携单文件约 68 MB，内含 .NET 运行时

校验与签名：

- 程序未做代码签名，Windows SmartScreen 可能提示“未知发布者”；可选择“更多信息 → 仍要运行”。
- 校验下载：`Get-FileHash .\DesktopMinesweeper-1.2.0-setup.exe -Algorithm SHA256`
  与同目录 `.sha256` 文件中的值比较。
- 可选的 `-win-x64-fx.exe` 体积约 0.2 MB，需要系统已安装 .NET 8 Desktop Runtime。

## 从源码构建（无需管理员权限）

```powershell
git clone https://github.com/leiou-xjq/desktop-minesweeper.git
cd desktop-minesweeper
.\scripts\setup-sdk.ps1     # 下载 .NET 8 SDK 到 .\.dotnet（不修改 PATH/注册表）
.\build.ps1                 # Release 构建
.\run.ps1                   # 启动
```

制作发布包：

```powershell
.\publish.ps1
```

输出到 `.\dist`：

- `DesktopMinesweeper-<版本>-win-x64.zip`（自包含 exe + LICENSE + README）
- `DesktopMinesweeper-<版本>-win-x64.exe`（自包含单文件）
- `DesktopMinesweeper-<版本>-win-x64-fx.exe`（依赖 .NET 8 Desktop Runtime）
- 对应的 `.sha256` 校验文件

制作安装包（需要 Inno Setup 6，可仅当前用户安装）：

```powershell
.\installer\build-installer.ps1
```

输出 `DesktopMinesweeper-<版本>-setup.exe` 与 `.sha256`；找不到 Inno 时脚本会给出安装提示。

推送 `v*` 标签时，GitHub Actions 会自动构建、运行规则自检、发布这些资产。
详细步骤见 [docs/releasing.md](docs/releasing.md)。

也可以手动运行：

```powershell
.\.dotnet\dotnet.exe run --project .\src\DesktopMinesweeper\DesktopMinesweeper.csproj
```

## 操作

| 操作 | 功能 |
|---|---|
| 左键格子 | 翻开；首次点击保证安全 |
| 右键格子 | 插旗 / 取消旗子 |
| `Esc` | 退出并移除覆盖层（覆盖层有焦点时） |
| `R` | 重开：重新读取图标并开新局 |
| 状态栏“退出” | 始终可用，覆盖层失焦时也可退出 |
| 状态栏“重开” | 重新读取图标并开新局 |

> 提示：点击状态栏可让覆盖层重新获得焦点，之后 `Esc` 即可生效。
> 已翻开的格子不会被右键误标记；旗子保护下的格子不会因左键翻开。

## 阶段 3 / 4 手动验收

1. 运行 `.\run.ps1`。
2. 桌面除图标格子和底部状态栏外应完全可见、可交互。
3. 每个图标上覆盖一张半透明格子，格内是该图标的只读副本。
4. 第一次左键点击必须安全：不会踩雷，且周围一圈（空间足够时）也没有雷。
5. 翻开空格时自动展开相连的空白区域；有雷相邻时显示彩色数字。
6. 右键可插旗/取消旗子；“剩余”数字随旗子数量变化。
7. 翻开所有非雷格子时显示绿色“胜利”提示，所有雷自动插旗。
8. 踩到雷时该格变红并左右抖动，所有地雷脉冲闪烁，提示按 `R` 重开。
9. 计时从第一次翻开开始走动，胜负后停止；按 `R` 或点“重开”重新读图并归零。
10. 退出前后桌面图标位置与名称不变；强制结束进程也同样。
11. 抖动/闪烁期间真实图标保持不动，覆盖层消失后桌面立即恢复原样。

### 退出路径自动验收

```powershell
.\build.ps1
.\scripts\verify-exit-safety.ps1
```

脚本会依次验证：退出按钮/Esc 代码路径、窗口关闭（Alt+F4）、真实 Esc 键、强制结束进程、
未处理异常兜底。每一步都会重新读取 47 个图标坐标与文本并与基线比较，全部一致才 `PASS`。

### 降级验证

```powershell
.\run.ps1 -FixedGrid
```

会跳过图标读取，使用 10×8 固定网格，并在顶部显示“固定网格模式（手动）”。
如果你桌面图标少于 12 个，或 Explorer 重启导致读取失败，也会自动进入该模式。
固定网格同样可以正常游玩。

## 只读诊断 / 对齐探针

```powershell
.\run.ps1 -Probe
```

会为每个桌面图标绘制青色描边与文本标签，用于验证 125% DPI 下的坐标对齐。
只读导出图标坐标与文本：

```powershell
.\.dotnet\dotnet.exe .\src\DesktopMinesweeper\bin\Release\net8.0-windows\DesktopMinesweeper.dll --dump "$env:TEMP\probe.txt"
```

### 规则自检

无需点击即可验证扫雷规则（首击安全、数字、洪水展开、插旗、胜负、重开、稀疏网格）：

```powershell
.\run.ps1 -SelfTest
```

结果会打印到控制台并写入 `%TEMP%\dms-selftest.txt`。

## 技术要点

- C# + WPF + .NET 8，`net8.0-windows`
- Per-Monitor V2 DPI 感知（`app.manifest`）
- 通过 `Progman` / `WorkerW` → `SHELLDLL_DefView` → `SysListView32` 定位桌面图标列表
- 只读 Win32 消息：`LVM_GETITEMCOUNT` / `LVM_GETITEMPOSITION` / `LVM_GETITEMTEXT` / `LVM_GETITEMSPACING`
- 跨进程读取位置/文本需要 `VirtualAllocEx` + `ReadProcessMemory`，缓冲区在 `finally` 中释放
- 实际图标列距可能与 `LVM_GETITEMSPACING` 略有差异（125% DPI 下见过 93 vs 95），网格间距从真实坐标推导，避免累积偏移

## 路线图

- [x] 阶段 0：环境、只读探针、DPI 对齐验证
- [x] 阶段 1：透明覆盖层骨架、状态栏、Esc/按钮退出
- [x] 阶段 2：图标快照与图标锚定网格、固定网格降级
- [x] 阶段 3：扫雷核心（布雷/首击安全/翻格/插旗/胜负/重开）
- [x] 阶段 4：踩雷抖动/闪烁与所有退出路径的安全验收
- [x] 阶段 5：自包含单文件发布、zip/校验和与 GitHub Actions 发布流程

## 已知限制

- 独占全屏游戏之上不会显示覆盖层。
- 当前网格只覆盖主显示器；其他显示器上的图标不会参与游戏（不会崩溃）。
- `Esc` 仅在覆盖层拥有焦点时生效；失焦时请点击状态栏或使用“退出”按钮。
- Explorer 重启后，本次游戏的图标快照不会自动更新，重开游戏即可。
- 首次运行需要允许程序读取桌面窗口信息；安全软件可能对跨进程读取产生提示。
- 单文件版首次启动会把原生库解压到 `%TEMP%`（.NET 单文件机制），不会写入注册表。
- 可执行文件未签名，SmartScreen 可能提示未知发布者；请核对 SHA-256 后再运行。

## 许可证

[MIT](LICENSE) © 2026 leiou-xjq

---

## English summary

Desktop Minesweeper is a lightweight Windows overlay game that turns desktop icons
into Minesweeper cells. It **only reads** icon metadata using Win32 `LVM_GET*`
messages and never moves, renames, hides, or modifies real desktop icons.

The project is at **1.2.0**: a transparent topmost overlay that maps desktop icon
slots to a playable Minesweeper board with flood reveal, flags, first-click safety,
win/loss detection, and cosmetic-only shake/pulse feedback. The board uses a
glassmorphism style: covered cells show blurred icon silhouettes, revealed cells are
crisp, and the status bar is a glass capsule. It falls back to a fixed 10×8 grid when
icon metadata is unavailable. The phase-0 alignment probe is still available via
`-Probe`, `-SelfTest` runs the headless rules and image checks, and
`scripts/verify-exit-safety.ps1` validates every exit path against a before/after
icon dump.

Releases include a per-user `setup.exe` (Inno Setup, no administrator rights) with
Start Menu shortcuts, an optional desktop shortcut, launch-after-install and a clean
uninstaller. Portable self-contained single-file builds and a zip with SHA-256
checksums remain available; tags publish everything to GitHub Releases.
Build with `.\build.ps1`, package with `.\publish.ps1`, compile the installer with
`.\installer\build-installer.ps1`, run with `.\run.ps1`, quit with `Esc` or the Exit
button.

MIT licensed.
