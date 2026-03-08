# COMP3329 手势识别模块 (Gesture Recognition Module)

> **Unity 2022.3.62f3c1 LTS** | **MediaPipe Unity Plugin v0.16.3** | **Built-in Render Pipeline** | **C# .NET Standard 2.1**

本文档面向 **Unity 初学者**，详细介绍手势识别模块的架构、每个文件的作用、代码依赖关系，以及常见操作的分步指南。

---

## 目录

1. [项目概述](#1-项目概述)
2. [快速开始](#2-快速开始)
3. [目录结构总览](#3-目录结构总览)
4. [架构设计](#4-架构设计)
5. [文件详解](#5-文件详解)
   - [5.1 Core 层 — 数据类型与配置](#51-core-层--数据类型与配置)
   - [5.2 Detection 层 — 摄像头、MediaPipe、分类器](#52-detection-层--摄像头mediapipe分类器)
   - [5.3 Service 层 — 门面单例](#53-service-层--门面单例)
   - [5.4 UI 层 — 显示面板与调试覆盖层](#54-ui-层--显示面板与调试覆盖层)
   - [5.5 Editor — 编辑器工具](#55-editor--编辑器工具)
   - [5.6 程序集定义与配置文件](#56-程序集定义与配置文件)
6. [代码依赖关系图](#6-代码依赖关系图)
7. [常见操作指南](#7-常见操作指南)
   - [7.1 添加新手势](#71-添加新手势)
   - [7.2 禁用/启用某个手势的精灵图](#72-禁用启用某个手势的精灵图)
   - [7.3 更换手势精灵图](#73-更换手势精灵图)
   - [7.4 调整置信度阈值](#74-调整置信度阈值)
   - [7.5 调整手部跟踪平滑度](#75-调整手部跟踪平滑度)
   - [7.6 前端接入手势识别](#76-前端接入手势识别)
   - [7.7 运行测试场景](#77-运行测试场景)
   - [7.8 切换面板显示模式](#78-切换面板显示模式)
   - [7.9 生成占位符精灵图](#79-生成占位符精灵图)
8. [测试说明](#8-测试说明)
9. [构建部署](#9-构建部署)
10. [团队协作与 Git](#10-团队协作与-git)
11. [Unity 初学者速查](#11-unity-初学者速查)
12. [常见问题 (FAQ)](#12-常见问题-faq)

---

## 1. 项目概述

本项目是 COMP3329 课程的 2D 游戏项目，集成了基于 **MediaPipe** 的实时手势识别模块。该模块可以：

- 通过摄像头实时检测手部 21 个关键点 (landmarks)
- 识别 5 种手势：**Fist** (握拳)、**OpenPalm** (张开手掌)、**Push** (推掌)、**Lift** (举手)、**Shoot** (手枪手势)
- 提供可拖拽、可缩放的浮动面板，支持 3 种显示模式
- 输出手势类型、置信度、手部位置等数据供游戏前端使用
- 全部打包在 exe 中运行，无需外部 Python 进程

**团队分工**：手势识别模块由算法工程师开发，前端团队通过 `GestureService` 和 `GesturePanelManager` 的 API 接入。

---

## 2. 快速开始

### 2.1 首次运行测试场景

1. 打开 Unity Editor（通过 Tuanjie Hub 或直接启动）
2. 在 **Project** 窗口中导航到 `Assets/Scenes/`
3. 双击 **GestureTestScene** 打开场景
4. 确保场景中有一个名为 `GestureManager` 的 GameObject，上面挂载了 **GestureService** 组件（Inspector 中 `_autoStart` 已勾选）
5. 点击顶部工具栏的 **Play** 按钮（或按 `Ctrl+P`）
6. 系统会自动启动摄像头和手势识别，在面板中显示结果

### 2.2 前端快速接入

```csharp
using GestureRecognition.Core;
using GestureRecognition.Service;

// 方式一：订阅事件（推荐）
GestureEvents.OnGestureChanged += (result) =>
{
    Debug.Log($"手势变了: {result.Type}, 置信度: {result.Confidence}");
};

// 方式二：每帧轮询
void Update()
{
    var result = GestureService.Instance.CurrentResult;
    if (result.IsHandDetected)
    {
        // 使用 result.Type, result.Confidence, result.HandPosition
    }
}

// 显示/隐藏面板
GesturePanelManager.Instance.ShowPanel();
GesturePanelManager.Instance.HidePanel();
```

---

## 3. 目录结构总览

```
COMP3329_TEST/                          # 项目根目录
├── AGENTS.md                           # AI 编码助手指令文件
├── MEDIAPIPE_SETUP.md                  # MediaPipe 安装指南
├── README.md                           # ← 本文件
│
├── Assets/                             # ★ 所有用户内容的根目录
│   ├── Scenes/
│   │   ├── SampleScene.unity           # 默认空场景
│   │   └── GestureTestScene.unity      # 手势识别测试/演示场景
│   │
│   ├── Scripts/GestureRecognition/     # ★ 手势识别模块代码
│   │   ├── GestureRecognition.asmdef   # 运行时程序集定义
│   │   ├── AssemblyInfo.cs             # 程序集级别属性
│   │   ├── Core/                       # 数据类型、事件、配置
│   │   │   ├── GestureType.cs          #   手势枚举
│   │   │   ├── GestureResult.cs        #   单帧识别结果
│   │   │   ├── GestureEvents.cs        #   全局事件总线
│   │   │   └── GestureConfig.cs        #   ScriptableObject 配置
│   │   ├── Detection/                  # 检测层：摄像头、MediaPipe、分类
│   │   │   ├── CameraManager.cs        #   WebCamTexture 管理
│   │   │   ├── MediaPipeBridge.cs      #   MediaPipe 桥接（条件编译）
│   │   │   ├── GestureClassifier.cs    #   手势分类器（纯逻辑）
│   │   │   └── HandTracker.cs          #   手掌中心追踪 + 平滑
│   │   ├── Service/                    # 门面层：对外 API
│   │   │   ├── GestureService.cs       #   核心单例，编排整个流水线
│   │   │   └── GesturePanelManager.cs  #   面板管理便捷 API
│   │   ├── UI/                         # 界面层：显示面板与调试覆盖
│   │   │   ├── GestureDisplayPanel.cs  #   可拖拽可缩放浮动面板
│   │   │   └── GestureOverlay.cs       #   调试用关键点可视化
│   │   └── Editor/                     # 编辑器专用工具
│   │       ├── GenerateGestureSprites.cs  # 菜单：生成占位符精灵
│   │       └── GestureRecognition.Editor.asmdef
│   │
│   ├── Tests/                          # 单元测试与集成测试
│   │   ├── EditMode/
│   │   │   ├── EditModeTests.asmdef
│   │   │   └── GestureClassifierTests.cs  # 24 个编辑模式测试
│   │   └── PlayMode/
│   │       ├── PlayModeTests.asmdef
│   │       └── GestureIntegrationTests.cs # 15 个运行模式测试
│   │
│   └── Resources/                      # Resources.Load() 可加载资产
│       ├── GestureConfig.asset         # 手势配置资产实例
│       └── GestureSprites/             # 手势精灵图
│           ├── fist.png                #   用户制作的精灵
│           ├── lift.png
│           ├── shoot.png
│           ├── OpenPalm.png
│           └── Gesture_*.png           #   程序生成的占位符精灵
│
├── Packages/                           # UPM 包清单
│   ├── manifest.json                   #   包依赖声明
│   └── packages-lock.json             #   锁定版本
│
├── ProjectSettings/                    # 项目配置（需要版本控制）
│   ├── ProjectVersion.txt              #   Unity 版本号
│   └── ...                             #   各种编辑器配置
│
├── UserSettings/                       # 个人偏好（不需要版本控制）
├── Library/                            # 编辑器缓存（不要手动编辑）
├── Logs/                               # 日志文件
└── Temp/                               # 临时运行文件
```

### 为什么有 `.meta` 文件？

Unity 为 `Assets/` 下每个文件和文件夹生成 `.meta` 文件。`.meta` 文件包含该资产的唯一 GUID（全局唯一标识符）和导入设置。**绝对不要手动编辑或删除 `.meta` 文件**——如果 GUID 变了，所有引用该资产的地方都会丢失关联。提交代码时必须同时提交 `.meta` 文件。

---

## 4. 架构设计

### 4.1 分层架构

模块采用 **四层架构**，依赖方向是单向的（上层依赖下层，下层不依赖上层）：

```
┌─────────────────────────────────────────────────────────┐
│                     UI 层 (UI/)                         │
│   GestureDisplayPanel  ·  GestureOverlay                │
│   职责：显示手势面板、调试可视化                          │
├─────────────────────────────────────────────────────────┤
│                   Service 层 (Service/)                  │
│   GestureService  ·  GesturePanelManager                │
│   职责：对外 API、编排流水线、管理生命周期                │
├─────────────────────────────────────────────────────────┤
│                 Detection 层 (Detection/)                │
│   CameraManager · MediaPipeBridge · GestureClassifier   │
│   · HandTracker                                         │
│   职责：摄像头管理、关键点检测、手势分类、位置跟踪        │
├─────────────────────────────────────────────────────────┤
│                    Core 层 (Core/)                       │
│   GestureType · GestureResult · GestureEvents           │
│   · GestureConfig                                       │
│   职责：定义数据类型、事件、可配置项                      │
└─────────────────────────────────────────────────────────┘
```

### 4.2 数据流

每一帧的处理流程：

```
摄像头画面 (WebCamTexture)
    │
    ▼
CameraManager.GetCurrentFrame()        → 获取 CPU 端 Texture2D
    │
    ▼
MediaPipeBridge.ProcessFrame(texture)   → 输出 HandLandmarkData（21 个 3D 关键点）
    │
    ├──▶ GestureClassifier.Classify()   → 输出 GestureType + Confidence
    │
    └──▶ HandTracker.Update()           → 输出 平滑后的手部位置 (Vector2)
          │
          ▼
    组装 GestureResult
          │
          ▼
    GestureEvents 触发事件              → 前端订阅者收到通知
          │
          ▼
    GestureDisplayPanel 更新显示        → 用户看到手势精灵/摄像头画面
```

### 4.3 单例模式

`GestureService` 和 `GesturePanelManager` 都使用单例模式（`Instance` 属性）。这意味着整个游戏中只会有一个实例：

- **GestureService.Instance** — 核心服务，管理整个识别流水线
- **GesturePanelManager.Instance** — 面板管理器，提供一行代码的面板操作

> **Unity 初学者注意**：单例是通过 `Awake()` 方法实现的。场景中只需放置一个挂载了该脚本的 GameObject。如果出现多个，多余的会自动销毁。

### 4.4 条件编译

`MediaPipeBridge.cs` 使用 `#if MEDIAPIPE_INSTALLED` 条件编译：

- **已安装 MediaPipe 插件**（当前状态）：使用真实的 `HandLandmarker` API 进行手部关键点检测
- **未安装插件**：使用 stub（桩）模式，`ProcessFrame()` 始终返回"未检测到手"，但 `InjectMockData()` 仍然可以工作

`MEDIAPIPE_INSTALLED` 宏由 `.asmdef` 中的 `versionDefines` 自动定义——当检测到 `com.github.homuler.mediapipe` 包版本 >= 0.16.0 时自动生效，无需手动设置。

---

## 5. 文件详解

### 5.1 Core 层 — 数据类型与配置

#### `GestureType.cs` — 手势枚举

| 项目 | 说明 |
|------|------|
| **路径** | `Assets/Scripts/GestureRecognition/Core/GestureType.cs` |
| **命名空间** | `GestureRecognition.Core` |
| **类型** | `public enum GestureType` |
| **行数** | ~50 行 |

定义所有可识别的手势类型：

```csharp
public enum GestureType
{
    None = 0,       // 无手势 / 未检测到手
    Push = 1,       // 张开手掌向前推
    Lift = 2,       // 张开手掌向上举
    Shoot = 3,      // 手枪手势（食指伸出，其余弯曲）
    Fist = 4,       // 握拳
    OpenPalm = 5,   // 五指张开
    Count           // 哨兵值，等于手势总数（用于遍历）
}
```

**为什么有 `Count`？** 这是 C# 枚举的常见技巧。`(int)GestureType.Count` 等于 6，可以用来创建固定大小的数组或做循环遍历。

**被谁引用**：几乎所有文件都引用此枚举。

---

#### `GestureResult.cs` — 单帧识别结果

| 项目 | 说明 |
|------|------|
| **路径** | `Assets/Scripts/GestureRecognition/Core/GestureResult.cs` |
| **命名空间** | `GestureRecognition.Core` |
| **类型** | `public readonly struct GestureResult` |
| **行数** | ~68 行 |

封装一帧的完整识别结果：

| 属性 | 类型 | 说明 |
|------|------|------|
| `Type` | `GestureType` | 检测到的手势类型 |
| `Confidence` | `float` | 置信度 [0, 1]，构造时自动 Clamp |
| `HandPosition` | `Vector2` | 归一化手部中心位置 [0,1]；无手时为 (-1,-1) |
| `IsHandDetected` | `bool` | 是否检测到手 |
| `Timestamp` | `float` | 自识别启动以来的秒数 |

**关键设计**：使用 `readonly struct` 而非 `class`，因为这个结构每帧都会创建新实例，值类型避免了 GC 压力。

`GestureResult.Empty` 是一个静态属性，返回"未检测到任何东西"的默认结果。

---

#### `GestureEvents.cs` — 全局事件总线

| 项目 | 说明 |
|------|------|
| **路径** | `Assets/Scripts/GestureRecognition/Core/GestureEvents.cs` |
| **命名空间** | `GestureRecognition.Core` |
| **类型** | `public static class GestureEvents` |
| **行数** | ~96 行 |

提供 5 个静态事件，供前端订阅：

| 事件 | 参数 | 触发时机 |
|------|------|----------|
| `OnGestureUpdated` | `GestureResult` | 每帧触发（不管手势有没有变化） |
| `OnGestureChanged` | `GestureResult` | 手势类型发生变化时触发 |
| `OnHandPositionUpdated` | `Vector2` | 每帧触发（当检测到手时） |
| `OnHandDetectionChanged` | `bool` | 手的检测状态变化时触发（出现/消失） |
| `OnRecognitionStateChanged` | `bool` | 识别系统启动/停止时触发 |

**设计要点**：

- 触发方法（`InvokeXxx()`）是 `internal` 访问级别，只有同一程序集内的代码（即 `GestureService`）可以调用
- `ClearAll()` 方法清除所有订阅者，防止内存泄漏。在 `GestureService.OnDestroy()` 中调用
- 通过 `AssemblyInfo.cs` 的 `InternalsVisibleTo`，测试程序集也能访问这些 internal 方法

**前端使用示例**：

```csharp
// 在 OnEnable 中订阅
void OnEnable()
{
    GestureEvents.OnGestureChanged += HandleGestureChanged;
}

// 在 OnDisable 中退订（防止内存泄漏！）
void OnDisable()
{
    GestureEvents.OnGestureChanged -= HandleGestureChanged;
}

private void HandleGestureChanged(GestureResult result)
{
    if (result.Type == GestureType.Fist)
    {
        // 玩家握拳了，执行攻击
    }
}
```

---

#### `GestureConfig.cs` — 手势配置 (ScriptableObject)

| 项目 | 说明 |
|------|------|
| **路径** | `Assets/Scripts/GestureRecognition/Core/GestureConfig.cs` |
| **命名空间** | `GestureRecognition.Core` |
| **类型** | `public class GestureConfig : ScriptableObject` |
| **行数** | ~119 行 |
| **资产实例** | `Assets/Resources/GestureConfig.asset` |

**什么是 ScriptableObject？**

ScriptableObject 是 Unity 中用来存储数据的特殊类。与 MonoBehaviour 不同，它不需要挂载到 GameObject 上，而是以 `.asset` 文件的形式存在于项目中。你可以把它理解为一个"数据容器"——在 Inspector 中编辑数据，代码在运行时读取。

**GestureConfig 包含什么**：

| Inspector 字段 | 类型 | 说明 |
|----------------|------|------|
| `Confidence Threshold` | `float` (滑块 0-1) | 最低置信度阈值，默认 0.6。低于此值的手势会被判定为 None |
| `None Sprite` | `Sprite` | 未检测到手势时显示的精灵图 |
| `Gesture Entries` | `List<GestureEntry>` | 手势条目列表，每项包含 Type、Sprite、DisplayName |

**GestureEntry 结构**：

```csharp
[Serializable]
public class GestureEntry
{
    public GestureType Type;    // 手势类型
    public Sprite Sprite;       // 对应精灵图
    public string DisplayName;  // 显示名称
}
```

**关键方法**：

- `GetSprite(GestureType)` — 查找对应手势的精灵图，找不到则返回 `NoneSprite`
- `GetDisplayName(GestureType)` — 查找对应手势的显示名称，找不到则返回枚举名

> **操作提示**：在 Inspector 中修改 `GestureConfig.asset` 是配置手势显示的主要方式。详见 [7.2 禁用/启用某个手势的精灵图](#72-禁用启用某个手势的精灵图)。

---

### 5.2 Detection 层 — 摄像头、MediaPipe、分类器

#### `CameraManager.cs` — 摄像头管理

| 项目 | 说明 |
|------|------|
| **路径** | `Assets/Scripts/GestureRecognition/Detection/CameraManager.cs` |
| **命名空间** | `GestureRecognition.Detection` |
| **类型** | `public class CameraManager : MonoBehaviour` |
| **行数** | ~195 行 |

管理 `WebCamTexture` 的生命周期（创建、启动、停止、销毁）。

| 方法 / 属性 | 说明 |
|-------------|------|
| `StartCamera(deviceName)` | 协程：创建 WebCamTexture，启动摄像头，等待画面就绪（`width > 16`），分配 CPU 缓冲区 |
| `StopCamera()` | 停止摄像头，销毁纹理，释放缓冲区 |
| `GetCurrentFrame()` | 将当前帧复制到 CPU 端 Texture2D 并返回（供 MediaPipe 处理） |
| `CameraTexture` | 返回 WebCamTexture 实例（UI 用来显示实时画面） |
| `IsRunning` | 摄像头是否正在捕获 |
| `OnCameraReady` | 事件：摄像头首帧就绪时触发 |
| `GetAvailableDevices()` | 静态方法：返回所有可用摄像头设备名 |

**为什么 `width > 16`？** WebCamTexture 在实际开始输出画面之前，其 `width` 会是 16（Unity 的占位值）。代码通过 `WaitUntil(() => _webCamTexture.width > 16)` 来确保摄像头真正就绪。

---

#### `MediaPipeBridge.cs` — MediaPipe 桥接

| 项目 | 说明 |
|------|------|
| **路径** | `Assets/Scripts/GestureRecognition/Detection/MediaPipeBridge.cs` |
| **命名空间** | `GestureRecognition.Detection` |
| **类型** | `public class MediaPipeBridge : MonoBehaviour` + `public struct HandLandmarkData` |
| **行数** | ~456 行（最大的文件） |

这是整个模块最核心的文件，桥接 Unity 与 MediaPipe 手部关键点检测。

**HandLandmarkData 结构**：

```csharp
public struct HandLandmarkData
{
    public Vector3[] Landmarks;  // 21 个关键点，每个包含 (x, y, z)
    public bool IsValid;         // 数据是否有效
}
```

**关键点索引常量**（对应 MediaPipe 手部模型的 21 个点）：

```
              中指 (12)
    食指 (8)   │    无名指 (16)
      │       │      │     小指 (20)
      │       │      │       │
   ┌──┤    ┌──┤   ┌──┤    ┌──┤
   │ (7)   │(11)  │(15)   │(19)
   │ (6)   │(10)  │(14)   │(18)
   │       │      │       │
   └──(5)──┴──(9)─┴──(13)─┴──(17)──┐
       │                             │
  拇指 (4)                           │
    │                                │
   (3)         手掌                  │
    │                                │
   (2)                               │
    │                                │
   (1)───────────────────────────────│
                                     │
                    (0) 手腕 ────────┘
```

| 常量 | 值 | 说明 |
|------|-----|------|
| `Wrist` | 0 | 手腕 |
| `ThumbCmc` ~ `ThumbTip` | 1-4 | 拇指 |
| `IndexMcp` ~ `IndexTip` | 5-8 | 食指 |
| `MiddleMcp` ~ `MiddleTip` | 9-12 | 中指 |
| `RingMcp` ~ `RingTip` | 13-16 | 无名指 |
| `PinkyMcp` ~ `PinkyTip` | 17-20 | 小指 |

**两种初始化方法**：

| 方法 | 类型 | 用途 |
|------|------|------|
| `Initialize()` | 同步 | 桩模式 / 测试用。直接将 `IsInitialized` 设为 true |
| `InitializeAsync()` | 协程 | 真实模式。加载 `hand_landmarker.bytes` 模型，创建 `HandLandmarker` 实例 |

**条件编译分支**：

```csharp
#if MEDIAPIPE_INSTALLED
    // 真实模式：使用 HandLandmarker Task API
    // RunningMode.VIDEO, CPU 委托
    // TryDetectForVideo() 处理每帧
#else
    // 桩模式：ProcessFrame() 始终返回空数据
    // InjectMockData() 仍可工作
#endif
```

**命名空间注意**：MediaPipe 插件的命名空间是 `Mediapipe`（小写 p），不是 `MediaPipe`。`NormalizedLandmark` 类型同时存在于 `Mediapipe` 和 `Mediapipe.Tasks.Components.Containers` 中，代码使用完全限定名避免歧义。

---

#### `GestureClassifier.cs` — 手势分类器

| 项目 | 说明 |
|------|------|
| **路径** | `Assets/Scripts/GestureRecognition/Detection/GestureClassifier.cs` |
| **命名空间** | `GestureRecognition.Detection` |
| **类型** | `public class GestureClassifier`（纯 C# 类，非 MonoBehaviour） |
| **行数** | ~319 行 |

**纯逻辑类**——不继承 MonoBehaviour，不挂载到 GameObject，由 `GestureService` 在代码中 `new` 出来。

**内置 5 个分类器**：

| 分类器 | 识别逻辑 |
|--------|----------|
| `ClassifyFist` | 检查所有指尖是否弯曲（指尖比指根更靠近手腕） |
| `ClassifyOpenPalm` | 检查所有指尖是否伸展 |
| `ClassifyPush` | 张开手掌 + z 轴方差小（手掌平面朝前） |
| `ClassifyLift` | 张开手掌 + 手腕低于指尖（手掌向上） |
| `ClassifyShoot` | 食指伸展 + 其余手指弯曲 |

**分类流程**：

```
输入 21 个关键点
    │
    ▼
遍历所有注册的分类器
    │
    ▼
每个分类器返回置信度 [0, 1]
    │
    ▼
选择置信度最高的（须超过阈值）
    │
    ▼
输出 GestureType + Confidence
```

**可扩展性**——运行时注册自定义分类器：

```csharp
// 注册一个新的自定义手势分类器
classifier.RegisterClassifier(GestureType.Push, (landmarks) =>
{
    // 你的自定义逻辑
    return confidenceScore; // 0.0 ~ 1.0
});
```

**辅助方法**：

- `IsFingerCurled(landmarks, tipIdx, mcpIdx, wristIdx)` — 指尖比指根更靠近手腕 → 手指弯曲
- `IsFingerExtended(...)` — `IsFingerCurled` 的反义

---

#### `HandTracker.cs` — 手部位置追踪

| 项目 | 说明 |
|------|------|
| **路径** | `Assets/Scripts/GestureRecognition/Detection/HandTracker.cs` |
| **命名空间** | `GestureRecognition.Detection` |
| **类型** | `public class HandTracker`（纯 C# 类） |
| **行数** | ~143 行 |

计算手掌中心位置并进行指数平滑。

**手掌中心计算**：取 5 个点的平均值：
- Wrist (0), IndexMcp (5), MiddleMcp (9), RingMcp (13), PinkyMcp (17)

**指数平滑（Exponential Smoothing）**：

```
新位置 = Lerp(旧位置, 原始位置, smoothingFactor)
```

`smoothingFactor` 越大，跟踪越灵敏但越抖动；越小，越平滑但延迟越高。默认 0.5。

---

### 5.3 Service 层 — 门面单例

#### `GestureService.cs` — 核心服务

| 项目 | 说明 |
|------|------|
| **路径** | `Assets/Scripts/GestureRecognition/Service/GestureService.cs` |
| **命名空间** | `GestureRecognition.Service` |
| **类型** | `public class GestureService : MonoBehaviour`（单例） |
| **行数** | ~345 行 |

**整个模块的总指挥**。编排 Camera → MediaPipe → Classifier → Tracker → Events 的完整流水线。

**生命周期**：

```
Awake()
  ├── 单例初始化（如有重复实例则销毁自己）
  ├── 加载 GestureConfig（从 Resources/GestureConfig）
  ├── 获取/添加 CameraManager 组件
  ├── 获取/添加 MediaPipeBridge 组件
  ├── new GestureClassifier(config.ConfidenceThreshold)
  └── new HandTracker()

StartRecognition()
  └── 协程: StartCamera → InitializeAsync(MediaPipe) → 主循环(ProcessOneFrame)

ProcessOneFrame()  [每帧调用]
  ├── CameraManager.GetCurrentFrame()
  ├── MediaPipeBridge.ProcessFrame(frame)
  ├── GestureClassifier.Classify(landmarks)
  ├── HandTracker.Update(landmarkData)
  ├── 组装 GestureResult
  └── GestureEvents.InvokeXxx()

StopRecognition()
  ├── 停止协程
  ├── CameraManager.StopCamera()
  ├── MediaPipeBridge.Shutdown()
  └── GestureEvents.InvokeRecognitionStateChanged(false)

OnDestroy()
  ├── StopRecognition()
  └── GestureEvents.ClearAll()
```

**对外 API**：

| 属性/方法 | 说明 |
|-----------|------|
| `Instance` | 全局单例访问 |
| `IsRunning` | 识别是否运行中 |
| `CurrentResult` | 最新一帧的识别结果 |
| `Config` | GestureConfig 引用 |
| `Camera` | CameraManager 引用（可访问摄像头纹理） |
| `Bridge` | MediaPipeBridge 引用（高级用法/测试） |
| `StartRecognition()` | 启动识别 |
| `StopRecognition()` | 停止识别 |
| `GetAvailableCameras()` | 获取可用摄像头列表 |
| `SwitchCamera(deviceName)` | 切换摄像头 |

---

#### `GesturePanelManager.cs` — 面板管理便捷 API

| 项目 | 说明 |
|------|------|
| **路径** | `Assets/Scripts/GestureRecognition/Service/GesturePanelManager.cs` |
| **命名空间** | `GestureRecognition.Service` |
| **类型** | `public class GesturePanelManager : MonoBehaviour`（单例） |
| **行数** | ~312 行 |

为前端提供 **一行代码** 的面板操作 API。

| 方法 | 说明 |
|------|------|
| `ShowPanel()` | 显示面板（不存在则自动创建），自动启动识别 |
| `HidePanel()` | 隐藏面板，自动停止识别 |
| `TogglePanel()` | 切换面板可见性 |
| `SetDisplayMode(mode)` | 设置显示模式 |
| `SetPanelSize(width, height)` | 设置面板大小 |

**面板创建策略**（按优先级）：
1. 先查找场景中已有的 `GestureDisplayPanel`
2. 尝试从 Resources 加载预制体
3. 全部失败则从代码动态创建（包括 Canvas、EventSystem）

---

### 5.4 UI 层 — 显示面板与调试覆盖层

#### `GestureDisplayPanel.cs` — 可拖拽可缩放浮动面板

| 项目 | 说明 |
|------|------|
| **路径** | `Assets/Scripts/GestureRecognition/UI/GestureDisplayPanel.cs` |
| **命名空间** | `GestureRecognition.UI` |
| **类型** | `public class GestureDisplayPanel : MonoBehaviour` |
| **行数** | ~665 行（第二大文件） |

**自构建 UI**——在 `Awake()` 中通过代码创建完整的 UI 层级结构：

```
GestureDisplayPanel (RectTransform + CanvasGroup + Image 背景)
├── TitleBar (拖拽区域)
│   ├── TitleText ("Gesture Recognition")
│   └── CloseButton ("X")
├── ContentArea (带遮罩的内容区)
│   ├── CameraImage (RawImage, 显示摄像头画面)
│   └── GestureImage (Image, 显示手势精灵图)
├── LabelBar (底部信息栏)
│   ├── GestureLabel ("Gesture: None")
│   └── ConfidenceLabel ("Confidence: 0.0%")
└── ResizeHandle (右下角缩放手柄)
```

**三种显示模式**：

| 模式 | 枚举值 | 效果 |
|------|--------|------|
| 摄像头模式 | `CameraFeed` | 显示实时摄像头画面 |
| 卡通精灵模式 | `CartoonSprite` | 显示手势对应的精灵图（隐私模式） |
| 叠加模式 | `CameraWithOverlay` | 摄像头画面 + 右下角小精灵图 |

**交互功能**：
- **拖拽**：按住标题栏拖动面板（通过内嵌 `PanelDragHandler` 类实现）
- **缩放**：按住右下角手柄拖动缩放面板（通过内嵌 `PanelResizeHandler` 类实现）
- **关闭**：点击右上角 X 按钮隐藏面板
- **最小/最大尺寸**：缩放有限制，不会太小或太大

---

#### `GestureOverlay.cs` — 调试关键点可视化

| 项目 | 说明 |
|------|------|
| **路径** | `Assets/Scripts/GestureRecognition/UI/GestureOverlay.cs` |
| **命名空间** | `GestureRecognition.UI` |
| **类型** | `public class GestureOverlay : MonoBehaviour` |
| **行数** | ~175 行 |

在面板上绘制 21 个关键点的彩色圆点，用于调试手部检测是否准确。

| Inspector 字段 | 默认值 | 说明 |
|----------------|--------|------|
| `Landmark Color` | 绿色 | 圆点颜色 |
| `Dot Size` | 8 | 圆点直径（像素） |
| `Show Connections` | true | 是否显示关键点之间的连线 |

---

### 5.5 Editor — 编辑器工具

#### `GenerateGestureSprites.cs` — 占位符精灵生成器

| 项目 | 说明 |
|------|------|
| **路径** | `Assets/Scripts/GestureRecognition/Editor/GenerateGestureSprites.cs` |
| **命名空间** | `GestureRecognition.Editor` |
| **类型** | `public static class GenerateGestureSprites` |
| **行数** | ~125 行 |

提供两个编辑器菜单项：

| 菜单路径 | 功能 |
|----------|------|
| `Tools > Gesture Recognition > Generate Placeholder Sprites` | 为每种手势生成 64x64 彩色 PNG 占位符图 |
| `Tools > Gesture Recognition > Create GestureConfig Asset` | 创建 `DefaultGestureConfig.asset` |

生成的精灵保存到 `Assets/Resources/GestureSprites/` 目录。

> **注意**：此文件用 `#if UNITY_EDITOR` 包裹，只在编辑器中编译，不会打包到最终构建中。

---

### 5.6 程序集定义与配置文件

#### `GestureRecognition.asmdef` — 运行时程序集定义

**什么是 `.asmdef`？**

Assembly Definition 文件告诉 Unity 如何将代码组织成独立的编译单元（程序集）。好处：
- **增量编译**：修改一个脚本只需重新编译该程序集，而非所有代码
- **依赖控制**：明确声明哪些程序集可以互相引用
- **条件宏**：可以根据包安装情况自动定义编译宏

```json
{
    "name": "GestureRecognition",
    "references": ["Mediapipe.Runtime"],
    "versionDefines": [{
        "name": "com.github.homuler.mediapipe",
        "expression": "0.16.0",
        "define": "MEDIAPIPE_INSTALLED"
    }]
}
```

关键点：
- 引用了 `Mediapipe.Runtime`（MediaPipe 插件的运行时程序集）
- 当 MediaPipe 包版本 >= 0.16.0 时，自动定义 `MEDIAPIPE_INSTALLED` 宏

#### `GestureRecognition.Editor.asmdef` — 编辑器程序集

```json
{
    "name": "GestureRecognition.Editor",
    "references": ["GestureRecognition"],
    "includePlatforms": ["Editor"]
}
```

- 只在 Editor 平台编译
- 引用主运行时程序集

#### `AssemblyInfo.cs` — 程序集级别属性

```csharp
[assembly: InternalsVisibleTo("EditModeTests")]
[assembly: InternalsVisibleTo("PlayModeTests")]
```

允许测试程序集访问 `GestureRecognition` 程序集中的 `internal` 成员（如 `GestureEvents.InvokeXxx()` 方法）。

---

## 6. 代码依赖关系图

### 6.1 文件级依赖（谁引用了谁）

```
┌────────────────────── Core 层 ──────────────────────┐
│                                                      │
│  GestureType ◄─────── GestureResult                 │
│      ▲                     ▲                         │
│      │                     │                         │
│  GestureConfig        GestureEvents                  │
│      ▲                     ▲                         │
└──────┼─────────────────────┼─────────────────────────┘
       │                     │
┌──────┼── Detection 层 ─────┼─────────────────────────┐
│      │                     │                         │
│  CameraManager        MediaPipeBridge                │
│  (独立)               (独立, 定义 HandLandmarkData)  │
│      ▲                  ▲      ▲                     │
│      │                  │      │                     │
│      │        GestureClassifier│                     │
│      │        (引用 GestureType│+ MediaPipeBridge    │
│      │         的常量)         │                     │
│      │                  │      │                     │
│      │             HandTracker │                     │
│      │        (引用 MediaPipeBridge 的常量和          │
│      │         HandLandmarkData)                     │
└──────┼──────────────────┼──────┼─────────────────────┘
       │                  │      │
┌──────┼── Service 层 ────┼──────┼─────────────────────┐
│      │                  │      │                     │
│  GestureService ◄───────┘      │                     │
│  (引用 Core 全部 +              │                     │
│   Detection 全部)               │                     │
│      ▲                          │                     │
│      │                          │                     │
│  GesturePanelManager            │                     │
│  (引用 GestureDisplayPanel)     │                     │
│      ▲                          │                     │
└──────┼──────────────────────────┼────────────────────┘
       │                          │
┌──────┼───── UI 层 ──────────────┼────────────────────┐
│      │                          │                     │
│  GestureDisplayPanel            │                     │
│  (引用 Core: Config, Result,    │                     │
│   Events, Type                  │                     │
│   引用 Service: GestureService) │                     │
│                                 │                     │
│  GestureOverlay                 │                     │
│  (引用 Detection: MediaPipeBridge,                    │
│   HandLandmarkData              │                     │
│   引用 Service: GestureService) │                     │
└──────────────────────────────────────────────────────┘

┌─── Editor ────────────────────────────────────────────┐
│  GenerateGestureSprites                               │
│  (引用 Core: GestureConfig)                           │
└───────────────────────────────────────────────────────┘
```

### 6.2 详细依赖矩阵

| 文件 | 依赖的项目文件 |
|------|---------------|
| `GestureType.cs` | 无 |
| `GestureResult.cs` | GestureType |
| `GestureEvents.cs` | GestureResult |
| `GestureConfig.cs` | GestureType |
| `CameraManager.cs` | 无（独立） |
| `MediaPipeBridge.cs` | 无（独立，定义 HandLandmarkData） |
| `GestureClassifier.cs` | GestureType, MediaPipeBridge（常量） |
| `HandTracker.cs` | MediaPipeBridge（常量 + HandLandmarkData） |
| `GestureService.cs` | **全部 Core** + **全部 Detection** |
| `GesturePanelManager.cs` | GestureDisplayPanel, GestureConfig（间接） |
| `GestureDisplayPanel.cs` | GestureConfig, GestureResult, GestureEvents, GestureType, GestureService |
| `GestureOverlay.cs` | MediaPipeBridge, HandLandmarkData, GestureService |
| `GenerateGestureSprites.cs` | GestureConfig |
| `AssemblyInfo.cs` | 无 |

### 6.3 外部依赖

| 依赖 | 说明 |
|------|------|
| `Mediapipe.Runtime` | MediaPipe Unity Plugin 的运行时程序集，只在 `#if MEDIAPIPE_INSTALLED` 分支使用 |
| `UnityEngine` | Unity 引擎核心 |
| `UnityEngine.UI` | Unity UI 系统（Canvas, Image, RawImage, Text 等） |
| `UnityEngine.EventSystems` | 事件系统（拖拽、点击等 UI 交互） |
| `UnityEditor` | Unity 编辑器 API（仅 Editor 程序集使用） |

---

## 7. 常见操作指南

### 7.1 添加新手势

**场景**：你想添加一个"竖大拇指 (ThumbsUp)"手势。

**步骤 1：添加枚举值**

打开 `Assets/Scripts/GestureRecognition/Core/GestureType.cs`，在 `Count` 之前添加新值：

```csharp
OpenPalm = 5,
ThumbsUp = 6,    // ← 新增
Count
```

**步骤 2：编写分类器**

打开 `Assets/Scripts/GestureRecognition/Detection/GestureClassifier.cs`，添加一个静态分类方法：

```csharp
public static float ClassifyThumbsUp(Vector3[] lm)
{
    // 拇指伸展，其余手指弯曲
    bool thumbExtended = IsFingerExtended(lm, MediaPipeBridge.ThumbTip,
                                          MediaPipeBridge.ThumbMcp,
                                          MediaPipeBridge.Wrist);
    bool othersCurled = IsFingerCurled(lm, MediaPipeBridge.IndexTip,
                                       MediaPipeBridge.IndexMcp,
                                       MediaPipeBridge.Wrist)
                     && IsFingerCurled(lm, MediaPipeBridge.MiddleTip,
                                       MediaPipeBridge.MiddleMcp,
                                       MediaPipeBridge.Wrist)
                     && IsFingerCurled(lm, MediaPipeBridge.RingTip,
                                       MediaPipeBridge.RingMcp,
                                       MediaPipeBridge.Wrist)
                     && IsFingerCurled(lm, MediaPipeBridge.PinkyTip,
                                       MediaPipeBridge.PinkyMcp,
                                       MediaPipeBridge.Wrist);

    if (thumbExtended && othersCurled) return 0.9f;
    return 0.0f;
}
```

然后在构造函数中注册：

```csharp
public GestureClassifier(float confidenceThreshold = 0.6f)
{
    // ... 已有的注册 ...
    RegisterClassifier(GestureType.ThumbsUp, ClassifyThumbsUp);  // ← 新增
}
```

**步骤 3：准备精灵图**

1. 将 `thumbsup.png` 放入 `Assets/Resources/GestureSprites/`
2. 在 Inspector 中选择该图片，确认 **Texture Type** 设为 **Sprite (2D and UI)**
3. 点击 **Apply**

**步骤 4：配置 GestureConfig**

1. 在 Project 窗口中找到 `Assets/Resources/GestureConfig.asset`，单击选中
2. 在 Inspector 中找到 **Gesture Entries** 列表
3. 点击列表底部的 **+** 按钮添加新条目
4. 设置：
   - **Type** = ThumbsUp（下拉选择）
   - **Sprite** = 拖拽你的 `thumbsup.png` 到此处
   - **Display Name** = "Thumbs Up"

**步骤 5（可选）：编写单元测试**

在 `Assets/Tests/EditMode/GestureClassifierTests.cs` 中添加测试方法，验证新分类器返回预期的置信度。

**代码链路**：
```
GestureType.ThumbsUp (枚举)
  → GestureClassifier.ClassifyThumbsUp() (分类逻辑)
  → GestureService.ProcessOneFrame() 调用 classifier.Classify()
  → 返回 GestureResult(Type=ThumbsUp, ...)
  → GestureEvents.OnGestureChanged 触发
  → GestureDisplayPanel.HandleGestureUpdated()
  → GestureConfig.GetSprite(ThumbsUp) 查找精灵图
  → 面板显示对应精灵
```

---

### 7.2 禁用/启用某个手势的精灵图

**禁用精灵图**（分类器仍运行，但面板不显示特定图片）：

1. 选中 `Assets/Resources/GestureConfig.asset`
2. 在 **Gesture Entries** 列表中找到要禁用的条目
3. 点击条目右侧的 **-** 按钮删除它
4. 保存（`Ctrl+S`）

**效果**：当识别到该手势时，`GestureConfig.GetSprite()` 找不到对应条目，会返回 `NoneSprite`（默认精灵）。

**重新启用**：

1. 点击 **+** 添加新条目
2. 重新设置 Type、Sprite、DisplayName

**代码链路**：
```
GestureConfig.GetSprite(type)
  → 遍历 _gestureEntries 列表
  → 找不到匹配的 Type
  → 返回 _noneSprite（默认精灵）
```

> **注意**：这只影响显示。分类器不受 GestureConfig 条目的影响——`GestureClassifier` 仍会识别该手势并输出正确的 `GestureType`。`GestureEvents.OnGestureChanged` 仍会触发。

---

### 7.3 更换手势精灵图

1. 将新的精灵图（PNG）放入 `Assets/Resources/GestureSprites/`
2. 选中该图片，在 Inspector 中确认 **Texture Type** = **Sprite (2D and UI)**，点击 **Apply**
3. 选中 `Assets/Resources/GestureConfig.asset`
4. 在 **Gesture Entries** 中找到目标条目
5. 将新精灵图从 Project 窗口拖拽到 **Sprite** 字段

**或者用小圆圈选择器**：

5. 点击 Sprite 字段右侧的小圆圈图标 ⊙
6. 在弹出的窗口中搜索并选择新精灵

---

### 7.4 调整置信度阈值

**通过 Inspector（推荐）**：

1. 选中 `Assets/Resources/GestureConfig.asset`
2. 拖动 **Confidence Threshold** 滑块（0 ~ 1）
3. 值越高，分类越严格（需要更明确的手势才能被识别）
4. 值越低，分类越宽松（可能产生更多误判）
5. 推荐值：0.5 ~ 0.7

**代码链路**：
```
GestureConfig.ConfidenceThreshold
  → GestureService.Awake() 读取: new GestureClassifier(config.ConfidenceThreshold)
  → GestureClassifier.Classify() 中比较: if (confidence > _confidenceThreshold)
  → 低于阈值的手势被丢弃, 返回 GestureType.None
```

**通过代码动态调整**：

```csharp
// 方式一：直接在分类器上设置
GestureService.Instance.Bridge; // 间接访问...

// 方式二：分类器的 SetConfidenceThreshold (需要获取分类器引用)
// 目前分类器是 GestureService 内部的 private 字段
// 如需运行时调整，建议通过修改 GestureConfig 资产实现
```

---

### 7.5 调整手部跟踪平滑度

`HandTracker` 构造时接受 `smoothingFactor` 参数（当前在 `GestureService.cs` 中硬编码为 0.5）。

如需调整：

1. 打开 `Assets/Scripts/GestureRecognition/Service/GestureService.cs`
2. 找到 `new HandTracker()` 那行
3. 改为 `new HandTracker(0.3f)` （更平滑）或 `new HandTracker(0.8f)`（更灵敏）

| smoothingFactor | 效果 |
|-----------------|------|
| 0.1 ~ 0.3 | 非常平滑，延迟较高 |
| 0.4 ~ 0.6 | 平衡（推荐） |
| 0.7 ~ 1.0 | 非常灵敏，可能抖动 |

---

### 7.6 前端接入手势识别

#### 方式一：事件订阅（推荐）

```csharp
using GestureRecognition.Core;

public class MyGameController : MonoBehaviour
{
    private void OnEnable()
    {
        GestureEvents.OnGestureChanged += OnGestureChanged;
        GestureEvents.OnHandPositionUpdated += OnHandMoved;
    }

    private void OnDisable()
    {
        GestureEvents.OnGestureChanged -= OnGestureChanged;
        GestureEvents.OnHandPositionUpdated -= OnHandMoved;
    }

    private void OnGestureChanged(GestureResult result)
    {
        switch (result.Type)
        {
            case GestureType.Fist:
                Attack();
                break;
            case GestureType.OpenPalm:
                Defend();
                break;
            case GestureType.Shoot:
                FireProjectile();
                break;
        }
    }

    private void OnHandMoved(Vector2 position)
    {
        // position.x, position.y 范围 [0, 1]
        // (0,0) = 画面左下角, (1,1) = 画面右上角
        MoveCharacter(position);
    }
}
```

#### 方式二：每帧轮询

```csharp
using GestureRecognition.Service;

void Update()
{
    if (GestureService.Instance == null || !GestureService.Instance.IsRunning)
        return;

    var result = GestureService.Instance.CurrentResult;
    if (result.IsHandDetected && result.Confidence > 0.7f)
    {
        // 使用 result.Type, result.HandPosition 等
    }
}
```

#### 方式三：一行代码管理面板

```csharp
using GestureRecognition.Service;

// 显示面板（自动创建 + 自动启动识别）
GesturePanelManager.Instance.ShowPanel();

// 隐藏面板（自动停止识别）
GesturePanelManager.Instance.HidePanel();

// 切换
GesturePanelManager.Instance.TogglePanel();

// 设置显示模式
GesturePanelManager.Instance.SetDisplayMode(GestureDisplayPanel.DisplayMode.CartoonSprite);

// 调整大小
GesturePanelManager.Instance.SetPanelSize(400, 300);
```

#### 前端无需关心的事情

- 摄像头的打开/关闭（`GestureService` 自动管理）
- MediaPipe 的初始化/销毁（`GestureService` 自动管理）
- Canvas/EventSystem 的创建（`GesturePanelManager` 自动处理）
- 内存回收（组件销毁时自动清理）

---

### 7.7 运行测试场景

1. 打开 `Assets/Scenes/GestureTestScene.unity`
2. 确认场景中有 `GestureManager` 对象，上面挂载了 `GestureService`（`_autoStart` 已勾选）
3. 点击 **Play**
4. 系统会自动启动摄像头，进行实时手势识别
5. 手势面板会显示识别结果（手势类型、置信度）

**无摄像头测试**：可以在代码中调用 `GestureService.Instance.Bridge.InjectMockData()` 注入模拟数据进行测试。

---

### 7.8 切换面板显示模式

**在代码中**：

```csharp
// 通过 GesturePanelManager
GesturePanelManager.Instance.SetDisplayMode(
    GestureDisplayPanel.DisplayMode.CartoonSprite
);

// 或直接操作面板
var panel = FindObjectOfType<GestureDisplayPanel>();
panel.CurrentMode = GestureDisplayPanel.DisplayMode.CameraWithOverlay;
```

**三种模式的 UI 变化**：

| 模式 | CameraImage | GestureImage | 说明 |
|------|-------------|--------------|------|
| `CameraFeed` | 显示（全尺寸） | 隐藏 | 实时摄像头画面 |
| `CartoonSprite` | 隐藏 | 显示（全尺寸） | 隐私模式，只显示手势图标 |
| `CameraWithOverlay` | 显示（全尺寸） | 显示（右下角缩小） | 摄像头 + 手势图标叠加 |

---

### 7.9 生成占位符精灵图

如果你还没有正式的手势精灵图，可以用编辑器工具生成占位符：

1. 在菜单栏点击 **Tools > Gesture Recognition > Generate Placeholder Sprites**
2. 等待控制台输出完成信息
3. 生成的文件在 `Assets/Resources/GestureSprites/` 下：
   - `Gesture_None.png` — 灰色
   - `Gesture_Push.png` — 蓝色
   - `Gesture_Lift.png` — 绿色
   - `Gesture_Shoot.png` — 红色
   - `Gesture_Fist.png` — 橙色
   - `Gesture_OpenPalm.png` — 紫色

---

## 8. 测试说明

### 8.1 测试概览

| 测试文件 | 位置 | 测试数 | 类型 |
|----------|------|--------|------|
| `GestureClassifierTests.cs` | `Assets/Tests/EditMode/` | 24 | 编辑模式（无需 Play） |
| `GestureIntegrationTests.cs` | `Assets/Tests/PlayMode/` | 15 | 运行模式（模拟 Play） |

**总计：39 个测试**

### 8.2 EditMode 测试详情 (24 个)

**GestureClassifierTests (16 个)**:

| 测试 | 验证内容 |
|------|----------|
| `Classify_NullLandmarks_ReturnsNone` | 空输入返回 None |
| `Classify_EmptyLandmarks_ReturnsNone` | 空数组返回 None |
| `Classify_TooFewLandmarks_ReturnsNone` | 不足 21 点返回 None |
| `Classify_FistPose_ReturnsFist` | 合成握拳数据正确识别 |
| `ClassifyFist_WithFistPose_ReturnsHighConfidence` | 握拳置信度 > 0.7 |
| `Classify_OpenPalmPose_ReturnsOpenPalm` | 合成张掌数据正确识别 |
| `ClassifyOpenPalm_WithOpenPalmPose_ReturnsHighConfidence` | 张掌置信度 > 0.7 |
| `Classify_ShootPose_ReturnsShoot` | 合成手枪手势正确识别 |
| `ClassifyShoot_WithShootPose_ReturnsHighConfidence` | 手枪手势置信度 > 0.6 |
| `ClassifyLift_WithLiftPose_ReturnsHighConfidence` | 举手置信度 > 0.5 |
| `Classify_HighThreshold_ReturnsNoneForLowConfidence` | 高阈值拒绝低置信度结果 |
| `SetConfidenceThreshold_UpdatesThreshold` | 运行时调整阈值生效 |
| `RegisterClassifier_CustomGesture_CanBeDetected` | 自定义分类器注册后可检测 |
| `IsFingerCurled_TipCloserToWrist_ReturnsTrue` | 指尖靠近手腕 = 弯曲 |
| `IsFingerCurled_TipFartherFromWrist_ReturnsFalse` | 指尖远离手腕 = 不弯曲 |
| `IsFingerExtended_IsOppositeOfCurled` | 伸展与弯曲互斥 |

**HandTrackerTests (4 个)**:

| 测试 | 验证内容 |
|------|----------|
| `Update_ValidData_ReturnsPosition` | 有效数据产生正确位置 |
| `Update_InvalidData_DoesNotUpdatePosition` | 无效数据不更新位置 |
| `Reset_ClearsState` | Reset 清除状态 |
| `ComputePalmCenter_ReturnsAverageOfFivePoints` | 手掌中心 = 5 点平均 |

**GestureResultTests (4 个)**:

| 测试 | 验证内容 |
|------|----------|
| `Constructor_ClampsConfidence` | 置信度 > 1 被截断为 1 |
| `Constructor_ClampsNegativeConfidence` | 置信度 < 0 被截断为 0 |
| `Empty_ReturnsNoneType` | Empty 返回 None 类型 |
| `ToString_ContainsType` | ToString 包含手势类型名 |

### 8.3 PlayMode 测试详情 (15 个)

**GestureIntegrationTests**:

| 测试 | 验证内容 |
|------|----------|
| `Service_StartsNotRunning` | 服务初始不运行 |
| `Service_HasBridge` | 服务有 Bridge 组件 |
| `Service_HasCamera` | 服务有 Camera 组件 |
| `Service_CurrentResultIsEmpty` | 初始结果为空 |
| `Bridge_InjectMockData_FiresEvent` | 注入模拟数据触发事件 |
| `GestureEvents_OnGestureUpdated_CanSubscribe` | 可订阅更新事件 |
| `GestureEvents_OnGestureChanged_CanSubscribe` | 可订阅变化事件 |
| `GestureEvents_OnHandPositionUpdated_CanSubscribe` | 可订阅位置事件 |
| `GestureEvents_OnHandDetectionChanged_CanSubscribe` | 可订阅检测状态事件 |
| `GestureEvents_OnRecognitionStateChanged_CanSubscribe` | 可订阅识别状态事件 |
| `GestureEvents_ClearAll_RemovesSubscribers` | ClearAll 清除所有订阅 |
| `Bridge_Initialize_SetsIsInitialized` | Initialize 设置已初始化标志 |
| `Bridge_Shutdown_ClearsIsInitialized` | Shutdown 清除已初始化标志 |
| `Bridge_DoubleInitialize_LogsWarning` | 重复初始化输出警告 |
| `Bridge_InjectMock_ThenClassify_ProducesCorrectGesture` | 端到端：注入→分类→正确输出 |

### 8.4 在 Unity Editor 中运行测试

1. 打开 Unity Editor
2. 菜单栏 → **Window > General > Test Runner**
3. 在 Test Runner 窗口中：
   - 点击 **EditMode** 标签页运行编辑模式测试
   - 点击 **PlayMode** 标签页运行运行模式测试
4. 点击 **Run All** 运行所有测试
5. 绿色 ✓ = 通过，红色 ✗ = 失败

### 8.5 命令行运行测试

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

# 单个测试
"E:/Application/Develop/Software/Tuanjie_Hub/Hub/Editor/2022.3.62f3c1/Editor/Unity.exe" ^
  -runTests -batchmode -nographics ^
  -projectPath "E:/Application/Develop/Software/Tuanjie_Hub/Project/COMP3329_TEST" ^
  -testResults TestResults.xml ^
  -testPlatform EditMode ^
  -testFilter "GestureClassifierTests.Classify_FistPose_ReturnsFist"
```

> **注意**：命令行测试不能在 Unity Editor 已打开的情况下运行。需要先关闭 Editor。

---

## 9. 构建部署

### 9.1 构建前准备

1. **复制 MediaPipe 模型文件**：
   - 在 `Assets/` 下创建 `StreamingAssets/` 文件夹（如不存在）
   - 将 `hand_landmarker.bytes`（或 `.task`）复制到 `Assets/StreamingAssets/`
   - 详见 `MEDIAPIPE_SETUP.md`

2. **确认场景列表**：
   - 菜单 **File > Build Settings**
   - 确保你的游戏场景在 **Scenes in Build** 列表中
   - 拖拽场景文件到列表中添加

3. **确认平台**：
   - 在 Build Settings 中确认 **Target Platform** 为 Windows（或 Mac）

### 9.2 构建命令

```bash
"E:/Application/Develop/Software/Tuanjie_Hub/Hub/Editor/2022.3.62f3c1/Editor/Unity.exe" ^
  -batchmode -nographics -quit ^
  -projectPath "E:/Application/Develop/Software/Tuanjie_Hub/Project/COMP3329_TEST" ^
  -buildWindows64Player "Builds/Game.exe"
```

### 9.3 构建输出

构建完成后，`Builds/` 文件夹包含：
- `Game.exe` — 可执行文件
- `Game_Data/` — 游戏数据
- `Game_Data/StreamingAssets/` — 包含 MediaPipe 模型文件
- 其他 DLL 和配置文件

将整个 `Builds/` 文件夹打包即可分发。

---

## 10. 团队协作与 Git

### 10.1 推荐的 .gitignore

```gitignore
# Unity 生成的缓存
Library/
Temp/
Obj/
Build/
Builds/
Logs/
UserSettings/

# IDE
.vs/
.vscode/
*.csproj
*.sln

# OS
.DS_Store
Thumbs.db

# 构建产物
*.apk
*.unitypackage
```

### 10.2 必须提交的文件

```
Assets/          # 所有资产和代码（包括 .meta 文件！）
Packages/        # manifest.json 和 packages-lock.json
ProjectSettings/ # 项目配置
AGENTS.md        # AI 助手指令
MEDIAPIPE_SETUP.md
README.md
```

### 10.3 团队工作流

**前端团队**只需要关注：

| 文件/资产 | 用途 |
|-----------|------|
| `GestureEvents` | 订阅手势事件 |
| `GestureService.Instance` | 访问当前识别状态 |
| `GesturePanelManager.Instance` | 一行代码管理面板 |
| `GestureConfig.asset` | 在 Inspector 中配置手势精灵图 |

**算法团队**（你）关注：

| 文件 | 用途 |
|------|------|
| `GestureClassifier.cs` | 调优分类算法 |
| `HandTracker.cs` | 调优跟踪平滑 |
| `MediaPipeBridge.cs` | MediaPipe 集成 |

### 10.4 场景合并冲突

Unity 的 `.unity` 场景文件是 YAML 格式，但合并困难。建议：

- **每个团队成员在不同的场景中工作**
- 如果必须编辑同一场景，事先沟通，避免同时修改
- 使用 Unity 的 **Smart Merge**（在 ProjectSettings 中配置 YAML merge tool）

---

## 11. Unity 初学者速查

### 11.1 核心概念

| 概念 | 说明 |
|------|------|
| **GameObject** | 场景中的一切东西都是 GameObject（空物体、摄像头、精灵、UI 等） |
| **Component** | 挂载到 GameObject 上的功能模块。多个 Component 可以共存于一个 GameObject |
| **MonoBehaviour** | C# 脚本的基类。继承它才能挂载到 GameObject 上 |
| **ScriptableObject** | 数据容器，不挂载到 GameObject，以 `.asset` 文件存在 |
| **Inspector** | 显示选中对象的所有 Component 和其可编辑字段 |
| **Prefab** | 可复用的 GameObject 模板（类似"蓝图"） |
| **Scene** | 场景文件，包含一组 GameObject 的快照 |
| **Assembly Definition (.asmdef)** | 定义代码编译单元，控制依赖关系 |

### 11.2 常用快捷键

| 快捷键 | 功能 |
|--------|------|
| `Ctrl+P` | Play / 停止游戏 |
| `Ctrl+S` | 保存场景 |
| `Ctrl+Z` | 撤销 |
| `Ctrl+Shift+Z` | 重做 |
| `F` | 聚焦选中对象 |
| `Ctrl+D` | 复制对象 |
| `Delete` | 删除对象 |

### 11.3 常用窗口

| 窗口 | 打开方式 | 用途 |
|------|----------|------|
| Scene | 默认打开 | 可视化编辑场景 |
| Game | 默认打开 | 预览游戏运行效果 |
| Inspector | 默认打开 | 查看/编辑选中对象的属性 |
| Project | 默认打开 | 浏览项目文件（类似文件管理器） |
| Hierarchy | 默认打开 | 查看场景中所有 GameObject 的树形结构 |
| Console | Window > General > Console | 查看 Debug.Log 输出和错误信息 |
| Test Runner | Window > General > Test Runner | 运行单元测试 |

### 11.4 如何挂载脚本到 GameObject

1. 在 Hierarchy 中选中目标 GameObject
2. 在 Inspector 底部点击 **Add Component**
3. 搜索脚本名（如 "GestureService"）
4. 点击添加

**或者**：从 Project 窗口直接拖拽 `.cs` 文件到 Inspector 中。

### 11.5 `[SerializeField]` 的意义

```csharp
[SerializeField] private float _moveSpeed = 5f;
```

这行代码的意思是：
- `private` — 其他脚本不能直接访问这个变量
- `[SerializeField]` — 但 Unity Inspector 可以看到并编辑它
- `= 5f` — 默认值是 5（Inspector 中修改后会覆盖此值）

这是 Unity 的最佳实践：**字段保持 private，但通过 `[SerializeField]` 暴露给 Inspector**。

### 11.6 生命周期方法执行顺序

```
Awake()         ← 对象创建时调用（在 Start 之前）
  ↓
OnEnable()      ← 对象启用时调用
  ↓
Start()         ← 第一帧之前调用（只调用一次）
  ↓
┌─ Update()     ← 每帧调用（游戏逻辑）
│  FixedUpdate() ← 固定时间间隔调用（物理计算）
│  LateUpdate()  ← Update 之后调用（跟随摄像头等）
└─ 循环 ────────
  ↓
OnDisable()     ← 对象禁用时调用
  ↓
OnDestroy()     ← 对象销毁时调用
```

---

## 12. 常见问题 (FAQ)

### Q: 为什么摄像头画面是黑的？

**A**: 可能原因：
1. 没有摄像头设备——检查系统设置中是否有可用摄像头
2. 摄像头被其他程序占用——关闭其他使用摄像头的应用
3. 在编辑器中没有摄像头权限——Windows 通常自动授权，Mac 需要在系统偏好中授权
4. WebCamTexture 还没准备好——`CameraManager` 有等待机制，但如果超时则显示黑色

### Q: 手势识别不准确怎么办？

**A**:
1. 调低 `ConfidenceThreshold`（在 GestureConfig Inspector 中）
2. 确保光线充足，手部在画面中清晰可见
3. 检查 `GestureClassifier.cs` 中的分类逻辑是否需要调优
4. 使用 `GestureOverlay` 查看关键点检测是否准确

### Q: 编译报错 CS0104: ambiguous reference 'NormalizedLandmark'

**A**: 这是因为 `NormalizedLandmark` 同时存在于 `Mediapipe` 和 `Mediapipe.Tasks.Components.Containers` 命名空间。解决方案：使用完全限定类型名或 `var` 关键字。该问题已在当前代码中修复。

### Q: 如何在没有摄像头的电脑上测试？

**A**: 可以通过代码使用 `GestureService.Instance.Bridge.InjectMockData()` 注入模拟手势数据进行测试，完全不需要摄像头。详见 [7.7 运行测试场景](#77-运行测试场景)。

### Q: 为什么 `Library/` 文件夹这么大？

**A**: `Library/` 是 Unity 的编译缓存和导入缓存。它会根据项目大小自动增长（通常几百 MB 到几 GB）。**绝对不要提交到 Git**。删除后 Unity 会在下次打开时重新生成（需要较长时间）。

### Q: 测试在命令行运行失败但 Editor 里正常？

**A**: 确保在运行命令行测试之前**完全关闭 Unity Editor**。Unity 项目同一时间只能被一个进程访问。

### Q: 如何理解 `.asmdef` 文件？

**A**: `.asmdef` 是 Assembly Definition 文件。简单来说，它告诉 Unity："把这个文件夹下的所有代码编译成一个独立的 DLL"。好处是：修改一个脚本只需重编译该程序集（而非整个项目），且可以控制代码之间的可见性。详见 [5.7 程序集定义与配置文件](#57-程序集定义与配置文件)。

### Q: MediaPipe 模型文件放在哪里？

**A**: 开发时由 `MediaPipeBridge.InitializeAsync()` 通过 `LocalResourceManager` 从 `Packages/com.github.homuler.mediapipe/` 加载。构建部署时需要手动复制 `hand_landmarker.bytes` 到 `Assets/StreamingAssets/`。详见 `MEDIAPIPE_SETUP.md`。

---

## 附录：项目统计

| 指标 | 数值 |
|------|------|
| C# 源文件 | 13 个 |
| 总代码行数 | ~3,100 行 |
| 程序集定义 | 4 个（运行时、编辑器、EditMode 测试、PlayMode 测试） |
| 单元/集成测试 | 39 个（24 EditMode + 15 PlayMode） |
| 支持手势类型 | 5 种 + None |
| 精灵图文件 | 10 个（4 用户制作 + 6 占位符） |
| 场景文件 | 2 个 |
| 文档文件 | 5 个（README.md, AGENTS.md, MEDIAPIPE_SETUP.md, FRONTEND_GUIDE.md, BACKEND_GUIDE.md） |

---

*最后更新：2026 年 3 月*
