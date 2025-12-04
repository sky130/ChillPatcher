using Bulbul;
using HarmonyLib;
using R3;
using System;
using System.Collections.Generic;
using System.Linq;
using ChillPatcher.UIFramework.Music;

namespace ChillPatcher.Patches.UIFramework
{
    /// <summary>
    /// 移除100首音乐导入限制 - 可配置开关
    /// 默认关闭，不影响原游戏行为和存档兼容性
    /// 仅在用户明确启用时生效
    /// </summary>
    [HarmonyPatch]
    public class MusicService_RemoveLimit_Patch
    {
        /// <summary>
        /// 全局MusicService实例引用 - 供CustomTagManager使用
        /// </summary>
        public static MusicService CurrentInstance { get; internal set; }
        
        /// <summary>
        /// Patch MusicService.AddMusicItem - 可选移除100首限制
        /// </summary>
        [HarmonyPatch(typeof(MusicService), "AddMusicItem")]
        [HarmonyPrefix]
        static bool AddMusicItem_Prefix(MusicService __instance, GameAudioInfo music, ref bool __result)
        {
            // ✅ 保存实例引用
            CurrentInstance = __instance;
            // 检查配置开关：启用文件夹歌单或无限歌曲时，移除100首限制
            if (!UIFrameworkConfig.EnableUnlimitedSongs.Value && !PluginConfig.EnableFolderPlaylists.Value)
            {
                return true; // 两者都关闭，执行原方法（保持100首限制）
            }

            if (music == null)
            {
                __result = false;
                return false;
            }

            // 获取私有字段 _allMusicList
            var allMusicList = Traverse.Create(__instance)
                .Field("_allMusicList")
                .GetValue<List<GameAudioInfo>>();

            if (allMusicList == null)
            {
                Plugin.Log.LogError("Failed to get _allMusicList from MusicService");
                return true; // 执行原方法
            }

            // ========== 关键：移除100首限制 ==========
            // 原代码：if (allMusicList.Count >= 100) return false;
            // 我们直接跳过这个检查

            // 处理本地PC音乐
            if (music.PathType == AudioMode.LocalPc)
            {
                // 检查路径是否已存在
                if (allMusicList.Any(m => m.LocalPath == music.LocalPath))
                {
                    Plugin.Log.LogWarning($"LocalPath already exists: {music.LocalPath}");
                    __result = false;
                    return false;
                }
            }

            // 检查UUID是否已存在
            if (allMusicList.Any(m => m.UUID == music.UUID))
            {
                Plugin.Log.LogWarning($"UUID already exists: {music.UUID}");
                __result = false;
                return false;
            }

            // 添加到列表
            allMusicList.Add(music);

            // ✅ 合并自定义Tag到音频对象
            var customTags = CustomTagManager.Instance.GetSongCustomTags(music.UUID);
            if (customTags != 0)
            {
                music.Tag |= customTags;
                Plugin.Log.LogDebug($"[TagMerge] {music.Title}: {music.Tag} (merged custom: {customTags})");
            }

            // ⚠️ 注释掉存档保存：运行时加载的歌曲不需要保存到存档
            // SaveDataManager.Instance.MusicSetting.PlaylistOrder.Add(music.UUID);
            // SaveDataManager.Instance.SaveMusicSetting();

            // 更新当前播放列表
            var currentAudioTag = SaveDataManager.Instance.MusicSetting.CurrentAudioTag;

            if (currentAudioTag != null && currentAudioTag.CurrentValue.HasFlagFast(music.Tag))
            {
                var shuffleList = Traverse.Create(__instance)
                    .Field("shuffleList")
                    .GetValue<List<GameAudioInfo>>();

                var currentPlayList = __instance.CurrentPlayList;

                shuffleList?.Add(music);
                currentPlayList?.Add(music);

                // 设置脏标记
                Traverse.Create(__instance)
                    .Property("IsPlayListDirtyForLocalImport")
                    .SetValue(true);
            }

            __result = true;
            Plugin.Log.LogInfo($"[Unlimited] Added music: {music.Title} (Total: {allMusicList.Count})");

            // 阻止原方法执行
            return false;
        }

        /// <summary>
        /// Patch MusicService.AddLocalMusicItem - 可选移除100首限制
        /// </summary>
        [HarmonyPatch(typeof(MusicService), "AddLocalMusicItem")]
        [HarmonyPrefix]
        static bool AddLocalMusicItem_Prefix(MusicService __instance, GameAudioInfo music, ref bool __result)
        {
            // ✅ 保存实例引用
            CurrentInstance = __instance;

            // 检查配置开关：启用文件夹歌单或无限歌曲时，移除100首限制
            if (!UIFrameworkConfig.EnableUnlimitedSongs.Value && !PluginConfig.EnableFolderPlaylists.Value)
            {
                return true; // 两者都关闭，执行原方法
            }

            if (music == null || music.PathType != AudioMode.LocalPc || string.IsNullOrEmpty(music.LocalPath))
            {
                __result = false;
                return false;
            }

            var allMusicList = Traverse.Create(__instance)
                .Field("_allMusicList")
                .GetValue<List<GameAudioInfo>>();

            if (allMusicList == null)
            {
                return true; // 执行原方法
            }

            // ========== 移除100首限制 ==========

            // 检查是否已存在
            if (allMusicList.Any(m => m.LocalPath == music.LocalPath))
            {
                Plugin.Log.LogWarning($"LocalPath already exists: {music.LocalPath}");
                __result = false;
                return false;
            }

            if (allMusicList.Any(m => m.UUID == music.UUID))
            {
                Plugin.Log.LogWarning($"UUID already exists: {music.UUID}");
                __result = false;
                return false;
            }

            // 添加到列表
            allMusicList.Add(music);

            // ✅ 合并自定义Tag到音频对象
            var customTags = CustomTagManager.Instance.GetSongCustomTags(music.UUID);
            if (customTags != 0)
            {
                music.Tag |= customTags;
                Plugin.Log.LogDebug($"[TagMerge] {music.Title}: {music.Tag} (merged custom: {customTags})");
            }

            // ⚠️ 注释掉存档保存：运行时加载的歌曲不需要保存到存档
            // SaveDataManager.Instance.MusicSetting.PlaylistOrder.Add(music.UUID);
            // SaveDataManager.Instance.SaveMusicSetting();

            // 更新播放列表
            var currentAudioTag = SaveDataManager.Instance.MusicSetting.CurrentAudioTag;

            // ✅ 检查歌曲的Tag是否匹配当前筛选Tag（支持自定义Tag）
            if (currentAudioTag != null && currentAudioTag.CurrentValue.HasFlagFast(music.Tag))
            {
                var shuffleList = Traverse.Create(__instance)
                    .Field("shuffleList")
                    .GetValue<List<GameAudioInfo>>();

                var currentPlayList = __instance.CurrentPlayList;

                shuffleList?.Add(music);
                currentPlayList?.Add(music);
            }

            // 触发导入完成事件
            var onCompleteImportMusic = Traverse.Create(__instance)
                .Field("onCompleteImportMusic")
                .GetValue<R3.Subject<R3.Unit>>();

            onCompleteImportMusic?.OnNext(R3.Unit.Default);

            Traverse.Create(__instance)
                .Property("IsPlayListDirtyForLocalImport")
                .SetValue(true);

            __result = true;
            Plugin.Log.LogInfo($"[Unlimited] Added local music: {music.Title} (Total: {allMusicList.Count})");

            return false;
        }
    }

    /// <summary>
    /// MusicService.Load补丁：保存实例引用供延迟加载使用
    /// </summary>
    [HarmonyPatch(typeof(MusicService), "Load")]
    public static class MusicService_Load_SaveInstance_Patch
    {
        [HarmonyPostfix]
        static void Postfix(MusicService __instance)
        {
            // ✅ 保存实例引用供后续使用
            MusicService_RemoveLimit_Patch.CurrentInstance = __instance;
        }
    }

    [HarmonyPatch(typeof(MusicService))]
    public static class MusicService_Block_Origin
    {
        /// <summary>
        /// Harmony补丁 - 拦截内置播放器播放
        /// </summary>
        [HarmonyPatch(typeof(MusicService), "PlayArugumentMusic")]
        [HarmonyPrefix]
        public static bool PlayArugumentMusic_Prefix()
        {
            if (UIFrameworkConfig.EnableThirdPlayerMediaTransportControls.Value)
            {
                Plugin.Logger.LogInfo("[ThirdPlayer] PlayArugumentMusic_Prefix");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Harmony补丁 - 拦截内置播放器播放
        /// </summary>
        [HarmonyPatch(typeof(MusicService), "PlayMusicInPlaylist")]
        [HarmonyPrefix]
        public static bool PlayMusicInPlaylist_Prefix()
        {
            if (UIFrameworkConfig.EnableThirdPlayerMediaTransportControls.Value)
            {
                Plugin.Logger.LogInfo("[ThirdPlayer] PlayMusicInPlaylist_Prefix");
                return false;
            }

            return true;
        }

        [HarmonyPatch(typeof(MusicService), "PlayNextMusic")]
        [HarmonyPrefix]
        public static bool PlayNextMusic_Prefix()
        {
            if (UIFrameworkConfig.EnableThirdPlayerMediaTransportControls.Value)
            {
                Plugin.Logger.LogInfo("[ThirdPlayer] PlayNextMusic_Prefix");
                return false;
            }
            return true;
        }
        
        [HarmonyPatch(typeof(MusicService), "PlayBackMusic")]
        [HarmonyPrefix]
        public static bool PlayBackMusic_Prefix()
        {
            if (UIFrameworkConfig.EnableThirdPlayerMediaTransportControls.Value)
            {
                Plugin.Logger.LogInfo("[ThirdPlayer] PlayBackMusic_Prefix");
                return false;
            }
            return true;
        }
        
        [HarmonyPatch(typeof(MusicService), "SkipCurrentMusic")]
        [HarmonyPrefix]
        public static bool SkipCurrentMusic_Prefix()
        {
            if (UIFrameworkConfig.EnableThirdPlayerMediaTransportControls.Value)
            {
                Plugin.Logger.LogInfo("[ThirdPlayer] SkipCurrentMusic_Prefix");
                return false;
            }
            return true;
        }
        
        [HarmonyPatch(typeof(MusicService), "SetMusicProgress")]
        [HarmonyPrefix]
        public static bool SetMusicProgress_Prefix(float progress)
        {
            if (UIFrameworkConfig.EnableThirdPlayerMediaTransportControls.Value)
            {
                Plugin.Logger.LogInfo($"[ThirdPlayer] SetMusicProgress_Prefix {progress}");
                return false;
            }
            return true;
        }
    }
}