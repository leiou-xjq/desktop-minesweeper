# 发布流程

本项目使用 GitHub Actions 在推送 `v*` 标签时自动构建并创建 Release。

## 首次发布

1. 在 GitHub 创建空仓库 `desktop-minesweeper`（不要勾选初始化 README）。
2. 关联远程并推送主分支：

   ```powershell
   cd D:\DesktopMinesweeper
   git remote add origin https://github.com/<你的用户名>/desktop-minesweeper.git
   git push -u origin main
   ```

3. 打开仓库 **Actions**，确认 `build` 工作流在 `main` 上通过。
4. 打标签并推送：

   ```powershell
   git tag -a v1.2.0 -m "Desktop Minesweeper 1.2.0"
   git push origin v1.2.0
   ```

5. 工作流会依次执行：
   - Release 构建
   - 规则与图像自检（`--selftest`）
   - 自包含/框架依赖发布
   - 校验发布出的 exe 能独立运行
   - 安装 Inno Setup 并构建 `setup.exe`
   - 创建 GitHub Release 并附加安装包、zip、exe 与 `.sha256`

## 本地预演

发布前可在本机生成同样的资产：

```powershell
.\publish.ps1
.\installer\build-installer.ps1
Get-ChildItem .\dist -File
```

安装包构建需要 Inno Setup 6（可仅当前用户安装，无需管理员）。

输出：

| 文件 | 说明 |
|---|---|
| `DesktopMinesweeper-<版本>-setup.exe` | 安装包（Inno Setup） |
| `DesktopMinesweeper-<版本>-win-x64.zip` | 自包含 exe + LICENSE + README |
| `DesktopMinesweeper-<版本>-win-x64.exe` | 自包含单文件 |
| `DesktopMinesweeper-<版本>-win-x64-fx.exe` | 需要 .NET 8 Desktop Runtime |
| `*.sha256` | 对应文件的 SHA-256 |

建议在发布前额外运行：

```powershell
.\run.ps1 -SelfTest
.\scripts\verify-exit-safety.ps1 -ExePath .\dist\DesktopMinesweeper-1.2.0-win-x64.exe
```

并做一次安装/卸载冒烟测试：

```powershell
Start-Process .\dist\DesktopMinesweeper-1.2.0-setup.exe -ArgumentList '/VERYSILENT','/CURRENTUSER','/SUPPRESSMSGBOXES','/NORESTART' -Wait
Start-Process "$env:LOCALAPPDATA\Programs\DesktopMinesweeper\unins000.exe" -ArgumentList '/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART' -Wait
```

## 版本号

版本号位于 `src/DesktopMinesweeper/DesktopMinesweeper.csproj` 的 `<Version>`。
打标签时工作流会用标签名覆盖发布版本（`v1.2.3` → `1.2.3`），并写进 exe 的版本信息。
