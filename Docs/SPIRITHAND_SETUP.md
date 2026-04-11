# SpiritHand 配置指南

本文档用于指导团队成员在 Unity 中手动配置 `SpiritHand`（精灵手提示）。

## 1) 作用与现状

- `SpiritHand` 用于在玩家头顶显示手势提示图。
- 当前仅显示三种手势：`Push`、`Fist`、`Shoot`。
- 脚本文件：`Assets/Scripts/Player/SpiritHandDisplay.cs`

> 说明：如果场景里没有 `SpiritHand`，`GameManager` 会在运行时自动创建一个临时对象。
> 但临时对象在退出 Play 后不会保留。要长期可调参数，建议手动放到 Hierarchy。

## 2) 手动拖到 Hierarchy（推荐做法）

1. 打开目标场景（例如 `Tutorial`、`Level2`）。
2. 在 Hierarchy 右键 `Create Empty`，命名为 `SpiritHand`。
3. 给 `SpiritHand` 添加组件：
   - `SpriteRenderer`
   - `SpiritHandDisplay`
4. 在 `SpiritHandDisplay` 里绑定图片：
   - `pushSprite` -> `Assets/Textures/SpiritHands/push.png`
   - `fistSprite` -> `Assets/Textures/SpiritHands/pull.png`
   - `shootSprite` -> `Assets/Textures/SpiritHands/shoot.png`
5. 把场景中的 `Player` 拖到 `SpiritHandDisplay.player` 字段。
6. 在 `SpriteRenderer` 中设置：
   - `Sorting Order = 10`（建议值，保证显示在玩家前面）
7. 保存场景。

## 3) 尺寸与位置怎么调

- 全局大小：`SpiritHandDisplay.targetWorldHeight`
- 每个动作单独倍率：`gestureSizeEntries`
  - `Push` / `Fist` / `Shoot`
  - 默认都为 `1.0`
  - `< 1.0` 变小，`> 1.0` 变大
- 位置偏移：`offset`（通常调 `y`）

## 4) 验证步骤

1. 进入 Play 模式。
2. 做 `Push` / `Fist` / `Shoot` 手势。
3. 观察玩家头顶是否显示对应图标，大小与位置是否符合预期。

## 5) 常见问题

- 看不到 `SpiritHand`：
  - 你可能只在运行时自动创建模式下，编辑模式 Hierarchy 不会常驻显示。
  - 按上面的手动创建流程放入场景即可。
- 图标不显示：
  - 检查 `push/fist/shoot` 三张图是否已绑定。
  - 检查 `GestureService` 是否正常运行（MediaPipe 依赖正确安装）。
- 调了参数但下次丢失：
  - 你改的是运行时自动生成对象。请改手动放进场景的 `SpiritHand` 对象并保存场景。
