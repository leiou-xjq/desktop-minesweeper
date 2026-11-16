# 玻璃拟态界面设计（v1.1.0）

日期：2026-11-16
状态：已确认，待实现

## 背景

当前覆盖层偏“工程化”：未翻开格是深色半透明方块，翻开格是高对比浅色块，状态栏是平直深色条。功能完整，但视觉粗糙，长时间使用时显得沉重。

目标是在**不改变任何安全边界、读取逻辑和玩法**的前提下，把界面升级为轻量的玻璃拟态风格。

## 目标

- 未翻开格呈现毛玻璃质感，图标轮廓隐约透出
- 已翻开格清晰通透，数字可读性优先
- 状态栏轻盈、有层次，按钮与提示风格统一
- 动效克制，不干扰操作
- 性能稳定：不引入实时模糊特效

## 非目标

- 不改变图标读取、DPI 映射、安全策略、退出逻辑
- 不引入 MVVM 框架、主题系统、配置文件
- 不为高对比度模式做专门适配（保持现状并记录限制）
- 不重做游戏规则或棋盘生成逻辑

## 视觉规格

### 材质

| 元素 | 规格 |
|---|---|
| 玻璃底色 | 垂直渐变 `#B30E1720 → #CC0B1116` |
| 外部描边 | 1px，悬停前 `#33FFFFFF`，悬停时 `#66E5FFF7` |
| 顶部内高光 | 1px，`#4DFFFFFF` |
| 底部内阴影 | 1px，`#40000000` |
| 圆角 | 10px |
| 强调色 | 青绿 `#5EEAD4`、琥珀 `#FBBF24`、柔红 `#F87171` |

### 未翻开格

- 底图：`GlassThumbnail`（模糊半径约 8px，压暗 45%）
- 玻璃渐变覆盖 + 顶部高光 + 底部内阴影
- 悬停：高光层 0 → 1（150ms），描边提亮

### 已翻开格

- 底图：清晰缩略图
- 覆盖：冷白玻璃约 12%，青色 30% 描边
- 数字：24px 磨砂圆徽标，彩色数字；0 雷格只保留清晰图标与淡绿边
- 不加外发光

### 旗子与地雷

- 旗子：琥珀矢量旗 + 磨砂圆底，与数字徽标同尺寸
- 地雷：红色玻璃底 + 白色矢量雷
- 爆炸格：更强红色，保留抖动

### 状态栏

- 底部居中玻璃胶囊，单层柔和阴影（整个状态栏一个 `DropShadowEffect`）
- 左侧青绿圆点 + “桌面扫雷”
- 分组：剩余雷数、格子数、时间（等宽数字），细竖线分隔
- 状态文案颜色：进行中灰白、胜利青绿、失败红色
- 按钮：ghost 风格，圆角 8，悬停淡青底，按下内凹

### 顶部提示

- 玻璃胶囊 + 琥珀左边条

### 字体

- UI：`Segoe UI Variable Text, Segoe UI, Microsoft YaHei UI`
- 数字：`Cascadia Mono, Consolas`

## 实现方案

### 图像处理

在 `DesktopBitmapCapture` 中新增：

```csharp
public static BitmapSource? BlurAndDarken(BitmapSource source, int radius, double dim);
```

- 提取 BGRA 像素
- 水平、垂直各两遍盒式模糊（近似高斯）
- 压暗：`channel * dim`
- alpha 固定为 255，避免透明边缘
- 返回冻结的 `BitmapSource`

### 数据模型

- `BoardCell` 新增 `GlassThumbnail`
- `BoardGridBuilder` 为每个桌面图标格生成玻璃缩略图
- 固定网格模式保持 `null`，渲染层回退到纯渐变玻璃

### 游戏模型

- `MinesweeperGame` 新增 `LastRevealed`（本次操作翻开的格子集合）
- 用于只给新翻开的格子播放动画

### 渲染

`BoardRenderer` 分层：

1. 底图（`Image`，`Stretch.Fill`）
2. 玻璃渐变（`Rectangle` + `LinearGradientBrush`）
3. 顶部高光 / 底部内阴影（1px 线条）
4. 内容层：数字徽标 / 旗子 / 地雷 / 悬停高光

交互：

- 悬停：高光层透明度动画，离开反向
- 翻开：`LastRevealed` 中的格子做 0.94 → 1 缩放 + 0 → 1 淡入（180ms，`CubicEase`）
- 踩雷：保留抖动与脉冲，颜色适配新主题
- 已结束的棋局仍不可交互

### 状态栏

重写 `OverlayWindow.xaml`：

- 玻璃胶囊与品牌点
- 分组数据与竖线
- ghost 按钮 `ControlTemplate`（悬停 / 按下）
- 顶部提示徽标同步改版

### 性能预算

- 模糊只在构建棋盘时执行一次（约几十毫秒）
- 运行时无 `BlurEffect`
- 动画仅悬停与单次翻开
- 视觉树规模与当前相近（每格约 4–5 个元素）

## 降级与容错

| 情况 | 行为 |
|---|---|
| 模糊处理异常 | 回退到现有压暗缩略图 |
| 缩略图为空 | 纯渐变玻璃 + 极淡斜向高光 |
| 图标读取失败 | 现有固定网格降级不变 |
| DPI 非 100% | 沿用现有 Per-Monitor V2 映射 |

## 测试

- `run.ps1 -SelfTest`：30 项规则回归必须全部通过
- `scripts/verify-exit-safety.ps1`：5 条退出路径 + 47 个图标一致
- `--capture --demo` / `--demo-lose`：检查毛玻璃、翻开格、数字、旗子、状态栏、踩雷
- 120 DPI 下目测：无文字模糊、无重叠、无裁切
- 手动悬停与点击验证过渡效果

## 改动文件

- `src/DesktopMinesweeper/Desktop/DesktopBitmapCapture.cs`
- `src/DesktopMinesweeper/Game/BoardGridBuilder.cs`
- `src/DesktopMinesweeper/Game/MinesweeperGame.cs`
- `src/DesktopMinesweeper/Ui/BoardRenderer.cs`
- `src/DesktopMinesweeper/OverlayWindow.xaml`
- `src/DesktopMinesweeper/OverlayWindow.xaml.cs`
- `README.md`、`CHANGELOG.md`、版本号（1.1.0）

## 验收标准

1. 未翻开格能看到被模糊的图标轮廓，且整体呈玻璃质感
2. 已翻开格图标清晰，数字在浅色与深色壁纸上都可读
3. 悬停与翻开过渡平滑，无闪烁、无整盘重绘感
4. 状态栏与按钮风格统一，玻璃层次清晰
5. 固定网格模式外观协调
6. 规则自检与退出安全验收全部通过
7. 退出后桌面图标坐标与文本与启动前完全一致
