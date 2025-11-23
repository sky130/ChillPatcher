using BepInEx.Configuration;

namespace ChillPatcher
{
    public static class PluginConfig
    {
        // 语言设置
        public static ConfigEntry<int> DefaultLanguage { get; private set; }

        // 用户ID设置
        public static ConfigEntry<string> OfflineUserId { get; private set; }

        // DLC设置
        public static ConfigEntry<bool> EnableDLC { get; private set; }

        // 键盘钩子设置
        public static ConfigEntry<int> KeyboardHookInterval { get; private set; }

        // Rime输入法设置
        public static ConfigEntry<string> RimeSharedDataPath { get; private set; }
        public static ConfigEntry<string> RimeUserDataPath { get; private set; }
        public static ConfigEntry<bool> EnableRimeInputMethod { get; private set; }

        public static void Initialize(ConfigFile config)
        {
            // 语言设置 - 使用枚举值
            DefaultLanguage = config.Bind(
                "Language",
                "DefaultLanguage",
                3, // 默认值：ChineseSimplified = 3
                new ConfigDescription(
                    "默认游戏语言\n" +
                    "枚举值说明：\n" +
                    "0 = None (无)\n" +
                    "1 = Japanese (日语)\n" +
                    "2 = English (英语)\n" +
                    "3 = ChineseSimplified (简体中文)\n" +
                    "4 = ChineseTraditional (繁体中文)\n" +
                    "5 = Portuguese (葡萄牙语)",
                    new AcceptableValueRange<int>(0, 5)
                )
            );

            // 离线用户ID设置
            OfflineUserId = config.Bind(
                "SaveData",
                "OfflineUserId",
                "OfflineUser",
                "离线模式使用的用户ID，用于存档路径\n" +
                "修改此值可以使用不同的存档槽位，或读取原Steam用户的存档\n" +
                "例如：使用原Steam ID可以访问原来的存档"
            );

            // DLC设置
            EnableDLC = config.Bind(
                "DLC",
                "EnableDLC",
                false,
                "是否启用DLC功能\n" +
                "true = 启用DLC\n" +
                "false = 禁用DLC（默认）"
            );

            // 键盘钩子消息循环间隔
            KeyboardHookInterval = config.Bind(
                "KeyboardHook",
                "MessageLoopInterval",
                10,
                new ConfigDescription(
                    "键盘钩子消息循环检查间隔（毫秒）\n" +
                    "默认值：10ms（推荐）\n" +
                    "较小值：响应更快，CPU占用略高\n" +
                    "较大值：CPU占用低，响应略慢\n" +
                    "建议范围：1-10ms",
                    new AcceptableValueRange<int>(1, 100)
                )
            );

            // Rime输入法配置
            EnableRimeInputMethod = config.Bind(
                "Rime",
                "EnableRimeInputMethod",
                true,
                "是否启用Rime输入法引擎\n" +
                "true = 启用Rime（默认）\n" +
                "false = 使用简单队列输入"
            );

            RimeSharedDataPath = config.Bind(
                "Rime",
                "SharedDataPath",
                "",
                "Rime共享数据目录路径（Schema配置文件）\n" +
                "留空则自动查找，优先级：\n" +
                "1. BepInEx/plugins/ChillPatcher/rime-data/shared\n" +
                "2. %AppData%/Rime\n" +
                "3. 此配置指定的自定义路径"
            );

            RimeUserDataPath = config.Bind(
                "Rime",
                "UserDataPath",
                "",
                "Rime用户数据目录路径（词库、用户配置）\n" +
                "留空则使用：BepInEx/plugins/ChillPatcher/rime-data/user"
            );

            Plugin.Logger.LogInfo("配置文件已加载:");
            Plugin.Logger.LogInfo($"  - 默认语言: {DefaultLanguage.Value}");
            Plugin.Logger.LogInfo($"  - 离线用户ID: {OfflineUserId.Value}");
            Plugin.Logger.LogInfo($"  - 启用DLC: {EnableDLC.Value}");
            Plugin.Logger.LogInfo($"  - 键盘钩子间隔: {KeyboardHookInterval.Value}ms");
            Plugin.Logger.LogInfo($"  - 启用Rime: {EnableRimeInputMethod.Value}");
            if (!string.IsNullOrEmpty(RimeSharedDataPath.Value))
                Plugin.Logger.LogInfo($"  - Rime共享目录: {RimeSharedDataPath.Value}");
            if (!string.IsNullOrEmpty(RimeUserDataPath.Value))
                Plugin.Logger.LogInfo($"  - Rime用户目录: {RimeUserDataPath.Value}");
        }
    }
}
