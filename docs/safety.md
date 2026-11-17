# 安全边界

本项目的核心约束：**真实桌面永远不被修改。**

## 允许的操作

- 截取桌面/图标区域的画面（仅用于绘制副本）。
- 读取桌面图标的位置与文本。
- 创建透明置顶覆盖层窗口进行渲染。

## 明确禁止的操作

实现代码中不得出现：

- `LVM_SETITEMPOSITION`、`LVM_SETITEMPOSITION32`
- `LVM_ARRANGE`、`LVM_SORTITEMS`、`LVM_SORTITEMSEX`
- `LVM_SETITEM`、`LVM_SETITEMTEXT`、`LVM_DELETEITEM`、`LVM_INSERTITEM`
- `SHChangeNotify`、`SHSetFolderPath`
- 通过 `Shell.Application` 调整图标、自动排列、刷新桌面
- 修改 `desktop.ini` 或任何桌面文件
- 写入注册表、注册开机启动、修改系统 DPI/主题设置
- 向 `explorer.exe` 发送改变窗口/图标状态的命令

## 图标副本与截图

- 只读图标副本通过 `PrintWindow` 渲染桌面 `SysListView32` 获得，不抓取用户正在使用的其他窗口内容。
- 位图仅保存在进程内存中，退出即释放；不会写入桌面或改变文件。
- 只有显式运行诊断命令 `--capture <path>` 时才会写出一张 PNG 截图。

## 游戏状态

- 雷区、旗子、翻开状态只存在于进程内存中，不写入磁盘、不发送给 Explorer。
- 覆盖层关闭或进程被强制结束后，这些状态随进程一起消失。
- 重开只重新读取图标快照并生成新局，依然不会修改真实桌面。
- 踩雷抖动/闪烁是覆盖层内的 WPF 动画，只移动/改变副本图元；真实图标没有对应操作。

## 跨进程读取说明
读取图标位置与文本时，程序会：

1. `OpenProcess` 打开 Explorer 进程（只申请 VM 读/写/操作权限）。
2. `VirtualAllocEx` 在 Explorer 进程内分配一个小缓冲区（约 4 KB）。
3. 用 `SendMessageTimeout`（1 秒超时、`SMTO_ABORTIFHUNG`）请求 ListView 填充结果。
4. `ReadProcessMemory` 读取结果。
5. 在 `finally` 中 `VirtualFreeEx` 并关闭句柄。

该过程不会写入图标数据，不会发送任何 set/arrange/sort 消息。若任何一步失败或超时，程序自动降级为固定网格或只读展示。

## 退出与异常

覆盖层是独立窗口，不持有任何需要恢复的桌面状态：

| 退出方式 | 行为 |
|---|---|
| `Esc` | 关闭覆盖层 |
| 状态栏“退出”按钮 | 关闭覆盖层 |
| 关闭窗口 | 关闭覆盖层 |
| 未处理异常 | 关闭覆盖层后退出 |
| 任务管理器强制结束 | 系统销毁窗口 |
| 进程崩溃 | 系统销毁窗口 |

上述路径均不会改动真实图标。自动化验收：

```powershell
.\scripts\verify-exit-safety.ps1
```

脚本会逐一触发这些退出方式，并在每一步后重新读取图标坐标与文本，与基线逐行比较。
任何一项不一致都会以非零退出码失败。

## 安装器与程序

- 安装器（Inno Setup）会写入开始菜单快捷方式、可选的桌面快捷方式，以及
  `HKCU\Software\Microsoft\Windows\CurrentVersion\Uninstall` 下的卸载信息。
  这是标准安装行为，与“程序运行时安全边界”是两件事。
- 安装器不写入开机启动项、不修改系统设置、不请求管理员权限，
  也不移动/修改任何真实桌面图标（新建的快捷方式除外）。
- 程序本身（无论安装版还是便携版）运行时仍不写注册表、不改桌面图标。
- 卸载会删除安装目录、开始菜单项、桌面快捷方式（若创建过）与卸载注册项。

## 权限

程序以 `asInvoker` 运行，不请求管理员权限。提权反而可能导致跨完整性级别的窗口消息被 UIPI 拦截。
安装器使用 Inno Setup 的 `PrivilegesRequired=lowest`，同样不请求提权。

## 隐私

- 无网络请求、无遥测、无日志上传。
- 不读取图标对应的文件内容，只读取显示名称与屏幕位置。
- 截图仅保存在进程内存中（阶段 1 起按需使用）。

## 发布包

- 自包含单文件 exe 首次启动时由 .NET 宿主把原生库解压到 `%TEMP%\.net\...`；
  这是单文件打包机制，不属于本项目的桌面操作，也不会写注册表或开机启动。
- 便携版不包含安装器，不请求管理员权限；删除文件即可卸载。
- 安装版通过 `setup.exe` 安装到 `%LOCALAPPDATA%\Programs\DesktopMinesweeper`，
  使用标准卸载入口清理。
- 发布资产附带 SHA-256 校验文件，建议运行前核对。
