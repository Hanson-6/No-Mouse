# 后端 & 测试人员操作指南 (Backend & Testing Guide)

> 本文档面向**后端（算法）工程师和测试人员**，说明如何启动项目、运行测试、添加新手势、以及日常维护操作。

---

## 0. 环境准备：安装 MediaPipe Unity Plugin

**在使用手势识别模块之前，必须安装 MediaPipe 插件。**

### 第一步：下载插件

1. 打开浏览器，访问：https://github.com/homuler/MediaPipeUnityPlugin/releases/tag/v0.16.3
2. 在页面底部的 **Assets** 区域，找到并下载 `.tgz` 文件（文件名类似 `com.github.homuler.mediapipe-0.16.3.tgz`）
3. 将下载的 `.tgz` 文件保存到一个你记得住的位置（例如桌面），**不要解压**

### 第二步：在 Unity 中导入

1. 打开 Unity Editor，确保已加载本项目（COMP3329_TEST）
2. 菜单栏点击 **Window → Package Manager**（包管理器）
3. 在 Package Manager 窗口左上角，点击 **+** 按钮（加号）
4. 选择 **Add package from tarball...**（从 tarball 添加包）
5. 在文件选择对话框中，找到你刚下载的 `.tgz` 文件，选中，点击 **Open**（打开）
6. 等待 Unity 导入完成（左下角进度条跑完，Console 无报错）

### 第三步：验证安装

1. 在 Package Manager 中，切换到 **In Project** 标签页
2. 应该能看到 **MediaPipe Unity Plugin** (v0.16.3) 已列出
3. 打开 Console 窗口（**Window → General → Console**），确认没有红色报错
4. 确认代码可以编译：如果 Console 没有报错，说明 `MEDIAPIPE_INSTALLED` 宏已自动生效

> **注意**：`.tgz` 文件不需要放在项目文件夹内。Unity 会将其内容缓存到 `Library/PackageCache/` 中。团队每个人都需要各自安装。

---

## 1. 文件归属说明

手势识别模块的文件分布在以下位置：

```
Assets/
├── Scripts/
│   └── GestureRecognition/    ← 全部代码（后端维护）
│       ├── Core/              ← 数据类型、事件、配置
│       ├── Detection/         ← 摄像头、MediaPipe、分类器、追踪器
│       ├── Service/           ← 对外 API 单例
│       ├── UI/                ← 显示面板（通常不需要修改）
│       └── Editor/            ← 编辑器工具（精灵图生成等）
│
├── Tests/                     ← 自动化测试
│   ├── EditMode/              ← 编辑模式测试（纯逻辑测试）
│   └── PlayMode/              ← 运行模式测试（集成测试）
│
├── Scenes/
│   ├── GestureTestScene.unity ← 手动测试场景
│   └── SampleScene.unity      ← 默认 / 游戏场景
│
└── Resources/
    ├── GestureConfig.asset    ← 手势配置资产
    └── GestureSprites/        ← 手势精灵图文件
```

**后端人员主要需要修改的文件**：
- `Core/GestureType.cs` — 添加新手势枚举
- `Detection/GestureClassifier.cs` — 编写/调优分类逻辑
- `Resources/GestureSprites/` — 添加新精灵图
- `Resources/GestureConfig.asset` — 在 Inspector 中配置新手势条目

**通常不需要修改的文件**：Service 层、UI 层、事件系统、MediaPipeBridge——这些是通用框架。

---

## 2. 启动测试场景（手动测试）

### 第一步：打开测试场景

1. 在 Unity Editor 的 **Project** 窗口中，导航到 `Assets/Scenes/`
2. 双击 **GestureTestScene.unity**（这会切换当前场景）

### 第二步：确认场景配置

1. 在 **Hierarchy** 面板中，应该看到一个名为 `GestureManager` 的 GameObject
2. 点击选中它，在 **Inspector** 面板中确认它有 **GestureService** 组件
3. 确认 **GestureService** 组件中 `_autoStart` 字段已勾选（值为 true）

如果 `GestureManager` 不存在或缺少组件：
1. Hierarchy 中右键 → **Create Empty** → 改名为 `GestureManager`
2. 选中它 → Inspector 底部 **Add Component** → 搜索 `GestureService` → 添加
3. 在 Inspector 中勾选 `_autoStart`
4. `Ctrl+S` 保存场景

### 第三步：运行

1. 点击顶部工具栏的 **Play** 按钮（▶），或按 `Ctrl+P`
2. 等待 Console 中出现初始化信息（如 `[GestureService] Initialized`）
3. 系统会自动启动摄像头和手势识别
4. 将手放到摄像头前方，面板会实时显示识别到的手势

### 第四步：停止

点击 **Play** 按钮再次关闭（或按 `Ctrl+P`）。

### 无摄像头测试

如果没有可用摄像头，可以在代码中使用 `GestureService.Instance.Bridge.InjectMockData()` 注入模拟手势数据进行测试。

---

## 3. 运行自动化测试

### 在 Unity Editor 中运行

1. 菜单栏 → **Window → General → Test Runner**
2. Test Runner 窗口会显示所有测试

**EditMode 测试**（24 个）：
1. 点击 **EditMode** 标签页
2. 点击 **Run All** 运行所有 24 个测试
3. 绿色 ✓ = 通过，红色 ✗ = 失败

**PlayMode 测试**（15 个）：
1. 点击 **PlayMode** 标签页
2. 点击 **Run All**（Unity 会自动进入 Play 模式运行测试，完成后自动退出）

**运行单个测试**：展开测试树，右键某个测试 → **Run**

### 命令行运行（需先关闭 Editor）

```bash
# EditMode 测试
"E:/Application/Develop/Software/Tuanjie_Hub/Hub/Editor/2022.3.62f3c1/Editor/Unity.exe" ^
  -runTests -batchmode -nographics ^
  -projectPath "E:/Application/Develop/Software/Tuanjie_Hub/Project/COMP3329_TEST" ^
  -testResults TestResults-EditMode.xml ^
  -testPlatform EditMode

# PlayMode 测试
"E:/Application/Develop/Software/Tuanjie_Hub/Hub/Editor/2022.3.62f3c1/Editor/Unity.exe" ^
  -runTests -batchmode -nographics ^
  -projectPath "E:/Application/Develop/Software/Tuanjie_Hub/Project/COMP3329_TEST" ^
  -testResults TestResults-PlayMode.xml ^
  -testPlatform PlayMode
```

> **重要**：命令行测试要求 Unity Editor **完全关闭**。否则会报错。

---

## 4. 添加新手势（完整操作流程）

**示例**：添加一个"竖大拇指 (ThumbsUp)"手势。

### 步骤 1：添加枚举值

**文件**：`Assets/Scripts/GestureRecognition/Core/GestureType.cs`

找到枚举定义，在 `Count` 之前插入新值：

```csharp
// 修改前
OpenPalm = 5,
Count

// 修改后
OpenPalm = 5,
ThumbsUp = 6,    // ← 新增：竖大拇指
Count
```

> `Count` 必须保持在最后，它的数值会自动更新。

### 步骤 2：编写分类逻辑

**文件**：`Assets/Scripts/GestureRecognition/Detection/GestureClassifier.cs`

**2a. 添加静态分类方法**（在文件末尾、类的右花括号之前）：

```csharp
/// <summary>
/// Classifies thumbs-up gesture: thumb extended upward, all other fingers curled.
/// </summary>
public static float ClassifyThumbsUp(Vector3[] lm)
{
    if (lm == null || lm.Length < MediaPipeBridge.LandmarkCount)
        return 0f;

    // 拇指伸展
    bool thumbExtended = IsFingerExtended(lm,
        MediaPipeBridge.ThumbTip, MediaPipeBridge.ThumbMcp, MediaPipeBridge.Wrist);

    // 其余四指弯曲
    bool indexCurled = IsFingerCurled(lm,
        MediaPipeBridge.IndexTip, MediaPipeBridge.IndexMcp, MediaPipeBridge.Wrist);
    bool middleCurled = IsFingerCurled(lm,
        MediaPipeBridge.MiddleTip, MediaPipeBridge.MiddleMcp, MediaPipeBridge.Wrist);
    bool ringCurled = IsFingerCurled(lm,
        MediaPipeBridge.RingTip, MediaPipeBridge.RingMcp, MediaPipeBridge.Wrist);
    bool pinkyCurled = IsFingerCurled(lm,
        MediaPipeBridge.PinkyTip, MediaPipeBridge.PinkyMcp, MediaPipeBridge.Wrist);

    bool othersCurled = indexCurled && middleCurled && ringCurled && pinkyCurled;

    if (thumbExtended && othersCurled)
    {
        // 额外检查：拇指尖在手腕上方（y 更大 = 更高）
        float thumbUpScore = lm[MediaPipeBridge.ThumbTip].y > lm[MediaPipeBridge.Wrist].y
            ? 0.95f : 0.7f;
        return thumbUpScore;
    }

    return 0f;
}
```

**2b. 在构造函数中注册**：

找到构造函数 `public GestureClassifier(float confidenceThreshold = 0.6f)`，在最后一个 `RegisterClassifier` 调用之后添加：

```csharp
RegisterClassifier(GestureType.ThumbsUp, ClassifyThumbsUp);
```

### 步骤 3：准备精灵图

1. 准备一张 PNG 图片（建议 64×64 或 128×128 像素），命名为 `thumbsup.png`
2. 将其放入 `Assets/Resources/GestureSprites/` 文件夹（直接复制到该目录，或从 Windows 资源管理器拖入 Unity Project 窗口）
3. 在 Unity Editor 中，选中 `thumbsup.png`
4. 在 Inspector 中确认：
   - **Texture Type** = `Sprite (2D and UI)`（如果不是，点击下拉框选择）
   - 点击右下角 **Apply** 保存设置

### 步骤 4：配置 GestureConfig

1. 在 Project 窗口导航到 `Assets/Resources/`
2. 单击选中 `GestureConfig.asset`
3. 在 Inspector 中找到 **Gesture Entries** 列表
4. 点击列表底部的 **+** 按钮
5. 在新增的条目中：
   - **Type** → 点击下拉框 → 选择 `ThumbsUp`
   - **Sprite** → 从 Project 窗口将 `thumbsup.png` 拖拽到此字段（或点击右侧小圆圈 ⊙ 搜索选择）
   - **Display Name** → 输入 `"Thumbs Up"`
6. `Ctrl+S` 保存

### 步骤 5：编写单元测试（可选但推荐）

**文件**：`Assets/Tests/EditMode/GestureClassifierTests.cs`

在 `GestureClassifierTests` 类中添加：

```csharp
[Test]
public void ClassifyThumbsUp_WithThumbsUpPose_ReturnsHighConfidence()
{
    Vector3[] lm = new Vector3[MediaPipeBridge.LandmarkCount];
    // 设置拇指伸展向上
    lm[MediaPipeBridge.Wrist] = new Vector3(0.5f, 0.4f, 0);
    lm[MediaPipeBridge.ThumbMcp] = new Vector3(0.55f, 0.5f, 0);
    lm[MediaPipeBridge.ThumbTip] = new Vector3(0.59f, 0.75f, 0);
    // 设置其他手指弯曲（指尖比指根更靠近手腕）
    lm[MediaPipeBridge.IndexMcp] = new Vector3(0.52f, 0.5f, 0);
    lm[MediaPipeBridge.IndexTip] = new Vector3(0.51f, 0.41f, 0);
    lm[MediaPipeBridge.MiddleMcp] = new Vector3(0.50f, 0.51f, 0);
    lm[MediaPipeBridge.MiddleTip] = new Vector3(0.49f, 0.41f, 0);
    lm[MediaPipeBridge.RingMcp] = new Vector3(0.48f, 0.50f, 0);
    lm[MediaPipeBridge.RingTip] = new Vector3(0.47f, 0.41f, 0);
    lm[MediaPipeBridge.PinkyMcp] = new Vector3(0.46f, 0.49f, 0);
    lm[MediaPipeBridge.PinkyTip] = new Vector3(0.45f, 0.41f, 0);

    float confidence = GestureClassifier.ClassifyThumbsUp(lm);
    Assert.Greater(confidence, 0.7f);
}
```

### 步骤 6：验证

1. 保存所有文件（`Ctrl+S`）
2. 等待 Unity 重新编译（左下角转圈）
3. 如果 Console 有红色报错，根据报错信息修改代码
4. 打开 `GestureTestScene`，按 Play
5. 将手做出 ThumbsUp 姿势对准摄像头，确认面板显示你的精灵图
6. （可选）打开 Test Runner，运行新添加的测试

### 添加新手势的完整代码链路

```
你做的事:
  GestureType.ThumbsUp (枚举)
  GestureClassifier.ClassifyThumbsUp() (分类逻辑)
  thumbsup.png (精灵图)
  GestureConfig 条目 (Inspector 配置)

自动发生的事:
  GestureService.ProcessOneFrame()
    → classifier.Classify(landmarks)
      → 遍历所有注册的分类器，包括你新注册的 ClassifyThumbsUp
      → 返回置信度最高的手势
    → 组装 GestureResult(Type=ThumbsUp, ...)
    → GestureEvents.InvokeGestureChanged(result)
      → 所有订阅者收到通知
    → GestureDisplayPanel.HandleGestureUpdated(result)
      → GestureConfig.GetSprite(ThumbsUp)
        → 在 Gesture Entries 中找到 Type=ThumbsUp 的条目
        → 返回 thumbsup.png 对应的 Sprite
      → 面板显示该精灵图
```

---

## 5. 调优现有分类器

### 查看分类器代码

所有分类逻辑在 `Assets/Scripts/GestureRecognition/Detection/GestureClassifier.cs` 中。

每个分类器是一个静态方法，接收 21 个关键点坐标，返回 0.0 ~ 1.0 的置信度：

```csharp
public static float ClassifyFist(Vector3[] lm)
{
    // 检查所有指尖是否比指根更靠近手腕（即弯曲）
    // 返回 0.0（完全不像握拳）到 1.0（非常像握拳）
}
```

### 调优方法

1. **降低置信度阈值**：在 `GestureConfig.asset` 的 Inspector 中调低 `Confidence Threshold`（默认 0.6）
2. **修改分类逻辑**：直接编辑对应的 `ClassifyXxx()` 方法
3. **用真实摄像头数据调试**：
   - 在 `GestureService.ProcessOneFrame()` 中添加 `Debug.Log(landmarks)` 查看真实关键点数据
   - 根据观察到的数据规律调整分类条件

### 常用辅助方法

```csharp
// 判断某根手指是否弯曲（指尖比指根更靠近手腕）
bool curled = GestureClassifier.IsFingerCurled(landmarks, tipIdx, mcpIdx, wristIdx);

// 判断某根手指是否伸展（上面的反义）
bool extended = GestureClassifier.IsFingerExtended(landmarks, tipIdx, mcpIdx, wristIdx);
```

### 关键点索引（用于分类逻辑）

所有索引常量定义在 `MediaPipeBridge` 中：

```
MediaPipeBridge.Wrist       = 0     手腕
MediaPipeBridge.ThumbCmc    = 1     拇指根部
MediaPipeBridge.ThumbMcp    = 2     拇指第一关节
MediaPipeBridge.ThumbIp     = 3     拇指第二关节
MediaPipeBridge.ThumbTip    = 4     拇指尖
MediaPipeBridge.IndexMcp    = 5     食指根部
MediaPipeBridge.IndexPip    = 6     食指第一关节
MediaPipeBridge.IndexDip    = 7     食指第二关节
MediaPipeBridge.IndexTip    = 8     食指尖
MediaPipeBridge.MiddleMcp   = 9     中指根部
MediaPipeBridge.MiddleTip   = 12    中指尖
MediaPipeBridge.RingMcp     = 13    无名指根部
MediaPipeBridge.RingTip     = 16    无名指尖
MediaPipeBridge.PinkyMcp    = 17    小指根部
MediaPipeBridge.PinkyTip    = 20    小指尖
```

---

## 6. 日常维护操作

### 6.1 禁用某个手势的精灵图

1. 选中 `Assets/Resources/GestureConfig.asset`
2. 在 Inspector 的 **Gesture Entries** 列表中，找到目标条目
3. 点击条目右侧的 **-** 按钮删除
4. `Ctrl+S` 保存

> 效果：分类器仍然会识别该手势并触发事件，但面板显示的精灵图会回退为默认的 None 图。

### 6.2 重新启用某个手势的精灵图

1. 在 **Gesture Entries** 底部点击 **+**
2. 设置 Type、Sprite、DisplayName
3. `Ctrl+S` 保存

### 6.3 更换精灵图

1. 将新 PNG 放入 `Assets/Resources/GestureSprites/`
2. 选中新图片 → Inspector 确认 **Texture Type** = **Sprite (2D and UI)** → **Apply**
3. 选中 `GestureConfig.asset`
4. 将新精灵图拖拽到对应条目的 **Sprite** 字段

### 6.4 生成占位符精灵图

菜单栏 → **Tools → Gesture Recognition → Generate Placeholder Sprites**

会在 `Assets/Resources/GestureSprites/` 下生成每种手势的 64×64 彩色占位符 PNG。

### 6.5 调整置信度阈值

选中 `GestureConfig.asset` → 拖动 **Confidence Threshold** 滑块

| 值 | 效果 |
|----|------|
| 0.3 ~ 0.4 | 非常宽松（容易误判） |
| 0.5 ~ 0.7 | 推荐范围 |
| 0.8 ~ 1.0 | 非常严格（可能漏判） |

### 6.6 调整手部追踪平滑度

打开 `Assets/Scripts/GestureRecognition/Service/GestureService.cs`，找到 `new HandTracker()` 那行，修改参数：

```csharp
_tracker = new HandTracker(0.3f);  // 0.1=非常平滑 ~ 1.0=无平滑
```

---

## 7. 后端人员的工作范围总结

**你需要负责的**：

| 工作 | 涉及文件 |
|------|----------|
| 添加新手势的枚举 | `Core/GestureType.cs` |
| 编写新手势的分类逻辑 | `Detection/GestureClassifier.cs` |
| 调优现有分类器 | `Detection/GestureClassifier.cs` |
| 提供手势精灵图 | `Resources/GestureSprites/` 目录 |
| 在 Inspector 中配置新手势条目 | `Resources/GestureConfig.asset` |
| （可选）编写新手势的单元测试 | `Tests/EditMode/GestureClassifierTests.cs` |

**你不需要修改的**（框架代码，已经完善）：

| 文件 | 原因 |
|------|------|
| `GestureEvents.cs` | 通用事件系统，自动转发任何手势类型 |
| `GestureResult.cs` | 通用数据结构，不因手势类型而变 |
| `GestureConfig.cs` | 通用配置类，Inspector 操作即可 |
| `GestureService.cs` | 通用编排器，自动调用所有注册的分类器 |
| `GesturePanelManager.cs` | 通用面板管理 |
| `GestureDisplayPanel.cs` | 通用显示面板 |
| `GestureOverlay.cs` | 调试覆盖层 |
| `CameraManager.cs` | 摄像头管理 |
| `MediaPipeBridge.cs` | MediaPipe 桥接（除非需要修改模型加载逻辑） |

**简单来说**：添加一个新手势只需要改 **2 个文件**（`GestureType.cs` + `GestureClassifier.cs`），加 **1 张图片**，在 **Inspector 中配一下**，就完成了。

---

## 8. 构建部署

### 构建前准备

1. 在 `Assets/` 下创建 `StreamingAssets/` 文件夹（如不存在）
2. 将 `hand_landmarker.bytes` 模型文件复制到 `Assets/StreamingAssets/`
3. 详细步骤参见项目根目录的 `MEDIAPIPE_SETUP.md`

### 命令行构建

```bash
"E:/Application/Develop/Software/Tuanjie_Hub/Hub/Editor/2022.3.62f3c1/Editor/Unity.exe" ^
  -batchmode -nographics -quit ^
  -projectPath "E:/Application/Develop/Software/Tuanjie_Hub/Project/COMP3329_TEST" ^
  -buildWindows64Player "Builds/Game.exe"
```

---

## 9. 常见问题

### Q: 编译报错怎么办？

A: 打开 Console（**Window → General → Console**），查看红色错误信息。常见原因：
- 枚举值重复——确保新枚举值的数字不与已有值冲突
- 方法名拼写错误——确保 `RegisterClassifier` 中的方法名与实际方法名一致
- 缺少 `using` 语句——分类器文件需要 `using GestureRecognition.Core;`

### Q: 新手势在面板中不显示精灵图？

A: 检查：
1. `GestureConfig.asset` 中是否添加了对应的条目
2. 条目的 Type 是否选对了
3. 精灵图是否已正确设置为 Sprite 类型（Texture Type = Sprite (2D and UI)）

### Q: Tests 和 Scenes 文件夹可以删除吗？

A: 可以。它们完全独立：
- `Tests/` — 自动化测试。删除后只是无法跑自动测试
- `Scenes/GestureTestScene.unity` — 手动测试场景。删除后只是没有手动测试环境
- 它们之间没有引用关系，删除任一个不影响其他文件和运行时代码
