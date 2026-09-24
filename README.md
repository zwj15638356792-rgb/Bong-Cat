# 大肥鱼桌宠 · 关节动作版

使用角色原图和 [ayangweb/BongoCat](https://github.com/ayangweb/BongoCat) 的鼠标素材制作的 Windows 桌宠，搭配原生绘制的紧凑键盘。鼠标与键盘操作会驱动肩、肘、腕关节，窗口背景透明。

![桌宠预览](docs/preview.png)

## 运行

下载或自行构建程序后，打开 `Dafeiyu.exe`，并保留同目录的 `assets` 文件夹。

- 移动鼠标、点击左右键和输入键盘，桌宠同步响应。
- 拖动角色移动窗口，右键可放大、缩小和退出。
- 紧凑键盘采用浅蓝圆角底座、圆形及胶囊形键帽，按角色操作方向呈现透视。显示 Q/W/E/R、A/S/D、Tab、Shift、Ctrl、Space、Enter 共 12 个键，按下时呈淡金色；左右 Ctrl、Shift 分别共用键帽。
- 保留 55 种既有输入映射；其他键也会触发通用打字动作，不高亮无关键帽。组合键可同时高亮，手臂跟随最后按下且未松开的键。
- 打字时会随机出现开心、眨眼或惊讶表情，短暂显示后恢复自然表情；冷却间隔避免连续闪换。
- 角色、鼠标及鼠标垫使用较高分辨率素材渲染，键盘与文字直接绘制，放大时保持清晰。
- 鼠标垫沿用原素材轮廓，配色调整为浅蓝灰；键盘移近角色，键盘侧手臂缩短，减少伸手过远。
- 静止时跳过内容未变化的画面，减少待机绘制开销。
- 通过只读 Windows Raw Input 接收键盘和鼠标按钮事件；鼠标位置只读取，不模拟按键或移动系统指针。输入仅用于本地动画，不记录文本、不上传数据。

## 从源码构建

需要 Windows 10/11、Python 3.10+、Node.js 20+。C# 编译使用 Windows 自带的 .NET Framework 4.x 编译器；此版本不需要 Rust、MSVC 或安装前端依赖。

在仓库根目录执行：

```powershell
python -m pip install -r requirements.txt
powershell -ExecutionPolicy Bypass -File native-pet/build.ps1
```

程序输出到 `output/Dafeiyu-Jointed/`，动作检查输出到 `output/jointed-checks/`。构建会从源码生成素材和模型几何数据，无需保留本地缓存。重建前请退出正在运行的同目录程序。

也可以指定独立输出目录：

```powershell
powershell -ExecutionPolicy Bypass -File native-pet/build.ps1 -OutputDirectory output/my-build
```

构建包含 Raw Input 数据包离线解析、55 种既有输入映射、通用按键反馈、12 个可见键帽及其别名、3025 组目标转换、鼠标移动范围和表情时序的检查。这些离线检查不会注册输入设备或生成真实输入。GitHub Actions 的 Windows 构建会执行相同流程，并提供可下载的构建产物。

## 项目结构

| 路径 | 用途 |
| --- | --- |
| `native-pet/` | 当前桌宠的 C# 源码、构建脚本及使用说明 |
| `scripts/` | 素材提取、模型几何导出，以及保留的上游构建脚本 |
| `artwork/source-poses/` | 构建所需的角色原图 |
| `src-tauri/assets/models/standard/` | 提取鼠标与鼠标垫所需的上游素材及模型 |
| `src/`、`src-tauri/`、`public/` | 保留的 BongoCat 上游实现及其资源 |
| `docs/` | 项目展示图 |

当前可运行的关节动作版是 `native-pet/` 中的独立程序，不是可导入 BongoCat 的 Live2D 模型。上游 Vue/Tauri 源码仍可单独开发，参见 [上游说明](README.upstream.md) 和 [贡献指南](.github/CONTRIBUTING.md)。它仍保留上游的应用标识和更新配置；构建当前关节版请使用上面的 PowerShell 命令。

## 上传与发布

`.gitignore` 已排除依赖、缓存、临时文件、程序产物和本地环境配置。源码提交到仓库，生成的 `Dafeiyu-Jointed` 文件夹打包后用于 Release；不要把 `node_modules`、`output` 或安装器作为源码上传。

当前实现仍为卡通手臂模拟，没有独立手指关节或完整角色骨骼。

## 来源与许可

BongoCat 源码及素材来自 [ayangweb/BongoCat](https://github.com/ayangweb/BongoCat)，具体版本见 [UPSTREAM_REVISION.txt](UPSTREAM_REVISION.txt)。保留原 [MIT 许可证](LICENSE) 及 Live2D 组件版权声明。角色原图由用户提供，其使用与再分发范围由角色权利人授权决定。
