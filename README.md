# ChillPatcher

这是一个 BepInEx 插件，包括多种新的功能改进
- 为游戏真正的flac支持.
- 为游戏提供歌曲批量导入和歌单管理.
- 使《Chill With You》游戏正确在 Wallpaper Engine 环境下运行.
- 添加了游戏内输入法.

## 🐛 遇到问题？

**请提供日志文件以便排查！**

日志文件位置：
```
# 插件日志
<游戏目录>\BepInEx\LogOutput.log
# unity 日志
C:\Users\<你的用户名>\AppData\LocalLow\Nestopi\Chill With You\Player.log
```

提交 Issue 时请附带日志文件，否则可能无法定位问题！

---

## ✨ 主要功能

### 核心功能
- **🎮 壁纸引擎模式运行**：无需 Steam 即可启动游戏
- **💾 存档切换**：支持多个存档槽位，或读取原 Steam 用户的存档
- **⌨️ 桌面输入支持**：在 Wallpaper Engine 中可以直接从桌面输入
- **🇨🇳 中文输入法**：集成 RIME 中州韵输入法引擎，支持拼音、双拼等多种输入方案
- **🌍 语言切换**：自定义默认语言设置
- **🎁 DLC 控制**：可选启用或禁用 DLC 功能
- **🎵 歌曲扩充**：歌曲功能扩充

### 性能优化
- **⚡ 虚拟滚动**：只渲染可见的音乐列表项，大幅提升性能
  - 支持 2000+ 首歌曲不卡顿
  - 内存占用降低 90%+
  - 滚动流畅丝滑

### 关于歌曲扩充
- **📁 文件夹播放列表**：自动扫描音频文件夹，按目录生成播放列表
- **🎵 扩展音频格式**：支持 OGG、FLAC、AIFF、.egg
- **🔢 突破限制**：扩充 AudioTag 到 16 位限制，支持12个额外自定义标签, 可扩充曲目上限

## 📦 安装方式

### 1. 安装 BepInEx

1. 下载 [BepInEx 5.x](https://github.com/BepInEx/BepInEx/releases)
2. 解压到 游戏exe 所在目录：
   ```
   # steam 安装
   steamapps\common\Chill with You Lo-Fi Story
   # 壁纸引擎项目
   wallpaper_engine\projects\myprojects\chill_with_you
   ```

### 2. 安装 ChillPatcher 插件

1. 从 [Releases](../../releases) 下载最新的 `ChillPatcher.zip`
2. 将 `ChillPatcher.zip` 中的文件夹`ChillPatcher`解压复制到：
   ```
   # steam 安装
   steamapps\common\Chill with You Lo-Fi Story\BepInEx\plugins\
   # 壁纸引擎项目
   wallpaper_engine\projects\myprojects\chill_with_you\BepInEx\plugins\
   ```

### 3. 完成！

## **FLAC 支持说明**

- **原游戏问题**：原游戏的音频类型识别仅对 `.mp3`/`.wav` 有明确处理，Unity 对运行时 `.flac` 的支持有限。
  - ❌ 采样率可能错误识别（导致播放速度不对）
  - ❌ 某些平台不支持 FLAC
  - ❌ 行为不一致（Windows Editor 可用，Standalone 可能失败）

- **插件如何解决**：本插件包含一个基于 `dr_flac` 的原生解码器（`NativePlugins/FlacDecoder`），并通过 Harmony 补丁拦截游戏的音频加载流程：
  - 当启用扩展格式（`EnableExtendedFormats`）且遇到 `.flac` 文件时，插件会优先使用原生解码器进行流式解码和播放（使用 `AudioClip.Create(..., stream: true)` + PCM 回调）。
  - 原生解码器导出流式 API（`OpenFlacStream` / `ReadFlacFrames` / `SeekFlacStream` / `CloseFlacStream`），托管层通过 `Native/FlacDecoder.cs` 的 `FlacStreamReader` 进行安全封装，保证低内存占用与可 seek 行为。

更多详细信息和构建选项请参见 [FlacDecoder](NativePlugins/FlacDecoder/README.md)。

## ⚙️ 配置选项

配置文件位于：`<游戏目录>\BepInEx\config\com.chillpatcher.plugin.cfg`

### 壁纸引擎模式

EnableWallpaperEngineMode更改为是来启动壁纸引擎模式
此模式不需要steam授权,并且可以鼠标点击交互

#### 关于时长和成就
- **无法获取steam游戏时长**
- **缓存的成就**

壁纸引擎模式会缓存成就到本地,当在线启动之后会自动从缓存的成就尝试为steam解锁,但是需要设置OfflineUserId为你的steamid.就是你的steam存档名.在

```
C:\Users\<你的用户名>\AppData\LocalLow\Nestopi\Chill With You\SaveData\Release\v2
```

你的steam安装的游戏也需要安装此插件,并且没有开启壁纸引擎模式,才会尝试从缓存同步成就

### 框架功能

```ini
[Features]

## 无限的歌曲导入(不开也可以用文件夹无限导入,用于破解官方导入限制)
## 可能影响存档兼容性
## Enable unlimited song import (may affect save compatibility)
# Setting type: Boolean
# Default value: false
EnableUnlimitedSongs = false

## 不限歌曲导入格式(不开也可以用文件夹导入,用于破解官方导入限制)
## 可能影响存档兼容性
## Enable extended audio formats (OGG, FLAC, AIFF)
# Setting type: Boolean
# Default value: false
EnableExtendedFormats = false

## 虚拟滚动
## Enable virtual scrolling for better performance
# Setting type: Boolean
# Default value: true
EnableVirtualScroll = true

## 文件夹导入功能
## Enable folder-based playlists (runtime only, not saved)
# Setting type: Boolean
# Default value: true
EnableFolderPlaylists = true
```

### 虚拟滚动高级设置

```ini
[Advanced]
## 虚拟滚动缓冲区大小
## 在可见区域前后渲染的额外项目数量
## 较大值：滚动更流畅，内存占用略高
## 较小值：内存占用低，快速滚动可能有延迟
## 推荐值：3-5
VirtualScrollBufferSize = 3
```

### 文件夹播放列表设置

```ini
[Playlist]

## 是否启用文件夹歌单系统
## true = 启用（默认），扫描目录并创建自定义Tag
## false = 禁用，不会扫描文件夹也不会添加自定义Tag
# Setting type: Boolean
# Default value: true
EnableFolderPlaylists = true

## 歌单根目录路径
## 相对路径基于游戏根目录（.dll所在目录）
## 默认：playlist（与ChillPatcher.dll同级的playlist文件夹）
# Setting type: String
# Default value: playlist
RootFolder = playlist

## 目录递归扫描深度
## 0 = 仅扫描根目录
## 1 = 扫描根目录及其一级子目录
## 2 = 扫描两级子目录
## 3 = 扫描三级子目录（默认）
## 建议范围：0-5
# Setting type: Int32
# Default value: 3
# Acceptable value range: From 0 to 10
RecursionDepth = 3

## 是否自动生成playlist.json
## true = 首次扫描目录时自动生成JSON缓存（默认）
## false = 仅使用已存在的JSON文件
# Setting type: Boolean
# Default value: true
AutoGenerateJson = true

## 是否启用歌单缓存
## true = 读取playlist.json缓存，加快启动速度（默认）
## false = 每次启动重新扫描所有音频文件
# Setting type: Boolean
# Default value: true
EnableCache = true
```

**使用示例**：

假设你的音乐文件夹结构如下：
```
Music/
├── Pop/
│   ├── song1.mp3
│   └── song2.ogg
└── Rock/
    ├── album1/
    │   ├── track1.flac
    │   └── track2.flac
    └── album2/
        └── track3.mp3

```

配置 `RootFolder = Music` 和 `RecursionDepth = 2`，将自动生成以下播放列表：
- 📁 Pop (2 首)
- 📁 Rock (1 首) 
- 📁 Rock/album1 (2 首)
- 📁 Rock/album2 (1 首)

**支持的音频格式**：
- `.mp3` - MP3 (MPEG Audio)
- `.wav` - WAV (Waveform Audio)
- `.ogg` - Ogg Vorbis
- `.egg` - Ogg Vorbis
- `.flac` - FLAC (Free Lossless Audio Codec)
- `.aiff` / `.aif` - AIFF (Audio Interchange File Format)

**如何添加新歌曲（增量更新）**：

首次运行后，每个歌单文件夹会生成两个文件：
```
playlist/
├── 我的收藏/
│   ├── !rescan_playlist    ← 扫描标志文件
│   ├── playlist.json       ← 歌曲缓存
│   ├── song1.mp3
│   └── song2.mp3
```

要添加新歌曲：
1. 将新的音频文件放入歌单文件夹
2. 删除该文件夹中的 `!rescan_playlist` 文件
3. 重启游戏

系统会：
- ✅ 保留已有歌曲的 UUID（收藏、排序、排除状态不丢失）
- ✅ 为新歌曲分配新的 UUID
- ✅ 更新 `playlist.json` 缓存
- ✅ 重新创建 `!rescan_playlist` 标志文件

**注意**：
- 每个歌单文件夹独立管理，互不影响
- 只需删除需要更新的文件夹的标志文件
- 不删除标志文件时，使用缓存快速加载

### 语言设置

不重要,仅无存档时生效

```ini
[Language]
## 默认游戏语言
## 0 = None (无)
## 1 = Japanese (日语)
## 2 = English (英语)
## 3 = ChineseSimplified (简体中文) - 默认
## 4 = ChineseTraditional (繁体中文)
## 5 = Portuguese (葡萄牙语)
DefaultLanguage = 3
```

### 存档设置
```ini
[SaveData]
## 离线模式使用的用户ID
## 修改此值可以使用不同的存档槽位，或读取原Steam用户的存档
## 例如：将其改为你的 Steam ID 可以访问原来的存档
OfflineUserId = OfflineUser

## 是否使用多存档功能
## true = 使用配置的离线用户ID作为存档路径，可以切换不同存档
## false = 使用Steam ID作为存档路径（默认）
## 注意：启用后即使不在壁纸引擎模式下也会使用配置的存档路径
# Setting type: Boolean
# Default value: false
UseMultipleSaveSlots = false
```

**如何使用原 Steam 存档？**

1. 找到你的 Steam ID（17 位数字）
2. 修改配置文件中的 `OfflineUserId = 你的SteamID`
3. 重启游戏即可使用原存档

**如何使用多个存档槽位？**

- 开启 `UseMultipleSaveSlots = true`
- 不同的 `OfflineUserId` 对应不同的存档
- 例如：`OfflineUserId = Save1`、`OfflineUserId = Save2`

### DLC 设置
```ini
[DLC]
## 是否启用DLC功能
EnableDLC = false
```

### 键盘钩子设置
```ini
[KeyboardHook]
## 键盘钩子消息循环检查间隔（毫秒）
## 默认值：1ms（推荐）
## 较小值：响应更快，CPU占用略高
## 较大值：CPU占用低，响应略慢
## 建议范围：1-10ms
MessageLoopInterval = 1
```

**调整建议**：
- `1ms` - 最佳响应速度（默认推荐）
- `5ms` - 平衡性能和响应
- `10ms` - 低 CPU 占用

### 中文输入法设置
```ini
[InputMethod]
## 是否启用RIME中文输入法
EnableRimeInputMethod = true

## Rime共享数据目录路径（Schema配置文件）
## 留空则自动查找，优先级：
## 1. BepInEx/plugins/ChillPatcher/rime-data/shared
## 2. %AppData%/Rime
## 3. 此配置指定的自定义路径
# Setting type: String
# Default value: 
SharedDataPath = 

## Rime用户数据目录路径（词库、用户配置）
## 留空则使用：BepInEx/plugins/ChillPatcher/rime-data/user
# Setting type: String
# Default value: 
UserDataPath = 
```

## 🖥️ Wallpaper Engine 使用说明

### 桌面输入功能

当你点击桌面（而不是游戏窗口）时，仍然可以输入到游戏的输入框中：

1. 在游戏中点击输入框（如搜索框、聊天框等）
2. 此时输入框获得焦点
3. 即使你点击了桌面，在键盘上输入的字符仍会被捕获并输入到游戏中

**支持功能**：
- ✅ 中文输入（RIME 输入法引擎）
- ✅ 英文字母、数字、常用符号
- ✅ Backspace（删除）、Delete、方向键
- ✅ Enter（确认）、上下键选择候选词

### 🇨🇳 RIME 中文输入法

本插件集成了 **RIME/中州韵输入法引擎**，这是一个强大的开源跨平台输入法框架。

#### 什么是 RIME？

RIME（Rime Input Method Engine）是一个开源的输入法引擎，支持：
- 🎯 **多种输入方案**：拼音、双拼、五笔、注音等
- 🔧 **高度可定制**：通过 YAML 配置文件自由定制
- 📚 **智能候选**：支持云输入、用户词库、自动学习
- 🌏 **跨平台**：Windows(小狼毫)、macOS(鼠须管)、Linux(ibus-rime)

更多信息请访问：
- 官方网站：https://rime.im/
- GitHub：https://github.com/rime/home
- 详细文档：https://github.com/rime/home/wiki

#### 快捷键

| 按键 | 功能 |
|------|------|
| **F4** | 打开方案选单（切换输入方案、中英标点、全/半角） |
| **F6** | 重新部署 RIME（重新加载配置，无需重启游戏） |
| **上/下** | 选择候选词 |
| **数字键 1-9** | 直接选择对应候选词 |
| **空格** | 确认第一个候选词 |
| **左/右** | 移动拼音光标 |

#### 默认输入方案

首次运行会自动部署以下输入方案：
- 🌙 **明月拼音** (luna_pinyin) - 全拼，默认方案
- 📌 **小鹤双拼** (double_pinyin_flypy)
- 🎹 **自然码双拼** (double_pinyin)
- 🪟 **微软双拼** (microsoft_shuangpin)
- 等等

#### 配置文件路径

RIME 配置文件位于：
```
BepInEx\plugins\ChillPatcher\rime-data\shared
```

常用配置文件：
- `default.yaml` - 全局配置（方案列表、快捷键等）
- `<方案名>.schema.yaml` - 各输入方案配置
- `<方案名>.custom.yaml` - 用户自定义配置（推荐）
- `<方案名>.userdb.txt` - 用户词库（可导入导出）

#### 自定义配置示例

修改 `user/default.custom.yaml`（如不存在请创建）：

```yaml
# 自定义补丁文件
patch:
  # 修改候选词数量
  "menu/page_size": 7
  
  # 修改快捷键
  "switcher/hotkeys":
    - "Control+grave"  # Ctrl+` 切换输入方案
    - "F4"
  
  # 添加自定义方案
  "schema_list":
    - schema: luna_pinyin         # 明月拼音
    - schema: double_pinyin_flypy # 小鹤双拼
```

修改后按 **F6** 重新部署即可生效。

#### 候选词显示格式

- **下标数字** `₁₂₃` - 未选中的候选词
- **上标数字** `¹²³` - 当前选中的候选词

示例：`nihao [你¹ 呢₂ 尼₃ 倪₄]`（当前选中"你"）

#### 常见问题

**Q: 如何切换输入方案？**  
A: 按 `F4` 打开方案选单，用数字键或方向键选择。

**Q: 修改配置后如何生效？**  
A: 按 `F6` 重新部署 RIME，无需重启游戏。

**Q: 如何导入自己的词库？**  
A: 将 `.userdb.txt` 或 `.dict.yaml` 放入 `rime/user/` 目录，按 `F6` 重新部署。

**Q: RIME 输入法有问题怎么办？**  
A: 
- ✅ 请先查看 RIME 官方文档：https://github.com/rime/home/wiki
- ✅ 检查日志文件：`rime/user/logs/`
- ❌ **不要向 RIME 官方仓库提交 issue**（这是第三方集成）
- ✅ 如果确认是本插件集成问题，请在本项目提交 issue

**Q: 如何完全禁用中文输入法？**  
A: 修改配置文件 `BepInEx\config\com.chillpatcher.plugin.cfg`：
```ini
[InputMethod]
EnableRimeInputMethod = false
```

### 清空输入缓冲

如果不想继续输入，只需：
- 在游戏中点击任意位置（鼠标左键）
- 或者点击其他输入框（会自动清理 RIME 状态）

## 🔧 开发构建

```bash
# 克隆仓库
git clone <repository-url>

# 使用 Visual Studio 或 Rider 打开
ChillPatcher.sln

# 构建项目
dotnet build

# 输出目录
bin/Debug/ChillPatcher.dll
```

## ❓ 常见问题

### Q: 游戏启动白屏/卡住？
A: 检查 `BepInEx\LogOutput.log` 查看错误信息。通常是 BepInEx 版本不兼容。

### Q: 桌面输入不起作用？
A: 确保：
1. 游戏输入框已获得焦点
2. 当前前台窗口是桌面（不是其他应用）
3. 尝试调整 `MessageLoopInterval` 配置

### Q: 游戏关闭后进程无响应？
A: 最新版本已修复此问题。如果仍有问题，请查看日志中的 `[KeyboardHook]` 信息。

### Q: 如何禁用桌面输入功能？
A: 暂不支持配置禁用。如需禁用，请移除 `ChillPatcher` 插件，输入法可以禁用。

## 📜 许可证

本项目采用 **GPL v3** 许可证:

- **librime** ([中州韵输入法引擎](https://github.com/rime/librime)) - BSD 3-Clause License
- **BepInEx** - LGPL 2.1 License
- **HarmonyX** - MIT License

根据开源许可兼容性,ChillPatcher 整体采用 GPL v3 许可。详见 [LICENSE](LICENSE) 文件。

**注意**: 本项目仅供学习研究使用。游戏本体版权归原开发者所有,请支持正版。

## 🙏 致谢

- [RIME/中州韵输入法引擎](https://github.com/rime/librime) - 强大的开源输入法引擎
- [BepInEx](https://github.com/BepInEx/BepInEx) - Unity 游戏模组框架
- [HarmonyX](https://github.com/BepInEx/HarmonyX) - .NET 运行时方法补丁库
- [dr_libs](https://github.com/mackron/dr_libs) - flac解码支持
