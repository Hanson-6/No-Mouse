# GestureRecognition 服务 SOP（新场景通用）

本文档用于团队在新增 Scene 时，快速恢复并验证手势识别系统。
按本文执行，可避免出现“摄像头不显示 / 手势全失效 / 精灵手不显示”等问题。

## 1) 适用范围

- 适用于所有需要手势功能的场景（Tutorial、Level1、Level2、后续新关卡）。
- 当前项目支持的核心手势玩法：
  - `Push`（推箱）
  - `Fist`（Pull 逻辑）
  - `Shoot`（发射子弹）

## 2) 一次性环境准备（每台机器）

1. 在项目根目录运行：

```powershell
./setup-mediapipe.ps1
```

2. 确认文件存在：
   - `Packages/com.github.homuler.mediapipe-0.16.3.tgz`
3. 打开 Unity，等待 Package Manager 解析完成。

说明：

- `Packages/manifest.json` 已使用相对路径依赖：
  - `"com.github.homuler.mediapipe": "file:com.github.homuler.mediapipe-0.16.3.tgz"`
- 这保证了团队 `git pull` 后不需要每个人改绝对路径。

## 3) 新场景最低配置（必做）

### 3.1 必须有 Player

- 场景中必须有带 `PlayerController` 的 `Player`。
- 如果需要 Shoot 功能，`Player` 还需要 `ShootingController`。

### 3.2 推荐有 GameManager

- 场景中放置 `GameManager`（`Assets/Scripts/Core/GameManager.cs`）。
- 该脚本会自动补齐手势玩法依赖：
  - `GestureInputBridge`
  - `SpiritHandDisplay`
  - 并把它们绑定到当前 `Player`

### 3.3 GestureService 自动创建机制

- `GestureServiceBootstrap` 会在场景加载前确保存在 `GestureService`。
- `GestureService` 默认会跨场景保留（`DontDestroyOnLoad`）。

结论：

- 标准做法是：**新场景至少有 Player + GameManager**，其他由系统自动补齐。

## 4) 可选 UI（摄像头面板）

- `GestureService` 开启 `Auto Show Panel` 时，会自动创建 `GestureCanvas + GesturePanel`。
- 若你不希望显示调试面板，可在 `GestureService` Inspector 里关闭 `Auto Show Panel`。
- 关闭面板不影响识别本体。

## 5) Scene 验收清单（每次换场景必测）

进入 Play 后按顺序检查：

1. Console 不应出现以下警告：
   - `Running in stub mode`
2. 摄像头画面正常（若启用面板）
3. 做 `Push` / `Fist` / `Shoot` 时，识别有响应
4. `SpiritHand` 能显示并跟随玩家头顶
5. `Shoot` 手势能开火
6. `Push/Fist` 与箱子交互正常（若场景有箱子）

## 6) 常见故障与处理

### 故障 A：摄像头无画面，手势全失效

优先检查：

1. `Packages/com.github.homuler.mediapipe-0.16.3.tgz` 是否存在
2. Console 是否有 `stub mode` 警告
3. 是否存在 `GestureService`（自动或手动）

处理：

- 重新运行 `./setup-mediapipe.ps1`
- 重启 Unity，让 Package 重新解析

### 故障 B：手势有识别，但没有精灵手

检查：

1. 场景中是否有 `Player`
2. 场景中是否存在 `SpiritHand`（或由 `GameManager` 自动创建）
3. `SpiritHandDisplay` 的三张图是否已绑定：
   - `Assets/Textures/SpiritHands/push.png`
   - `Assets/Textures/SpiritHands/pull.png`
   - `Assets/Textures/SpiritHands/shoot.png`

### 故障 C：Push/Fist/Shoot 在新场景失效

检查：

1. 新场景是否放了 `GameManager`
2. `Player` 上是否有 `PlayerController`
3. 需要射击时是否有 `ShootingController`

## 7) 团队协作约定（必须遵守）

- 不要把 `manifest` 改成本机绝对路径。
- 不要把 MediaPipe 改成 Git 源 URL 依赖（容易缺 native runtime）。
- 新增场景时，提 PR 前必须跑完本 SOP 的“Scene 验收清单”。

## 8) 相关文档

- MediaPipe 安装：`Docs/MEDIAPIPE_TEAM_SETUP.md`
- SpiritHand 手动配置：`Docs/SPIRITHAND_SETUP.md`
- Menu 配置：`Docs/MENU_SETUP.md`
