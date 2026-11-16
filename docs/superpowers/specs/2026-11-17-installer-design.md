# 安装包（setup.exe）设计

日期：2026-11-17
状态：已确认，待实现

## 背景

目前只提供便携 zip 与单文件 exe，用户需要自行解压、找位置、手动创建快捷方式。目标是提供一个标准的 Windows 安装向导，同时保持“无管理员权限、无开机启动、不改系统设置”的项目原则。

安装器会写入快捷方式与 HKCU 卸载信息，这是安装行为的必要部分；**程序运行时**仍然不写注册表、不改桌面图标。

## 目标

- 生成 `DesktopMinesweeper-<版本>-setup.exe`
- 仅当前用户安装，不弹 UAC
- 开始菜单快捷方式 + 可选桌面快捷方式
- 安装完成页可立即启动
- 标准卸载入口，卸载清理文件、快捷方式与注册项
- GitHub Actions 自动构建并附加到 Release
- 应用与安装器共用统一图标

## 非目标

- 不做 MSI、不做企业批量部署
- 不做代码签名（继续在文档中说明 SmartScreen）
- 不做自动更新
- 不改变游戏玩法、安全边界、图标读取逻辑
- 不删除或修改用户的真实桌面图标

## 安装器规格

| 项目 | 值 |
|---|---|
| 工具 | Inno Setup 6 |
| 产物 | `dist/DesktopMinesweeper-<版本>-setup.exe` |
| AppId | 固定 GUID，升级时复用 |
| 默认目录 | `%LOCALAPPDATA%\Programs\DesktopMinesweeper` |
| 权限 | `PrivilegesRequired=lowest` |
| 最低系统 | Windows 10 1809+，x64 |
| 压缩 | LZMA2 / max + solid |
| 向导风格 | modern |
| 安装后启动 | 完成页勾选项，默认勾选 |
| 桌面快捷方式 | 附加任务，默认不勾选 |
| 开始菜单 | 程序项 + 卸载项 |
| 卸载图标 | `{app}\DesktopMinesweeper.exe` |

### 安装内容

```
DesktopMinesweeper.exe   （自包含单文件）
LICENSE
README.md
```

### 升级与卸载

- 同一 AppId，新版本覆盖安装
- 卸载删除安装目录、开始菜单项、可选桌面快捷方式与 HKCU 卸载注册项
- `CloseApplications=yes`，升级/卸载前关闭正在运行的实例

## 图标

- 新增 `assets/app.ico`：256/64/32/16 多尺寸
- 设计：深色圆角底 `#0E1720` + 青色 `#5EEAD4` 地雷矢量
- 应用 exe、快捷方式、安装器统一使用
- csproj 中图标路径为 `..\..\assets\app.ico`（相对于项目目录）
- 提供 `scripts/make-icon.ps1` 以便重新生成

## 构建

新增 `installer/build-installer.ps1`：

1. 读取版本号（参数优先，否则读 csproj）
2. 确认 `dist\win-x64\DesktopMinesweeper.exe` 存在
3. 查找 `ISCC.exe`：
   - `%LOCALAPPDATA%\Programs\Inno Setup 6\ISCC.exe`
   - `%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe`
   - `PATH`
4. 调用 `ISCC.exe /DAppVersion=<版本> installer\desktop-minesweeper.iss`
5. 校验产物并生成 `.sha256`

找不到 Inno 时给出明确安装提示，不以模糊错误退出。

## CI

在现有 `build` 工作流中新增：

```yaml
- name: Install Inno Setup
  run: choco install innosetup --no-progress -y

- name: Build installer
  shell: pwsh
  run: .\installer\build-installer.ps1
```

Release 附件沿用现有 glob：`dist/*.exe` 与 `dist/*.sha256` 会自动包含 setup 与其校验文件。

## 安全与文档

- `docs/safety.md` 增加“安装器 vs 程序”说明：
  - 安装器写入快捷方式与 HKCU 卸载项，这是标准安装行为
  - 程序运行时不写注册表、不改桌面图标、不注册开机启动
- README 安装章节：
  - 推荐下载 `setup.exe`；便携 zip 仍保留
  - 说明无管理员权限、安装路径、卸载方式
  - SmartScreen 与 SHA-256 校验提示

## 验证

### 自动

- `--selftest`：39 项全部通过（安装后的 exe）
- `scripts/verify-exit-safety.ps1`：5/5 路径通过
- 构建脚本对产物做 SHA-256

### 安装/卸载

1. 静默安装：`setup.exe /VERYSILENT /CURRENTUSER /TASKS=desktopicon`
2. 检查：
   - `%LOCALAPPDATA%\Programs\DesktopMinesweeper\DesktopMinesweeper.exe` 存在
   - 开始菜单快捷方式存在
   - 桌面快捷方式存在
   - HKCU 卸载项存在
3. 启动安装后的 exe，确认可玩
4. 静默卸载：`unins000.exe /VERYSILENT`
5. 检查文件、快捷方式、卸载项全部移除
6. 安装/卸载前后读取 47 个桌面图标坐标与文本，必须一致

## 改动文件

- `assets/app.ico`（新增）
- `scripts/make-icon.ps1`（新增）
- `installer/desktop-minesweeper.iss`（新增）
- `installer/build-installer.ps1`（新增）
- `src/DesktopMinesweeper/DesktopMinesweeper.csproj`（`ApplicationIcon`，版本 1.2.0）
- `.github/workflows/build.yml`
- `README.md`、`CHANGELOG.md`、`docs/safety.md`、`docs/releasing.md`

## 验收标准

1. setup.exe 在无管理员权限下完成安装
2. 开始菜单/卸载项正确，桌面快捷方式遵循勾选
3. 完成页可立即启动游戏
4. 卸载完整清理，无残留快捷方式
5. 47 个真实桌面图标在安装/卸载前后完全一致
6. 安装后的 exe 通过规则自检与退出安全验收
7. Release 同时提供 setup.exe、便携 zip、单文件 exe 及 SHA-256
