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

上述路径均不会改动真实图标。验收方式：退出前后各读取一次 47 个图标的坐标与文本，结果必须完全一致。

## 权限

程序以 `asInvoker` 运行，不请求管理员权限。提权反而可能导致跨完整性级别的窗口消息被 UIPI 拦截。

## 隐私

- 无网络请求、无遥测、无日志上传。
- 不读取图标对应的文件内容，只读取显示名称与屏幕位置。
- 截图仅保存在进程内存中（阶段 1 起按需使用）。
