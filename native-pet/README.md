# 大肥鱼 · 关节动作版

安装版运行 `Dafeiyu-Setup-1.1.0.exe`，然后从开始菜单打开「大肥鱼桌宠」。安装于当前用户目录，可从 Windows「已安装的应用」中卸载，无需管理员权限。

便携版双击 `Dafeiyu.exe` 即可启动。素材已经内嵌，不需要外置 `assets` 文件夹。适用于 Windows 10/11。

- 在其他软件中移动鼠标、点击左右键或输入键盘，桌宠同步响应。
- 拖动角色移动窗口；右键角色或系统托盘图标打开菜单。
- 菜单包含显示/隐藏、大小、总在最前、锁定位置、重置位置、开机启动和退出。
- 双击托盘图标或再次启动应用可找回隐藏的角色，应用只运行一个实例。
- 托盘的「透明度」提供 25%、50%、75%、100% 四档，100% 为完全不透明。
- 可开启「鼠标悬停时隐藏」：移到角色窗口区域时隐藏并允许点击穿过，移开后按原透明度恢复；可从托盘随时关闭。
- 位置、大小和显示设置自动记忆。开机启动默认关闭，手动勾选后才启用；隐藏时停止绘制与输入监听。
- 背景透明，鼠标与鼠标垫沿用 BongoCat 素材，鼠标垫配色调整为浅蓝灰；角色与外设使用较高分辨率渲染。
- 紧凑键盘采用浅蓝圆角底座、圆形及胶囊形键帽，按角色操作方向呈现透视。原生绘制 Q/W/E/R、A/S/D、Tab、Shift、Ctrl、Space、Enter 共 12 个键，按下时呈淡金色；左右 Ctrl、Shift 分别共用键帽。
- 保留 55 种既有输入映射，其他键也会触发通用打字动作，不高亮无关键帽。组合键可同时高亮，手臂跟随最后按下且尚未松开的键。
- 打字时随机出现开心、眨眼或惊讶表情，短暂显示后恢复自然表情；冷却间隔避免连续闪换。
- 通过只读 Windows Raw Input 接收键盘和鼠标按钮事件，短按键及点击至少显示 55 毫秒。鼠标位置只读取，不模拟按键或移动系统指针，输入不会被吞掉、保存成文本或发送到网络。

## 实现

这是独立 Windows 桌面程序，不是官方 BongoCat 二进制文件或可导入的 Live2D 模型。肩膀固定，上臂、前臂和手部使用固定空间长度，在浅三维空间中计算关节。手腕顺着前臂，袖口固定在腕关节，手部按透视等比例缩短。鼠标侧肘部适度向外，大臂在移动范围内的画面外展角度约 25–34 度。

键盘移近角色，并缩短键盘侧手臂，减少伸手过远；键帽与手部落点共用同一透视变换。

静止时跳过内容未变化的画面，减少待机绘制开销。

此版没有独立手指关节、完整角色骨骼或 BongoCat 的模型切换功能。设置保存在 `%LOCALAPPDATA%/Dafeiyu/settings.xml`。

## 从源码构建

仓库根目录执行以下命令，需要 Python 3.10+、Node.js 20+ 和 Windows 自带的 .NET Framework C# 编译器：

```powershell
python -m pip install -r requirements.txt
powershell -ExecutionPolicy Bypass -File native-pet/build.ps1
```

重建前退出正在运行的同目录桌宠。使用 `-OutputDirectory output/my-build` 可输出到另一个目录。构建会生成素材、编译程序，并检查 Raw Input 数据包离线解析、55 种既有输入映射、通用按键反馈、12 个可见键帽及其别名、3025 组转换中的 181500 个中间姿势、鼠标移动范围和表情时序。这些离线检查不会注册输入设备或生成真实输入。

程序源码为 `native-pet/JointedPet.cs`，素材构建入口为 `scripts/build-jointed-assets.py`。默认程序目录为 `output/Dafeiyu-Jointed`，中间素材与检查结果为 `output/jointed-checks`。

制作安装包需要 Inno Setup 6，在仓库根目录执行 `powershell -ExecutionPolicy Bypass -File native-pet/package.ps1`。输出为 `output/installer/Dafeiyu-Setup-1.1.0.exe`；打包脚本不会安装程序或开启自启动。

BongoCat 素材来源与许可证见仓库根目录 `UPSTREAM_REVISION.txt`、`LICENSE`；随程序分发时许可证为 `LICENSE-BongoCat.txt`。角色图的使用与再分发范围由权利人授权决定。
