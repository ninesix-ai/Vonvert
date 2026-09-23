# 虚拟音频设备设置指南（VB-Cable）

> 语言：[English](../VB-CABLE.md) · 简体中文

Vonvert 实时改变你的声音,但要让 **其他应用听到变声后的音频**（Discord、Zoom、Teams、OBS、游戏等）,处理后的声音必须以"麦克风"的形式重新输入到这些应用中。这就是**虚拟音频设备**（虚拟音频线）所做的事。本指南以 VB-Audio 的 **VB-Cable** 为推荐默认方案,但 **Vonvert 兼容任何虚拟音频设备**——VoiceMeeter 和 VAC 的信息请见 [§5 其他虚拟音频设备](#5-其他虚拟音频设备)。

本指南将带你完成虚拟音频线的下载、安装和与 Vonvert 的接线配置。虚拟音频设备**可选但强烈推荐**：没有它你仍可使用*监听自己（Hear Myself）*功能,但聊天应用和游戏无法将变声后的音频识别为麦克风。

> VB-Cable 是 VB-Audio 提供的第三方专有（免费）驱动。它**不包含**在 Vonvert 中,也**不会自动安装**——需要你手动安装一次。因为它是系统音频驱动,安装需要管理员权限。

---

## 1. 下载

1. 打开官方页面：<https://vb-audio.com/Cable/>
   - 提示：如果 Vonvert 未检测到虚拟音频设备,**底部状态栏会显示可点击的"设置虚拟音频设备 →"链接**——点击即可打开本页面。
2. 找到 **Download VB-Cable** 并下载安装包 zip（`VB-CABLE_Driver.zip`）。

## 2. 安装

1. 解压下载的文件。
2. **右键点击 `VBCABLE_Setup_x64.exe` → *以管理员身份运行***
   （64 位 Windows；32 位系统使用 `VBCABLE_Setup.exe`）。
   > 安装程序没有管理员权限将无法安装——普通双击可能静默失败。请始终以管理员身份运行。
3. 按提示操作,完成后**重启**电脑。
4. 重启后,系统中会出现两个新设备：
   - **`CABLE Input`** — 播放设备。Vonvert 将变声后的音频发送至此。
   - **`CABLE Output`** — 录音设备（显示为麦克风）。聊天应用或游戏从此处读取输入。

## 3. 在 Vonvert 中配置

1. 启动 `Vonvert.exe`。
2. 打开**设置**（左侧导航栏）并配置：
   - **输入麦克风** → 你的**真实**麦克风（如耳机麦克风）。
   - **输出** → **`CABLE Input (VB-Audio Virtual Cable)`**。
3.（可选）开启 **监听自己（Hear Myself）**,可在耳机中听到自己的变声效果。
4. 底部状态栏此时应显示"已检测到虚拟音频设备"。

## 4. 在聊天应用/游戏中选择虚拟麦克风

在你需要使用变声音频的应用（Discord、Zoom、OBS、游戏等）的语音/音频设置中,将**输入设备/麦克风**设为：

> **`CABLE Output (VB-Audio Virtual Cable)`**

说话——对方现在听到的是 Vonvert 处理后的变声音频,而非你的原始麦克风。

### 音频路由示意

```
你的麦克风 ──► Vonvert（DSP：音高/均衡/…）──► CABLE Input
                                                        │  （虚拟音频线）
                                                        ▼
对方应用 ◄──────────────────────────────────  CABLE Output（识别为麦克风）
```

## 5. 其他虚拟音频设备

VB-Cable 是推荐默认,但 Vonvert 兼容**任何** WASAPI 虚拟音频设备——只需在**设置 → 输出**中选择你已安装的设备。完整对比参考 **[虚拟音频设备：完整对比](VIRTUAL-AUDIO-DEVICES.zh.md)**。

- **VoiceMeeter**（Basic / Banana / Potato）——全功能虚拟调音台,内置多条总线（B1、B2…）。将 Vonvert **输出设为 `VoiceMeeter Input`**,然后在聊天应用中选择 `VoiceMeeter B1 Out` 作为麦克风。适合需要混合背景音乐、多麦克风或使用 ASIO 的场景。
- **VAC（Virtual Audio Cable）**——付费专业工具,支持创建多条独立虚拟线、自定义采样率和规则化路由引擎。适合多流隔离/录音室环境。

> 底部状态栏会识别**任何**名称中包含"CABLE"（VB-Cable、VAC）或"VoiceMeeter"的已检测虚拟设备——你**不必**只使用 VB-Cable。如果使用非 VB-Cable 设备,安装后请在**设置 → 输出**中手动选择。

## 6. 故障排查

| 症状 | 解决方法 |
|---|---|
| 状态栏一直提示"设置虚拟音频设备 →" | 确认以**管理员身份**安装并**重启**；在 Vonvert 设备设置中点击**刷新**重新枚举。 |
| 对方听到的是你的原始声音 | 确认应用的**麦克风**设为 `CABLE Output`,不是你的真实麦克风。 |
| 完全无声音/无处理 | 确认 Vonvert 的**输出**设为 `CABLE Input`。 |
| 回声/啸叫 | 关闭*监听自己（Hear Myself）*,或使用耳机代替音箱。 |
| Windows 中找不到 `CABLE Input/Output` | 驱动未安装成功——重新以**管理员身份**运行 `VBCABLE_Setup_x64.exe` 并重启。 |

## 7. 卸载

从 *Windows 设置 → 应用* 中移除 **VB-Cable**（或运行 VB-Audio 下载中的卸载程序）并重启。Vonvert 没有它仍可运行——只是无法将变声音频路由到其他应用。

---

**官方下载页面：** <https://vb-audio.com/Cable/>

想使用其他虚拟音频设备？参阅 **[虚拟音频设备：完整对比](VIRTUAL-AUDIO-DEVICES.zh.md)**。

VB-Cable © VB-Audio Software。它是独立的专有（免费）产品,受其自身许可证约束,不属于 Vonvert 的 Apache-2.0 许可证。
