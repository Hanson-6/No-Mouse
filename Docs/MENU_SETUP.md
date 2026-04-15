# Menu 配置指南（MainMenu + PauseMenu）

本文档用于指导团队成员统一配置主菜单与暂停菜单。

## 1) 相关脚本与入口

- 主菜单构建脚本：`Assets/Editor/MainMenuSetup.cs`
- 暂停菜单构建脚本：`Assets/Editor/PauseMenuSetup.cs`
- 主菜单运行脚本：`Assets/Scripts/UI/MainMenuController.cs`
- 暂停菜单运行脚本：`Assets/Scripts/UI/PauseMenu.cs`

按钮素材目录（已统一）：

- `Assets/Textures/Buttons/`

## 2) MainMenu 如何设置

目标场景：`Assets/Scenes/MainMenu.unity`

步骤：

1. 打开 `MainMenu.unity`。
2. 在 Unity 菜单执行：`Tools/Setup Main Menu Scene`。
3. 脚本会自动：
   - 使用 `Assets/Textures/Buttons/NewGameButton.png`
   - 使用 `Assets/Textures/Buttons/ContinueButton.png`
   - 使用 `Assets/Textures/Buttons/QuitButton.png`
   - 挂载/重连 `MainMenuController`
   - 按图像比例自动设置按钮尺寸（避免拉伸）
4. 保存场景。

样式对齐说明：

- 主菜单按钮与暂停菜单统一视觉规格：
  - 目标高度 `100`
  - 宽度上限 `620`（按按钮图宽高比自动计算）
  - 非选中视觉更暗（约 `0.84`），选中为纯白
  - 键盘选中项会轻微放大（约 `1.04x`）

验证：

- `NewGame` 可进入 `Tutoring`。
- 有存档时显示 `Continue`，无存档时隐藏。
- `Quit` 在 Editor 中停止 Play，在 Build 中退出程序。

## 3) PauseMenu 如何设置

通常用于关卡场景（例如 `Tutorial`，或你希望支持暂停的关卡）。

重要规则（团队约定）：

- **每次修改 Pause Menu 相关代码或按钮素材后，都必须重新执行一次 `Tools\\Setup Pause Menu`（Unity 菜单显示为 `Tools/Setup Pause Menu`）。**
- 原因：`PauseCanvas`、按钮层级、坐标、引用绑定由构建脚本统一生成；手动改场景容易产生漏改或引用错位。

步骤：

1. 打开目标关卡场景。
2. 在 Unity 菜单执行：`Tools/Setup Pause Menu`。
3. 脚本会自动：
    - 创建 `PauseCanvas`（若已存在会重建）
    - 创建 `PauseButton`、`PausePanel`、`MenuButton`、`ResumeButton`、`RestartLevelButton`、`BackToCheckpointButton`
    - 绑定 `PauseMenu.cs`
    - 使用 `Assets/Textures/Buttons/` 下对应按钮图
    - 按图像比例自动设置按钮尺寸（避免拉伸）
4. 保存场景。

验证：

- 按 `Esc` 可暂停/恢复。
- 点击 `PauseButton` 可打开暂停面板。
- `Menu` 位于最上方（顶部）按钮。
- `Resume` 恢复游戏。
- `Restart Level` 重新开始当前关卡。
- `Back To Checkpoint` 回到最近 checkpoint。

## 4) Level2 说明

- 当前 `Level2` 默认可能没有 `PauseCanvas`。
- 若希望 `Level2` 有暂停功能，请在 `Level2` 场景执行一次 `Tools/Setup Pause Menu`。

## 5) 常见问题

- 按钮图改了但场景没变：
  - 重新执行对应 Setup 工具（MainMenu 或 PauseMenu），再保存场景。
- 改了 PauseMenu 脚本但某关按钮位置/绑定不对：
  - 重新打开该关，执行 `Tools/Setup Pause Menu`，并保存场景。
- 按钮显示糊：
  - Setup 工具会自动修正导入设置（Sprite、Point、无 Mipmap、Uncompressed）。
  - 若仍异常，手动 Reimport 对应图片。
- Continue 按钮不显示：
  - 这是正常逻辑（无存档时隐藏），由 `SaveManager.HasSave()` 控制。
