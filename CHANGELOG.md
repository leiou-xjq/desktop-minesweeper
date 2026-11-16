# Changelog

所有重要变更记录在此文件。格式参考 [Keep a Changelog](https://keepachangelog.com/zh-CN/1.1.0/)。

## [0.3.0] - 2026-11-16

### Added

- 图标槽位网格：由真实图标坐标推导列/行，按图标锚点生成格子。
- 只读图标副本：`PrintWindow` 渲染桌面 ListView，裁剪“图标 + 名称 + 壁纸”的整格缩略图。
- 图标级副本（`LVIR_ICON`）保留给阶段 4 的抖动/闪烁效果。
- 固定网格降级：图标少于 12 个、读取失败或 `--fixed-grid` 时使用 10×8 网格。
- 顶部降级提示徽标；状态栏新增“格子”数量。
- 格子悬停高亮与点击聚焦。
- `run.ps1 -FixedGrid`、`run.ps1 -Probe` 参数。
- `--capture <path>` 诊断截图（以 Per-Monitor V2 运行，便于像素级验收）。

### Fixed

- 显式声明 Per-Monitor V2 DPI 感知，避免 125% 缩放下网格缩小到 80%。
- 格子水平内边距按图标槽位居中计算，避免裁掉名称左侧文字。
- 用整格缩略图替代“半透明格子 + 独立标签”，消除真实图标/标签透出造成的重影。

### Verified

- 网格边界与图标列像素对齐（3/100/197/295/393/491 对应图标槽位）。
- 固定网格为 10×8=80 格，顶部提示可见。
- 退出后 47 个图标坐标/文本与启动前完全一致。
- 阶段 0 探针模式仍可正常对齐。

## [0.2.0] - 2026-11-16

### Added

- 正式透明置顶覆盖层 `OverlayWindow`：只覆盖工作区，任务栏保持可用。
- 底部状态栏：剩余雷数占位、计时、重开（`R`）、退出（`Esc`）。
- 计时器与重开逻辑（阶段 1 仅重置计时）。
- 统一退出守卫 `ExitGuard`：UI 异常、AppDomain 异常、应用退出时关闭所有窗口。
- `-Probe` 诊断模式保留阶段 0 的对齐检查；探针绘制迁至 `ProbeLayer`。
- `run.ps1` 支持直接传递应用参数。

### Changed

- `ProbeWindow` 重构为 `OverlayWindow` + `ProbeLayer`，生产界面默认不含调试描边。
- 覆盖层改为使用主显示器工作区（`rcWork`），避免遮挡任务栏。

### Verified

- 覆盖层为 1920×1030（125% DPI 工作区），除状态栏外完全透明。
- `R`/“重开”将计时归零；`Esc` 与“退出”按钮均结束进程并移除窗口。
- 退出后 47 个图标坐标/文本与启动前完全一致。

## [0.1.0] - 2026-11-16

### Added

- 项目骨架：.NET 8 + WPF，项目内免安装 SDK 引导脚本。
- 只读桌面图标读取：`Progman` / `WorkerW` → `SHELLDLL_DefView` → `SysListView32`。
- `LVM_GETITEMCOUNT` / `LVM_GETITEMPOSITION` / `LVM_GETITEMTEXT` / `LVM_GETITEMSPACING`。
- Per-Monitor V2 DPI 感知与物理/逻辑坐标换算。
- 透明置顶对齐探针：为每个桌面图标绘制描边与文本标签。
- `Esc` 退出；退出或强制终止后桌面图标坐标/文本保持不变（已手动验证 47 项）。
- `--dump <path>` 只读诊断模式。
- 安全边界文档、MIT 许可证、GitHub Actions 构建/发布工作流。

### Notes

- `LVM_GETITEMSPACING` 与实际图标列距可能不一致（125% DPI 下 93 vs 95），网格间距改为从真实坐标推导。
