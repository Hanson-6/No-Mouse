# LDtk 关卡导入 Unity 操作指南

本文档记录了将 LDtk（Level Designer Toolkit）关卡文件导入 Unity 项目的完整流程。

## 概述

- **LDtk 版本**：1.5.3
- **Unity 导入器**：[LDtkToUnity](https://github.com/Cammin/LDtkToUnity)（作者 Cammin），版本 6.12.2
- **安装方式**：OpenUPM Scoped Registry
- **LDtk 项目文件**：`Assets/IDTK/Levels.ldtk`
- **关卡**：`Level_0`（1456×456 像素，PPU=16）

LDtkToUnity 是 LDtk 官网唯一列出的 Unity 导入器，通过 Unity 的 ScriptedImporter 机制工作，将 `.ldtk` 文件自动转换为 Tilemap、Entity Prefab、碰撞体等。

---

## 第一步：安装 LDtkToUnity 导入器

通过 OpenUPM Scoped Registry 安装（官方推荐方式）。

### 1.1 添加 Scoped Registry

1. 打开 **Edit → Project Settings → Package Manager**
2. 在 Scoped Registries 区域添加：
   - **Name**: `OpenUPM`
   - **URL**: `https://package.openupm.com`
   - **Scope(s)**: `com.cammin.ldtkunity`
3. 点击 **Save**

### 1.2 安装包

1. 打开 **Window → Package Manager**
2. 左上角下拉选择 **My Registries**
3. 找到 **LDtk to Unity**，点击 **Install**

安装完成后，Unity 会自动识别 `Assets/IDTK/Levels.ldtk` 文件，在 Inspector 中显示 LDtk 项目的导入设置界面。

### 验证

`Packages/manifest.json` 中应包含：

```json
{
  "dependencies": {
    "com.cammin.ldtkunity": "6.12.2"
  },
  "scopedRegistries": [
    {
      "name": "OpenUPM",
      "url": "https://package.openupm.com",
      "scopes": [
        "com.cammin.ldtkunity"
      ]
    }
  ]
}
```

---

## 第二步：安装 LDtk 桌面软件

LDtkToUnity 6.x 架构需要通过 LDtk 软件的 Custom Command 机制运行 `LDtkTilesetExporter`，生成 `.ldtkt` tileset 定义文件。

1. 前往 [https://ldtk.io](https://ldtk.io) 下载并安装 LDtk 桌面软件
2. 安装后确保可以正常打开

> **注意**：`.ldtkt` 文件不包含地图数据（地图数据全在 `.ldtk` JSON 中），它是告诉 Unity 如何切割 tileset 图片的定义文件。

---

## 第三步：配置 Custom Command 并生成 .ldtkt 文件

### 3.1 在 Unity 中触发 Custom Command 配置

1. 在 Unity Project 窗口中选中 `Assets/IDTK/Levels.ldtk`
2. Inspector 中会显示 LDtk 导入设置界面
3. 如果看到警告提示，点击 **Fix → Auto-add command**
4. 这会将 Custom Command 写入 `Levels.ldtk` 文件

写入的 Custom Command 内容为：
```json
"customCommands": [
  {
    "command": "../../Library/LDtkTilesetExporter/ExportTilesetDefinition.exe \"Levels\"",
    "when": "AfterSave"
  }
]
```

### 3.2 在 LDtk 中保存以触发导出

1. 打开 LDtk 桌面软件
2. 用它打开 `Assets/IDTK/Levels.ldtk`
3. 按 **Ctrl+S** 保存项目
4. 第一次运行 Custom Command 时会弹出安全警告，选择 **"I understand the risks, allow commands"**
5. 保存完成后，`Assets/IDTK/Levels/` 目录下会自动生成 `.ldtkt` 文件

### 验证

`Assets/IDTK/Levels/` 目录下应出现以下 `.ldtkt` 文件（对应 LDtk 项目引用的每个 tileset）：

| 文件名 | 对应贴图 |
|---|---|
| `CaveBackgroundTiles.ldtkt` | `Assets/CaveAssets/Tiles/CaveBackgroundTiles.png` |
| `CaveDetailTiles.ldtkt` | `Assets/CaveAssets/Tiles/CaveDetailTiles.png` |
| `CaveDetailTiles2.ldtkt` | `Assets/CaveAssets/Tiles/CaveDetailTiles2.png` |
| `CaveTerrainDetailTiles.ldtkt` | `Assets/CaveAssets/Tiles/CaveTerrainDetailTiles.png` |
| `CaveTerrainDetailTiles2.ldtkt` | `Assets/CaveAssets/Tiles/CaveTerrainDetailTiles2.png` |
| `CaveTerrainTiles2.ldtkt` | `Assets/CaveAssets/Tiles/CaveTerrainTiles.png` |
| `FoliageTiles.ldtkt` | `Assets/CaveAssets/Tiles/FoliageTiles.png` |
| `CaveDetailSprites1.ldtkt` | `Assets/CaveAssets/Spritesheets/Decorations/CaveDetailSprites1.png` |
| `CaveDetailSprites2.ldtkt` | `Assets/CaveAssets/Spritesheets/Decorations/CaveDetailSprites2.png` |

---

## 第四步：修改贴图格式为 RGBA32

LDtkToUnity 要求所有 tileset 贴图使用 **RGBA32** 格式，不能使用默认的 Automatic 压缩。否则会报 `Loading artifacts didn't work` 错误。

### 需要修改的 9 张贴图

**`Assets/CaveAssets/Tiles/` 目录（7 张）：**
- `CaveDetailTiles.png`
- `CaveBackgroundTiles.png`
- `CaveDetailTiles2.png`
- `CaveTerrainDetailTiles.png`
- `CaveTerrainTiles.png`
- `CaveTerrainDetailTiles2.png`
- `FoliageTiles.png`

**`Assets/CaveAssets/Spritesheets/Decorations/` 目录（2 张）：**
- `CaveDetailSprites1.png`
- `CaveDetailSprites2.png`

> **重要**：这两个目录的贴图都必须修改，不要漏掉 `Spritesheets/Decorations/` 目录下的两张。

### 操作步骤

1. 在 Unity 的 **Project 窗口**中，分别导航到上述两个目录
2. **全选所有 9 张贴图**（按住 Ctrl 逐个点击，或在同一目录中 Ctrl+A）
3. 在 **Inspector** 底部找到 **Default** 平台设置
4. 将 **Format** 从 `Automatic` 改为 **`RGBA 32 bit`**
5. 点击 **Apply**

Unity 会重新导入这些贴图，然后自动触发 `.ldtkt` 和 `Levels.ldtk` 的重新导入。

### 验证

- Console 中不再出现 `Loading artifacts didn't work` 错误
- `.ldtkt.meta` 文件大小应为几十到几百 KB（说明 sprite artifacts 已成功生成）
- 如果某个 `.ldtkt.meta` 只有几百字节（如 445 bytes），说明对应的贴图格式尚未修改成功

### 技术细节

在 `.png.meta` 文件中，`DefaultTexturePlatform` 的 `textureFormat` 字段：
- `-1` = Automatic（默认值，不满足要求）
- `4` = RGBA32（需要改为此值）

---

## 第五步：将 LDtk 关卡放入场景

### 方法 A：手动拖入（推荐）

1. 在 **Project 窗口**中找到 `Assets/IDTK/Levels.ldtk`
2. 点击左边的 **展开箭头 (▶)**，可以看到子对象 `Level_0`
3. 将 `Level_0`（或整个 `Levels.ldtk`）从 Project 窗口**拖到 Hierarchy 窗口**中
4. 调整 Main Camera 的位置和 Orthographic Size 使其能看到整个关卡

### 方法 B：使用 Editor 脚本自动创建场景

运行菜单 **Tools → Create LDtk Test Scene**，脚本位于 `Assets/Editor/LDtkSceneSetup.cs`。

该脚本会自动：
- 创建新场景 `Assets/Scenes/LDtkLevel.unity`
- 添加正交相机
- 实例化 `Levels.ldtk` 到场景中
- 计算所有 Renderer 的边界并居中相机

---

## 项目文件结构

```
Assets/IDTK/
├── Levels.ldtk                  # LDtk 项目主文件（v1.5.3）
└── Levels/                      # 自动生成的 .ldtkt tileset 定义文件
    ├── CaveBackgroundTiles.ldtkt
    ├── CaveDetailSprites1.ldtkt
    ├── CaveDetailSprites2.ldtkt
    ├── CaveDetailTiles.ldtkt
    ├── CaveDetailTiles2.ldtkt
    ├── CaveTerrainDetailTiles.ldtkt
    ├── CaveTerrainDetailTiles2.ldtkt
    ├── CaveTerrainTiles2.ldtkt
    └── FoliageTiles.ldtkt

Assets/CaveAssets/
├── Tiles/                       # Tileset 贴图（7 张，格式须为 RGBA32）
│   ├── CaveBackgroundTiles.png
│   ├── CaveDetailTiles.png
│   ├── CaveDetailTiles2.png
│   ├── CaveTerrainDetailTiles.png
│   ├── CaveTerrainDetailTiles2.png
│   ├── CaveTerrainTiles.png
│   └── FoliageTiles.png
└── Spritesheets/Decorations/    # 装饰贴图（2 张，格式须为 RGBA32）
    ├── CaveDetailSprites1.png
    └── CaveDetailSprites2.png

Assets/Scenes/
└── LDtkLevel.unity              # LDtk 关卡测试场景

Assets/Editor/
└── LDtkSceneSetup.cs            # 自动创建 LDtk 场景的 Editor 脚本

Library/LDtkTilesetExporter/     # LDtkToUnity 安装的 tileset 导出工具
└── ExportTilesetDefinition.exe
```

---

## LDtk 关卡信息

| 属性 | 值 |
|---|---|
| 关卡名称 | `Level_0` |
| 关卡尺寸 | 1456×456 像素 |
| 世界坐标 | (0, -136) |
| 网格大小 | 8 像素 |
| PPU | 16 |
| Unity 世界大小 | 约 91×28.5 个单位 |

### 图层结构

| 图层 | 类型 | 说明 |
|---|---|---|
| Entities | Entities | 包含 `PlayerStartPoint` 实体 |
| Ore | AutoLayer | 矿石装饰 |
| Collision | IntGrid | 碰撞层，`Stone` (value=2) |
| Column | Tiles | 柱子装饰 |
| Cairn | Tiles | 石堆装饰 |

---

## 常见问题排查

### 错误：找不到 .ldtkt 文件

**原因**：未在 LDtk 中执行 Custom Command。

**解决**：
1. 确认 `Levels.ldtk` 中包含 `customCommands` 字段
2. 在 LDtk 软件中打开项目并按 Ctrl+S 保存
3. 允许 Custom Command 执行

### 错误：Loading artifacts didn't work

**原因**：对应的 tileset 贴图格式不是 RGBA32。

**解决**：
1. 根据错误信息找到对应的 `.ldtkt` 文件名
2. 查找该 tileset 对应的 `.png` 贴图
3. 在 Inspector 中将贴图 Format 改为 `RGBA 32 bit`
4. 点击 Apply

### .ldtkt.meta 文件异常小（几百字节）

**说明**：该 `.ldtkt` 的 sprite artifacts 未能成功生成。通常是对应贴图格式问题。

**验证**：正常导入的 `.ldtkt.meta` 文件大小应为几十到几百 KB，包含大量 `_sprites` 数据。

### 在 LDtk 中修改关卡后如何更新

1. 在 LDtk 软件中编辑关卡
2. 按 **Ctrl+S** 保存（会自动触发 Custom Command 重新生成 `.ldtkt`）
3. 切回 Unity，Unity 会自动检测文件变化并重新导入

---

## 后续工作（可选）

- **配置 IntGrid Tile**：为 Collision 层的 `Stone` 值创建 `LDtkIntGridTile` 资产，设置 Collider Type 为 Grid，使碰撞层生效
- **配置 Entity Prefab**：在 `Levels.ldtk` Inspector 中为 `PlayerStartPoint` 指定玩家 Prefab，实现自动生成玩家
- **将 LDtk 场景加入 Build Settings**：如需在正式游戏流程中使用 LDtk 关卡
