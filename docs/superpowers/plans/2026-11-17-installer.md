# setup.exe 安装包实现计划（v1.2.0）

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 用 Inno Setup 生成仅当前用户、无需管理员、带快捷方式/卸载/安装后启动的 `setup.exe`，并接入 GitHub Actions 发布。

**Architecture:** 复用 `publish.ps1` 产出的自包含 exe；新增应用图标生成脚本、Inno 脚本与编译脚本；CI 安装 Inno 后编译并随现有 Release 附件发布。程序运行时的安全边界不变。

**Tech Stack:** Inno Setup 6、PowerShell 5.1、System.Drawing（图标生成）、GitHub Actions（Chocolatey）。

---

## 文件结构

| 文件 | 职责 |
|---|---|
| `assets/app.ico` | 应用/安装器图标（生成产物，提交） |
| `scripts/make-icon.ps1` | 生成多尺寸 ICO |
| `installer/desktop-minesweeper.iss` | Inno 安装脚本 |
| `installer/build-installer.ps1` | 查找 ISCC、编译、校验和 |
| `src/DesktopMinesweeper/DesktopMinesweeper.csproj` | `ApplicationIcon`、版本 1.2.0 |
| `.github/workflows/build.yml` | 安装 Inno、编译 setup、发布 |
| `README.md` / `CHANGELOG.md` / `docs/safety.md` / `docs/releasing.md` | 文档 |

---

### Task 1: 应用图标

**Files:**
- Create: `scripts/make-icon.ps1`
- Create: `assets/app.ico`（脚本产物）
- Modify: `src/DesktopMinesweeper/DesktopMinesweeper.csproj`

- [ ] **Step 1: 写图标生成脚本**

`scripts/make-icon.ps1`：

```powershell
#requires -Version 5.1
param(
    [string]$OutFile = (Join-Path (Split-Path $PSScriptRoot -Parent) 'assets\app.ico')
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

function New-RoundedPath([float]$x, [float]$y, [float]$w, [float]$h, [float]$r) {
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $r * 2
    $path.AddArc($x, $y, $d, $d, 180, 90)
    $path.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $path.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90)
    $path.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $path.CloseFigure()
    return $path
}

function New-MasterBitmap {
    $size = 256
    $bmp = New-Object System.Drawing.Bitmap $size, $size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.Clear([System.Drawing.Color]::Transparent)

    $bg = New-RoundedPath 8 8 240 240 52
    $bgBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 14, 23, 32))
    $g.FillPath($bgBrush, $bg)
    $border = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(90, 94, 234, 212)), 4
    $g.DrawPath($border, $bg)

    $cx = 128.0
    $cy = 124.0
    $spike = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(255, 232, 246, 248)), 14
    $spike.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $spike.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    foreach ($angle in @(0, 45, 90, 135)) {
        $rad = $angle * [Math]::PI / 180
        $dx = [Math]::Cos($rad) * 58
        $dy = [Math]::Sin($rad) * 58
        $g.DrawLine($spike, [float]($cx - $dx), [float]($cy - $dy), [float]($cx + $dx), [float]($cy + $dy))
    }

    $bodyBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 10, 16, 21))
    $g.FillEllipse($bodyBrush, [float]($cx - 38), [float]($cy - 38), 76, 76)
    $bodyPen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(255, 232, 246, 248)), 10
    $g.DrawEllipse($bodyPen, [float]($cx - 38), [float]($cy - 38), 76, 76)

    $dot = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 94, 234, 212))
    $g.FillEllipse($dot, [float]($cx - 11), [float]($cy - 11), 22, 22)

    $g.Dispose()
    return $bmp
}

$master = New-MasterBitmap
$sizes = @(16, 32, 48, 256)
$images = New-Object System.Collections.Generic.List[byte[]]

foreach ($size in $sizes) {
    $scaled = New-Object System.Drawing.Bitmap $size, $size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($scaled)
    $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
    $graphics.DrawImage($master, (New-Object System.Drawing.Rectangle 0, 0, $size, $size))
    $graphics.Dispose()

    $stream = New-Object System.IO.MemoryStream
    $scaled.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
    $images.Add($stream.ToArray())
    $stream.Dispose()
    $scaled.Dispose()
}

$master.Dispose()

$output = New-Object System.IO.MemoryStream
$writer = New-Object System.IO.BinaryWriter $output
$writer.Write([uint16]0)
$writer.Write([uint16]1)
$writer.Write([uint16]$sizes.Count)

$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $size = $sizes[$i]
    $dimension = if ($size -ge 256) { 0 } else { $size }
    $writer.Write([byte]$dimension)
    $writer.Write([byte]$dimension)
    $writer.Write([byte]0)
    $writer.Write([byte]0)
    $writer.Write([uint16]1)
    $writer.Write([uint16]32)
    $writer.Write([uint32]$images[$i].Length)
    $writer.Write([uint32]$offset)
    $offset += $images[$i].Length
}

foreach ($image in $images) {
    $writer.Write($image)
}

$writer.Flush()
$directory = Split-Path $OutFile -Parent
if (-not (Test-Path $directory)) {
    New-Item -ItemType Directory -Path $directory -Force | Out-Null
}
[System.IO.File]::WriteAllBytes($OutFile, $output.ToArray())
$writer.Dispose()
$output.Dispose()

Write-Host ("Wrote {0} ({1} bytes, {2} sizes)" -f $OutFile, (Get-Item $OutFile).Length, $sizes.Count)
```

- [ ] **Step 2: 生成图标**

Run: `.\scripts\make-icon.ps1`
Expected: `Wrote ...\assets\app.ico (... bytes, 4 sizes)`

- [ ] **Step 3: 引用图标**

在 csproj 的 `<PropertyGroup>` 中加入：

```xml
    <ApplicationIcon>..\..\assets\app.ico</ApplicationIcon>
```

- [ ] **Step 4: 构建并验证图标嵌入**

Run:

```powershell
.\build.ps1
Add-Type -AssemblyName System.Drawing
$exe = '.\src\DesktopMinesweeper\bin\Release\net8.0-windows\DesktopMinesweeper.exe'
$icon = [System.Drawing.Icon]::ExtractAssociatedIcon((Resolve-Path $exe))
$bitmap = $icon.ToBitmap()
$bitmap.Save((Join-Path $env:TEMP 'dms-icon-check.png'), [System.Drawing.Imaging.ImageFormat]::Png)
```

Expected: `dms-icon-check.png` 能正常打开并显示青色地雷图标。

- [ ] **Step 5: 提交**

```powershell
git add assets/app.ico scripts/make-icon.ps1 src/DesktopMinesweeper/DesktopMinesweeper.csproj
git commit -m "feat: add application icon for exe and installer"
```

---

### Task 2: Inno 脚本与构建脚本

**Files:**
- Create: `installer/desktop-minesweeper.iss`
- Create: `installer/build-installer.ps1`

- [ ] **Step 1: 写 Inno 脚本**

`installer/desktop-minesweeper.iss`：

```ini
#define AppName "Desktop Minesweeper"
#ifndef AppVersion
  #define AppVersion "1.2.0"
#endif
#define AppPublisher "leiou-xjq"
#define AppExe "DesktopMinesweeper.exe"
#define ProjectUrl "https://github.com/leiou-xjq/desktop-minesweeper"

[Setup]
AppId={{8E4C1B2A-7F3D-4A6B-9C5E-2D1F0A7B3C64}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#ProjectUrl}
AppSupportURL={#ProjectUrl}/issues
AppUpdatesURL={#ProjectUrl}/releases
DefaultDirName={localappdata}\Programs\DesktopMinesweeper
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
MinVersion=10.0.17763
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64
OutputDir=..\dist
OutputBaseFilename=DesktopMinesweeper-{#AppVersion}-setup
SetupIconFile=..\assets\app.ico
UninstallDisplayIcon={app}\{#AppExe}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
LicenseFile=..\LICENSE

[Languages]
Name: "chinesesimplified"; MessagesFile: "compiler:Languages\ChineseSimplified.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "..\dist\win-x64\{#AppExe}"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\LICENSE"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\README.md"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{group}\{cm:UninstallProgram,{#AppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent
```

- [ ] **Step 2: 写构建脚本**

`installer/build-installer.ps1`：

```powershell
#requires -Version 5.1
param(
    [string]$Version
)
$ErrorActionPreference = 'Stop'

$root = Split-Path $PSScriptRoot -Parent
$project = Join-Path $root 'src\DesktopMinesweeper\DesktopMinesweeper.csproj'
$sourceExe = Join-Path $root 'dist\win-x64\DesktopMinesweeper.exe'
$iss = Join-Path $PSScriptRoot 'desktop-minesweeper.iss'

if (-not $Version) {
    [xml]$projectXml = Get-Content $project
    $Version = [string](@($projectXml.Project.PropertyGroup.Version | Where-Object { $_ }) | Select-Object -First 1)
}
if (-not $Version) {
    throw 'Could not determine the version. Pass -Version explicitly.'
}
if (-not (Test-Path $sourceExe)) {
    throw "Missing $sourceExe. Run .\publish.ps1 first."
}
if (-not (Test-Path $iss)) {
    throw "Missing installer script: $iss"
}

$candidates = @(
    (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe'),
    (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'),
    (Join-Path $env:ProgramFiles 'Inno Setup 6\ISCC.exe')
)
$iscc = $candidates | Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1
if (-not $iscc) {
    $command = Get-Command iscc.exe -ErrorAction SilentlyContinue
    if ($command) { $iscc = $command.Source }
}
if (-not $iscc) {
    throw @'
Inno Setup 6 was not found.
Install it per-user without admin rights:
  https://jrsoftware.org/isdl.php  (choose "Install for me only")
or let CI build the installer.
'@
}

Write-Host ("Compiling installer with {0} ..." -f $iscc)
& $iscc "/DAppVersion=$Version" $iss | Out-Host
if ($LASTEXITCODE -ne 0) {
    throw ("ISCC failed with exit code {0}" -f $LASTEXITCODE)
}

$setup = Join-Path $root ("dist\DesktopMinesweeper-{0}-setup.exe" -f $Version)
if (-not (Test-Path $setup)) {
    throw ("Installer was not produced: {0}" -f $setup)
}

$hash = (Get-FileHash $setup -Algorithm SHA256).Hash.ToLowerInvariant()
$line = "{0}  {1}" -f $hash, (Split-Path $setup -Leaf)
Set-Content -Path "$setup.sha256" -Value $line -Encoding ASCII

Write-Host ''
Write-Host 'Installer ready' -ForegroundColor Green
Write-Host ("  file   : {0} ({1:N1} MB)" -f $setup, ((Get-Item $setup).Length / 1MB))
Write-Host ("  sha256 : {0}" -f $hash)
exit 0
```

- [ ] **Step 3: 语法检查（不依赖 Inno）**

Run: `powershell -NoProfile -Command "Get-Content .\installer\desktop-minesweeper.iss -TotalCount 5"`

Expected: 正常输出前 5 行，无编码乱码。

- [ ] **Step 4: 提交**

```powershell
git add installer/desktop-minesweeper.iss installer/build-installer.ps1
git commit -m "feat: add Inno Setup installer definition and build script"
```

---

### Task 3: 本地编译与安装/卸载验证

**Files:** 无代码变更

- [ ] **Step 1: 准备 Inno（当前用户）**

优先尝试：

```powershell
winget install --id JRSoftware.InnoSetup -e --scope user --silent --accept-package-agreements --accept-source-agreements
```

若失败，下载官方安装器并静默安装到当前用户：

```powershell
$installer = Join-Path $env:TEMP 'innosetup.exe'
Invoke-WebRequest -Uri 'https://jrsoftware.org/download.php/is.exe' -OutFile $installer -UseBasicParsing
Start-Process -FilePath $installer -ArgumentList @('/VERYSILENT','/CURRENTUSER','/SUPPRESSMSGBOXES','/NORESTART','/NOICONS',"/DIR=$env:LOCALAPPDATA\Programs\Inno Setup 6") -Wait
```

- [ ] **Step 2: 生成发布 exe 与安装包**

```powershell
.\publish.ps1
.\installer\build-installer.ps1
```

Expected：`dist\DesktopMinesweeper-1.2.0-setup.exe` 与 `.sha256` 生成。

- [ ] **Step 3: 记录桌面基线**

```powershell
$env:DOTNET_ROOT_X64 = 'D:\DesktopMinesweeper\.dotnet'
$baseline = Join-Path $env:TEMP 'dms-installer-baseline.txt'
Start-Process -FilePath '.\dist\win-x64\DesktopMinesweeper.exe' -ArgumentList @('--dump', $baseline) -Wait
```

- [ ] **Step 4: 静默安装（不建桌面快捷方式）**

```powershell
$setup = (Resolve-Path '.\dist\DesktopMinesweeper-1.2.0-setup.exe').Path
Start-Process -FilePath $setup -ArgumentList @('/VERYSILENT','/CURRENTUSER','/SUPPRESSMSGBOXES','/NORESTART','/TASKS=""') -Wait
$app = Join-Path $env:LOCALAPPDATA 'Programs\DesktopMinesweeper\DesktopMinesweeper.exe'
"exists=$(Test-Path $app)"
```

Expected：`exists=True`，且未弹 UAC。

- [ ] **Step 5: 校验安装结果与运行**

```powershell
$startMenu = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs\Desktop Minesweeper'
Get-ChildItem $startMenu | Select-Object Name
$uninstallKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\{8E4C1B2A-7F3D-4A6B-9C5E-2D1F0A7B3C64}_is1'
"uninstallEntry=$(Test-Path $uninstallKey)"
$report = Join-Path $env:TEMP 'dms-installed-selftest.txt'
$process = Start-Process -FilePath $app -ArgumentList @('--selftest', $report) -Wait -PassThru
"selftestExit=$($process.ExitCode)"
Get-Content $report -Encoding UTF8 | Select-Object -Last 2
```

Expected：开始菜单有程序与卸载项；`uninstallEntry=True`；`RESULT PASS`。

- [ ] **Step 6: 校验卸载**

```powershell
$uninstaller = Join-Path $env:LOCALAPPDATA 'Programs\DesktopMinesweeper\unins000.exe'
Start-Process -FilePath $uninstaller -ArgumentList @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART') -Wait
Start-Sleep -Seconds 2
"appDirExists=$(Test-Path (Join-Path $env:LOCALAPPDATA 'Programs\DesktopMinesweeper'))"
"startMenuExists=$(Test-Path (Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs\Desktop Minesweeper'))"
"uninstallEntry=$(Test-Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\{8E4C1B2A-7F3D-4A6B-9C5E-2D1F0A7B3C64}_is1')"
```

Expected：全部 `False`。

- [ ] **Step 7: 桌面图标一致**

```powershell
$after = Join-Path $env:TEMP 'dms-installer-after.txt'
Start-Process -FilePath '.\dist\win-x64\DesktopMinesweeper.exe' -ArgumentList @('--dump', $after) -Wait
$a = (Get-Content $baseline -Encoding UTF8 | Where-Object { $_ -match '^icon\[' }) -join "`n"
$b = (Get-Content $after -Encoding UTF8 | Where-Object { $_ -match '^icon\[' }) -join "`n"
"identical=$($a -eq $b)"
```

Expected：`identical=True`。

- [ ] **Step 8: 桌面快捷方式任务验证**

```powershell
Start-Process -FilePath $setup -ArgumentList @('/VERYSILENT','/CURRENTUSER','/SUPPRESSMSGBOXES','/NORESTART','/TASKS=desktopicon') -Wait
$shortcut = Join-Path ([Environment]::GetFolderPath('Desktop')) 'Desktop Minesweeper.lnk'
"desktopShortcut=$(Test-Path $shortcut)"
```

Expected：`desktopShortcut=True`。随后再次静默卸载，并确认快捷方式消失、47 个图标恢复一致。

- [ ] **Step 9: 提交（如有脚本修正）**

```powershell
git add -A
git commit -m "fix: installer adjustments from local install test"
```

---

### Task 4: CI 集成

**Files:**
- Modify: `.github/workflows/build.yml`

- [ ] **Step 1: 在 Publish 之后加入 Inno 步骤**

```yaml
      - name: Install Inno Setup
        run: choco install innosetup --no-progress -y

      - name: Build installer
        shell: pwsh
        run: .\installer\build-installer.ps1 -Version '${{ steps.version.outputs.value }}'

      - name: Verify installer exists
        shell: pwsh
        run: |
          $setup = Get-ChildItem dist -Filter '*-setup.exe'
          if (-not $setup) { throw 'Installer was not produced.' }
          Get-ChildItem dist -Filter '*-setup.exe*' | Select-Object Name, Length
```

放在 `Verify published exe` 之后、`Upload artifacts` 之前。

- [ ] **Step 2: 本地检查 YAML 结构**

Run: `Get-Content .github/workflows/build.yml`

Expected：缩进正确、步骤顺序合理、`dist/*.exe` 与 `dist/*.sha256` 已覆盖 setup 资产。

- [ ] **Step 3: 提交**

```powershell
git add .github/workflows/build.yml
git commit -m "ci: build and publish the setup.exe installer"
```

---

### Task 5: 版本与文档

**Files:**
- Modify: `src/DesktopMinesweeper/DesktopMinesweeper.csproj`（1.2.0）
- Modify: `README.md`
- Modify: `CHANGELOG.md`
- Modify: `docs/safety.md`
- Modify: `docs/releasing.md`

- [ ] **Step 1: 升版本**

`<Version>1.2.0</Version>`。

- [ ] **Step 2: README**

- 状态改为 1.2.0，加入安装包说明
- “下载使用”改为：
  1. 推荐下载 `DesktopMinesweeper-<版本>-setup.exe`，双击安装
  2. 或下载便携 zip / 单文件 exe
- 安装说明：仅当前用户、默认路径、桌面快捷方式可选、完成页可启动
- 卸载说明：设置 → 应用，或开始菜单卸载项
- 保留 SmartScreen 与 SHA-256 说明

- [ ] **Step 3: CHANGELOG**

新增 `[1.2.0]`：

- Inno Setup 安装包（仅当前用户、无需管理员）
- 开始菜单快捷方式、可选桌面快捷方式、安装后启动
- 标准卸载入口
- 应用图标（exe + 快捷方式 + 安装器）
- CI 构建并发布 setup.exe 与校验和

- [ ] **Step 4: 安全文档**

新增“安装器与程序”小节：

- 安装器会写入开始菜单/桌面快捷方式与 HKCU 卸载项，这是标准安装行为
- 程序运行时仍不写注册表、不改桌面图标、不注册开机启动
- 卸载删除安装目录与快捷方式

- [ ] **Step 5: 发布文档**

`docs/releasing.md` 增加安装包构建步骤与产物说明，示例版本改为 1.2.0。

- [ ] **Step 6: 提交**

```powershell
git add -A
git commit -m "release: 1.2.0 installer"
```

---

### Task 6: 发布验证

**Files:** 无

- [ ] **Step 1: 全量自检**

```powershell
.\publish.ps1
.\installer\build-installer.ps1
.\run.ps1 -SelfTest
.\scripts\verify-exit-safety.ps1 -ExePath .\dist\DesktopMinesweeper-1.2.0-win-x64.exe
```

Expected：自检 PASS，退出安全 5/5 PASS。

- [ ] **Step 2: 校验安装包哈希**

```powershell
$setup = '.\dist\DesktopMinesweeper-1.2.0-setup.exe'
(Get-FileHash $setup -Algorithm SHA256).Hash.ToLower() -eq ((Get-Content "$setup.sha256" -Raw).Trim().Split(' ')[0])
```

Expected：`True`。

- [ ] **Step 3: 推送并发布**

```powershell
git push origin main
git tag -a v1.2.0 -m "Desktop Minesweeper 1.2.0"
git push origin v1.2.0
```

- [ ] **Step 4: 等待 CI 并校验 Release**

- 工作流 success
- Release 包含 setup.exe、便携 zip、单文件 exe、fx exe 与全部 `.sha256`
- 下载 CI 产出的 setup.exe，重复 Task 3 的静默安装/卸载与图标一致性检查

---

## 验收清单

- [ ] setup.exe 无需管理员即可安装
- [ ] 开始菜单程序项与卸载项存在
- [ ] 桌面快捷方式仅在勾选时创建
- [ ] 完成页可立即启动
- [ ] 卸载清理文件、快捷方式、HKCU 卸载项
- [ ] 安装/卸载前后 47 个桌面图标一致
- [ ] 安装后的 exe 通过 39 项自检
- [ ] 退出安全 5/5 通过
- [ ] Release 资产齐全且 SHA-256 匹配
