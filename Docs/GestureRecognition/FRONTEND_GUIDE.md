# 前端接入指南 (Frontend Integration Guide)

> 本文档面向**前端/游戏逻辑开发人员**，说明如何在你的游戏代码中接入手势识别模块。

---

## 0. 环境准备：安装 MediaPipe Unity Plugin

**在使用手势识别模块之前，团队每个人的项目中都必须安装 MediaPipe 插件。**

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

> **注意**：`.tgz` 文件不需要放在项目文件夹内。Unity 会将其内容缓存到 `Library/PackageCache/` 中。但建议团队共享时把 `.tgz` 文件放到一个公共位置，或每个人各自下载。

---

## 1. 文件归属说明

手势识别模块的所有代码都在以下文件夹中，与你的游戏代码分离：

```
Assets/
├── Scripts/
│   └── GestureRecognition/    ← 手势识别模块的全部代码（不要修改）
│       ├── Core/              ← 数据类型、事件、配置
│       ├── Detection/         ← 检测层（摄像头、MediaPipe、分类）
│       ├── Service/           ← 对外 API（你主要用这里的东西）
│       ├── UI/                ← 显示面板
│       └── Editor/            ← 编辑器工具（你不需要用）
│
├── Tests/                     ← 自动化测试（你不需要用，可以忽略）
│
├── Scenes/
│   ├── GestureTestScene.unity ← 测试场景（你不需要用）
│   └── SampleScene.unity      ← 默认场景 / 你的游戏场景
│
└── Resources/
    ├── GestureConfig.asset    ← 手势配置（可能需要编辑）
    └── GestureSprites/        ← 手势精灵图
```

**你的游戏代码**应放在 `Assets/Scripts/` 下的其他文件夹中（如 `Assets/Scripts/Game/`），不要和 `GestureRecognition/` 混在一起。

---

## 2. 你需要做的事（快速清单）

| 步骤 | 操作 | 是否必须 |
|------|------|----------|
| 1 | 在你的游戏场景中创建一个空 GameObject，挂载 `GestureService` 组件 | **必须** |
| 2 | 在你的游戏脚本中订阅 `GestureEvents` 事件 | **必须** |
| 3 | 调用 `FindObjectOfType<GestureDisplayPanel>().Show()` 显示手势面板 | 可选 |
| 4 | 在 `GestureConfig.asset` 中编辑手势精灵图和阈值 | 可选 |

### 步骤 1：添加 GestureService 到场景

1. 在 Hierarchy 面板中右键 → **Create Empty**（创建空对象）
2. 选中新建的 GameObject，在 Inspector 中将其改名为 `GestureManager`（或任意名字）
3. 点击 Inspector 底部的 **Add Component**
4. 搜索 `GestureService`，点击添加

> GestureService 是单例——整个游戏中只需要一个。它会自动管理摄像头、MediaPipe 和整个识别流水线。

### 步骤 2：在你的代码中接入

见下方 [Case 1 ~ Case 7](#3-接入-case)。

---

## 3. 接入 Case

### Case 1：根据手势执行不同动作

**场景**：玩家握拳攻击、张掌防御、手枪手势射击。

```csharp
using UnityEngine;
using GestureRecognition.Core;

public class PlayerCombat : MonoBehaviour
{
    private void OnEnable()
    {
        // 订阅"手势变化"事件（只在手势类型改变时触发）
        GestureEvents.OnGestureChanged += HandleGestureChanged;
    }

    private void OnDisable()
    {
        // 退订（非常重要！否则对象销毁后会报空引用错误）
        GestureEvents.OnGestureChanged -= HandleGestureChanged;
    }

    private void HandleGestureChanged(GestureResult result)
    {
        switch (result.Type)
        {
            case GestureType.Fist:
                Debug.Log("攻击！");
                Attack();
                break;
            case GestureType.OpenPalm:
                Debug.Log("防御！");
                Defend();
                break;
            case GestureType.Shoot:
                Debug.Log("射击！");
                FireProjectile();
                break;
            case GestureType.None:
                Debug.Log("手势消失，恢复待机");
                Idle();
                break;
        }
    }

    private void Attack() { /* 你的攻击逻辑 */ }
    private void Defend() { /* 你的防御逻辑 */ }
    private void FireProjectile() { /* 你的射击逻辑 */ }
    private void Idle() { /* 你的待机逻辑 */ }
}
```

**要点**：
- `OnGestureChanged` 只在手势**变化**时触发（从 Fist 变成 OpenPalm），不会每帧重复触发
- 始终在 `OnDisable()` 中退订，防止内存泄漏

---

### Case 2：用手的位置控制角色移动

**场景**：玩家手在画面左侧 → 角色向左移动；手在右侧 → 向右移动。

```csharp
using UnityEngine;
using GestureRecognition.Core;

public class HandMovementController : MonoBehaviour
{
    [SerializeField] private float _moveSpeed = 5f;
    [SerializeField] private Transform _character;

    private Vector2 _handPosition;
    private bool _handDetected;

    private void OnEnable()
    {
        GestureEvents.OnHandPositionUpdated += OnHandMoved;
        GestureEvents.OnHandDetectionChanged += OnHandDetection;
    }

    private void OnDisable()
    {
        GestureEvents.OnHandPositionUpdated -= OnHandMoved;
        GestureEvents.OnHandDetectionChanged -= OnHandDetection;
    }

    private void OnHandMoved(Vector2 position)
    {
        // position 范围 [0, 1]
        // x: 0 = 画面左边, 1 = 画面右边
        // y: 0 = 画面下方, 1 = 画面上方
        _handPosition = position;
    }

    private void OnHandDetection(bool detected)
    {
        _handDetected = detected;
    }

    private void Update()
    {
        if (!_handDetected || _character == null) return;

        // 将 [0,1] 映射到 [-1, 1]
        float horizontal = (_handPosition.x - 0.5f) * 2f;
        float vertical = (_handPosition.y - 0.5f) * 2f;

        // 移动角色
        Vector3 movement = new Vector3(horizontal, vertical, 0f);
        _character.position += movement * _moveSpeed * Time.deltaTime;
    }
}
```

**要点**：
- `OnHandPositionUpdated` **每帧**触发（只要检测到手），比 `OnGestureChanged` 更频繁
- 坐标是归一化的 [0, 1]，需要自己映射到游戏世界坐标

---

### Case 3：用轮询方式（不用事件）

**场景**：你更喜欢在 Update 里直接读取数据，而不是用事件回调。

```csharp
using UnityEngine;
using GestureRecognition.Core;
using GestureRecognition.Service;

public class GesturePoller : MonoBehaviour
{
    private void Update()
    {
        // 安全检查：服务是否存在且在运行
        if (GestureService.Instance == null) return;
        if (!GestureService.Instance.IsRunning) return;

        // 直接读取当前结果
        GestureResult result = GestureService.Instance.CurrentResult;

        if (result.IsHandDetected)
        {
            Debug.Log($"手势: {result.Type}, 置信度: {result.Confidence:P0}, 位置: {result.HandPosition}");
        }
    }
}
```

**要点**：
- 轮询方式简单直接，但你会在手势**没有变化**的帧也读到同样的数据
- 适合需要持续读取状态的场景（如"只要保持握拳就持续充能"）

---

### Case 4：显示/隐藏手势面板

**场景**：玩家按下 UI 按钮时显示手势面板，再按一次隐藏。

```csharp
using UnityEngine;
using GestureRecognition.UI;

public class GesturePanelToggle : MonoBehaviour
{
    // 缓存面板引用（Inspector 拖拽 或 FindObjectOfType）
    [SerializeField] private GestureDisplayPanel _panel;

    void Awake()
    {
        if (_panel == null)
            _panel = FindObjectOfType<GestureDisplayPanel>(true);
    }

    // 这个方法绑定到 UI Button 的 OnClick 事件
    public void OnToggleButtonClicked()
    {
        _panel.Toggle();
    }

    // 或者分开控制
    public void OnShowButtonClicked()
    {
        _panel.Show();
    }

    public void OnHideButtonClicked()
    {
        _panel.Hide();
    }

    // 设置面板大小
    public void SetSmallPanel()
    {
        _panel.SetSize(200, 150);
    }

    public void SetLargePanel()
    {
        _panel.SetSize(400, 300);
    }

    // 切换显示模式
    public void SetSpriteMode()
    {
        // 只显示卡通精灵图（隐私模式，不显示摄像头画面）
        _panel.CurrentMode = GestureDisplayPanel.DisplayMode.CartoonSprite;
    }

    public void SetCameraMode()
    {
        // 显示实时摄像头画面
        _panel.CurrentMode = GestureDisplayPanel.DisplayMode.CameraFeed;
    }

    public void SetOverlayMode()
    {
        // 摄像头画面 + 右下角小精灵图
        _panel.CurrentMode = GestureDisplayPanel.DisplayMode.CameraWithOverlay;
    }
}
```

**要点**：
- `Show()` / `Hide()` 是纯 UI 操作，不会影响 GestureService 的运行状态
- 面板可拖拽、可缩放——用户可以自由移动和调整大小

---

### Case 5：手动控制识别的启动和停止

**场景**：你不想用面板，只想在后台运行识别并获取数据。

```csharp
using UnityEngine;
using GestureRecognition.Core;
using GestureRecognition.Service;

public class BackgroundRecognition : MonoBehaviour
{
    private void Start()
    {
        // 订阅事件
        GestureEvents.OnGestureChanged += OnGesture;

        // 手动启动识别（不显示任何面板）
        GestureService.Instance.StartRecognition();
    }

    private void OnDestroy()
    {
        GestureEvents.OnGestureChanged -= OnGesture;

        // 停止识别
        if (GestureService.Instance != null)
        {
            GestureService.Instance.StopRecognition();
        }
    }

    private void OnGesture(GestureResult result)
    {
        Debug.Log($"后台识别到: {result.Type}");
    }
}
```

**要点**：
- `StartRecognition()` / `StopRecognition()` 可以独立于面板使用
- 即使没有面板，事件照样触发

---

### Case 6：检查识别系统状态

**场景**：在 UI 上显示"摄像头已连接"/"识别中"等状态提示。

```csharp
using UnityEngine;
using UnityEngine.UI;
using GestureRecognition.Core;
using GestureRecognition.Service;

public class StatusDisplay : MonoBehaviour
{
    [SerializeField] private Text _statusText;

    private void OnEnable()
    {
        GestureEvents.OnRecognitionStateChanged += OnStateChanged;
        GestureEvents.OnHandDetectionChanged += OnHandChanged;
    }

    private void OnDisable()
    {
        GestureEvents.OnRecognitionStateChanged -= OnStateChanged;
        GestureEvents.OnHandDetectionChanged -= OnHandChanged;
    }

    private void OnStateChanged(bool isRunning)
    {
        _statusText.text = isRunning ? "识别中..." : "识别已停止";
    }

    private void OnHandChanged(bool detected)
    {
        _statusText.text = detected ? "检测到手！" : "未检测到手";
    }
}
```

---

### Case 7：同时使用手势类型和手部位置

**场景**：握拳 + 手在屏幕上方 = 上勾拳；握拳 + 手在下方 = 下击。

```csharp
using UnityEngine;
using GestureRecognition.Core;
using GestureRecognition.Service;

public class CombinedGestureController : MonoBehaviour
{
    private void Update()
    {
        if (GestureService.Instance == null || !GestureService.Instance.IsRunning)
            return;

        GestureResult result = GestureService.Instance.CurrentResult;

        if (!result.IsHandDetected) return;

        if (result.Type == GestureType.Fist)
        {
            if (result.HandPosition.y > 0.6f)
            {
                // 手在画面上方 → 上勾拳
                UppercutAttack();
            }
            else if (result.HandPosition.y < 0.4f)
            {
                // 手在画面下方 → 下击
                LowAttack();
            }
            else
            {
                // 手在中间 → 普通攻击
                NormalAttack();
            }
        }
    }

    private void UppercutAttack() { Debug.Log("上勾拳！"); }
    private void LowAttack() { Debug.Log("下击！"); }
    private void NormalAttack() { Debug.Log("普通攻击！"); }
}
```

---

## 4. 完整的 API 参考

### GestureEvents（事件）

```csharp
using GestureRecognition.Core;

// 订阅
GestureEvents.OnGestureChanged += (GestureResult result) => { };      // 手势变化
GestureEvents.OnGestureUpdated += (GestureResult result) => { };      // 每帧更新
GestureEvents.OnHandPositionUpdated += (Vector2 pos) => { };          // 手位置
GestureEvents.OnHandDetectionChanged += (bool detected) => { };       // 手出现/消失
GestureEvents.OnRecognitionStateChanged += (bool running) => { };     // 系统启停
```

### GestureResult（数据结构）

```csharp
GestureResult result = GestureService.Instance.CurrentResult;

result.Type             // GestureType 枚举 (None, Push, Lift, Shoot, Fist, OpenPalm)
result.Confidence       // float [0, 1] 置信度
result.HandPosition     // Vector2 [0,1] 手的位置；(-1,-1) 表示无手
result.IsHandDetected   // bool 是否检测到手
result.Timestamp        // float 时间戳
```

### GestureService（核心服务）

```csharp
using GestureRecognition.Service;

GestureService.Instance.IsRunning                 // 是否在运行
GestureService.Instance.CurrentResult             // 当前识别结果
GestureService.Instance.StartRecognition()        // 启动识别
GestureService.Instance.StopRecognition()         // 停止识别
GestureService.Instance.GetAvailableCameras()     // 获取摄像头列表
GestureService.Instance.SwitchCamera("设备名")    // 切换摄像头
```

### GestureDisplayPanel（面板控制）

```csharp
using GestureRecognition.UI;

// 获取面板引用（面板存在于 DontDestroyOnLoad 中）
GestureDisplayPanel panel = FindObjectOfType<GestureDisplayPanel>(true);

panel.Show()                                           // 显示面板（纯 UI）
panel.Hide()                                           // 隐藏面板（纯 UI）
panel.Toggle()                                         // 切换面板
panel.SetSize(400, 300)                                // 设置大小
panel.CurrentMode = GestureDisplayPanel.DisplayMode.CartoonSprite  // 设置显示模式
```

### GestureType（手势枚举）

```csharp
GestureType.None       // 无手势
GestureType.Push       // 推掌
GestureType.Lift       // 举手
GestureType.Shoot      // 手枪手势
GestureType.Fist       // 握拳
GestureType.OpenPalm   // 张掌
```

---

## 5. 注意事项

### 必须在 OnDisable 中退订事件

```csharp
// 错误写法 —— 内存泄漏！
void Start()
{
    GestureEvents.OnGestureChanged += HandleGesture;
    // 如果对象被销毁但没退订，事件会试图调用已销毁对象的方法 → 报错
}

// 正确写法
void OnEnable()
{
    GestureEvents.OnGestureChanged += HandleGesture;
}
void OnDisable()
{
    GestureEvents.OnGestureChanged -= HandleGesture;
}
```

### 场景中只需要一个 GestureService

`GestureService` 是单例。如果场景中有多个，多余的会在 `Awake()` 中自动销毁自己。

### 手势面板完全可选

你可以只用事件获取手势数据，不显示任何面板。面板只是为了调试和展示用途。

### 识别延迟

MediaPipe CPU 模式下大约 30-60ms 延迟（15-30 FPS）。对于实时格斗游戏，建议：
- 使用 `OnGestureChanged`（变化触发）而非 `OnGestureUpdated`（每帧触发）来触发动作
- 用 `OnHandPositionUpdated` 做平滑移动

---

## 6. 常见问题

### Q: 我的游戏场景里需要放什么？

A: 最少只需一个 GameObject 上挂 `GestureService` 组件。

### Q: 如何在不显示面板的情况下使用手势数据？

A: 直接调用 `GestureService.Instance.StartRecognition()` 并订阅 `GestureEvents`。不调用 `ShowPanel()` 就不会有面板。

### Q: GestureConfig 需要我修改吗？

A: 通常不需要。如果你想换精灵图或调整置信度阈值，在 Inspector 中编辑 `Assets/Resources/GestureConfig.asset` 即可。

### Q: 手势识别模块的代码我能改吗？

A: 尽量不要。如果需要新功能，联系后端（算法）同学。`GestureRecognition/` 文件夹里的代码由后端维护。

### Q: 我的代码要放在哪里？

A: 放在 `Assets/Scripts/` 下的你自己的文件夹中（如 `Assets/Scripts/Game/`、`Assets/Scripts/UI/` 等），不要和 `GestureRecognition/` 混在一起。
