# Vonvert

> Language：[English](README.md) · [简体中文](README_zh.md)

**Windows 实时变声器** — 说话的同时改变你的声音,低延迟 WASAPI 音频,开箱即用的 5 个声线预设。

Vonvert 捕获你的麦克风信号,通过实时 DSP 链（音高变换、均衡器、压缩器、合唱等）处理后输出到任意播放设备——通常是虚拟音频线,让聊天应用/游戏将变声后的音频作为其麦克风。

## 功能特性

- **实时变声** — 内置 5 个声线预设（Normal、Deep Male、Female、Robot、Demon）,启动即自动应用女声（Female）
- **低延迟** — WASAPI 采集/播放,约 10 ms 处理窗口
- **监听自己（Hear Myself）** — 可选耳返回环,让你能在耳机中听到自己的变声效果（设置 → 监听自己）
- **实时可视化** — 说话时显示实时频谱和音高
- **设备选择** — 自由选取输入麦克风和输出设备；VB-Cable 友好
- **极简专注的界面** — 中英双语界面（自动跟随系统语言，可在设置里切换），系统托盘图标，零配置负担

## 系统要求

- Windows 10/11
- 一个麦克风 + 耳机/音箱
- 可选但推荐：**虚拟音频设备**,如 [VB-Cable](https://vb-audio.com/Cable/),使变声后的音频成为聊天应用或游戏中可选的麦克风。VoiceMeeter 和 VAC 同样适用。请参阅[虚拟音频设备设置指南](docs/zh/VB-CABLE.zh.md)和[设备完整对比](docs/zh/VIRTUAL-AUDIO-DEVICES.zh.md)。

## 快速开始

> 初次使用 Vonvert？请阅读完整的**[用户使用指南](docs/zh/USER-GUIDE.zh.md)**,了解分步设置、声线、路由、快捷键与故障排查。

1. 安装虚拟音频设备——推荐使用 [VB-Cable](https://vb-audio.com/Cable/)（VoiceMeeter / VAC 亦可）；详见[设置指南](docs/zh/VB-CABLE.zh.md)。
2. 启动 `Vonvert.exe`。
3. 打开**设置**（左侧导航栏）：
   - **输入麦克风** → 你的真实麦克风（如耳机麦克风）
   - **输出** → `CABLE Input (VB-Audio Virtual Cable)`
   - 开启**监听自己**可在耳机中听到你的变声。
4. 在你的聊天应用/游戏中,将麦克风设为 `CABLE Output (VB-Audio Virtual Cable)`。
5. 说话——内置女声（Female）立即生效（变声默认开启）；可在声线选择器中切换这 5 个预设。

## 从源码构建

需要 [.NET 10 SDK](https://dotnet.microsoft.com/download)（Windows）。

```bash
# 构建并发布为自包含 exe：
# 输出路径：Vonvert.App/bin/publish/Vonvert.exe
build.bat            # 或：python build.py

# 清理后构建
build.bat --clean    # 或：python build.py --clean

# 仅构建（不发布）
dotnet build Vonvert.OSS.sln -c Release
```

脚本还支持本地代码签名测试：`build.bat --sign`（或
`python build.py --sign`）——需要代码签名证书和
`VONVERT_PFX_PASSWORD` 环境变量。

### 安装包（可选,NSIS）

CI 会构建一个用户级安装包（`installer/setup.oss.nsi`,安装到
`%LOCALAPPDATA%\Programs\Vonvert`——无需管理员权限）。本地构建需要将
[NSIS](https://nsis.sourceforge.io) 加入 PATH：

```bash
makensis /DBUILD_DIR=..\Vonvert.App\bin\publish installer\setup.oss.nsi
# 输出路径：installer/Output/Vonvert_Setup.exe
```

## 运行测试

```bash
dotnet test Vonvert.OSS.sln -c Release
```

测试套件覆盖音高变换引擎（变声核心）、环形调制与软削峰失真效果器、内置预设包（加载与 DSP 链装配）、端到端女声预设处理路径以及音频管线契约。

## 项目结构

```
Vonvert.App/       WPF 用户界面（窗口、控件、本地化）
Vonvert.Engine/    音频引擎：WASAPI 采集/输出、DSP 链、预设、
                   实时分析器
test/              xUnit 测试套件
```

## 许可证

Apache License 2.0 — 详见 [LICENSE](LICENSE)。第三方组件及其许可证列于 [NOTICE](NOTICE)。
