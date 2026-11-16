# Changelog

所有重要变更记录在此文件。格式参考 [Keep a Changelog](https://keepachangelog.com/zh-CN/1.1.0/)。

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
