# MediaPipe 团队同步方案（GitHub）

本项目当前使用 `com.github.homuler.mediapipe-0.16.3.tgz` 的本地 tarball 方式。

## 推荐方案（稳定且可长期使用）

- 在 `Packages/manifest.json` 中使用相对路径：
  - `"com.github.homuler.mediapipe": "file:com.github.homuler.mediapipe-0.16.3.tgz"`
- 约定每位开发者都把同名文件放到项目内：
  - `Packages/com.github.homuler.mediapipe-0.16.3.tgz`
- 该 `.tgz` 文件保持 **不入库**（已在 `.gitignore` 忽略）

这样做的好处：
- 不再依赖每个人电脑上的绝对路径
- `git pull` 后无需修改 `manifest.json`
- 每个人只需一次性放置 tar 文件到固定位置

## 新成员接入步骤

1. 从官方 Release 下载 `com.github.homuler.mediapipe-0.16.3.tgz`
2. 将文件复制到项目路径：`Packages/com.github.homuler.mediapipe-0.16.3.tgz`
3. 打开 Unity，等待 Package Manager 自动解析

也可以直接在项目根目录运行脚本自动下载：

```powershell
./setup-mediapipe.ps1
```

## 可能出现的问题

- 如果文件不存在，Unity 会在 Console 报 package not found，手势系统会退回 stub 模式（无真实识别）
- 如果改成 Git URL 依赖，通常会缺少预编译 native 运行时，导致运行时不可用

## 可选替代（进阶）

如果团队后续想进一步自动化，可搭建私有 UPM registry 托管该包。优点是无需手动复制 `.tgz`；缺点是维护成本更高，不适合课程项目的短周期协作。
