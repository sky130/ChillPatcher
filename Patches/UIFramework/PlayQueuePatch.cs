using Bulbul;
using ChillPatcher.UIFramework.Music;
using Cysharp.Threading.Tasks;
using HarmonyLib;
using KanKikuchi.AudioManager;
using NestopiSystem;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace ChillPatcher.Patches.UIFramework
{
    /// <summary>
    /// 播放队列系统的 Harmony Patches
    /// 
    /// 拦截 MusicService 的播放流程，使其使用 PlayQueueManager
    /// </summary>
    [HarmonyPatch]
    public static class PlayQueuePatch
    {
        /// <summary>
        /// 是否启用队列系统
        /// </summary>
        public static bool IsQueueSystemEnabled { get; set; } = true;
        
        #region SkipCurrentMusic Patch
        
        /// <summary>
        /// 拦截 SkipCurrentMusic，使用队列系统的下一首
        /// </summary>
        [HarmonyPatch(typeof(MusicService), nameof(MusicService.SkipCurrentMusic))]
        [HarmonyPrefix]
        public static bool SkipCurrentMusic_Prefix(MusicService __instance, MusicChangeKind kind, ref UniTask<bool> __result)
        {
            if (!IsQueueSystemEnabled)
                return true;  // 使用原始逻辑
            
            // 使用队列系统
            __result = SkipWithQueueAsync(__instance, kind);
            return false;  // 跳过原始方法
        }
        
        private static async UniTask<bool> SkipWithQueueAsync(MusicService musicService, MusicChangeKind kind)
        {
            var queueManager = PlayQueueManager.Instance;
            var currentPlaylist = musicService.CurrentPlayList;
            Func<GameAudioInfo, bool> isExcludedFunc = audio => musicService.IsContainsExcludedFromPlaylist(audio);
            
            Plugin.Log.LogInfo($"[PlayQueuePatch] SkipWithQueueAsync: IsInHistoryMode={queueManager.IsInHistoryMode}, IsInExtendedMode={queueManager.IsInExtendedMode}, HistoryPosition={queueManager.HistoryPosition}, ExtendedSteps={queueManager.ExtendedSteps}");
            
            // 如果在扩展模式中，先消耗扩展步数
            if (queueManager.IsInExtendedMode)
            {
                Plugin.Log.LogInfo("[PlayQueuePatch] In extended mode, trying GoNextExtended");
                var nextExtended = queueManager.GoNextExtended(currentPlaylist, queueManager.CurrentPlaying, isExcludedFunc);
                Plugin.Log.LogInfo($"[PlayQueuePatch] GoNextExtended returned: {nextExtended?.AudioClipName ?? "null"}");
                
                if (nextExtended != null)
                {
                    // 从扩展模式获取到了下一首
                    queueManager.SetCurrentPlaying(nextExtended, addToHistory: false);
                    Plugin.Log.LogInfo($"[PlayQueuePatch] Go next in extended mode: {nextExtended.AudioClipName}");
                    return await PlayAudioAsync(musicService, nextExtended, kind);
                }
                // 扩展步数用完了，回到历史最早记录，继续从历史前进
                Plugin.Log.LogInfo("[PlayQueuePatch] Extended steps exhausted, continue from history");
            }
            
            // 如果在历史回溯模式中，先尝试从历史前进
            if (queueManager.IsInHistoryMode)
            {
                Plugin.Log.LogInfo("[PlayQueuePatch] In history mode, trying GoNext");
                var nextInHistory = queueManager.GoNext();
                Plugin.Log.LogInfo($"[PlayQueuePatch] GoNext returned: {nextInHistory?.AudioClipName ?? "null"}");
                
                if (nextInHistory != null)
                {
                    // 从历史获取到了下一首
                    queueManager.SetCurrentPlaying(nextInHistory, addToHistory: false);
                    Plugin.Log.LogInfo($"[PlayQueuePatch] Go next in history: {nextInHistory.AudioClipName}");
                    return await PlayAudioAsync(musicService, nextInHistory, kind);
                }
                // 历史用完了，继续使用队列
                Plugin.Log.LogInfo("[PlayQueuePatch] History exhausted, using queue");
            }
            
            // 获取当前播放列表和随机设置
            bool isShuffle = musicService.IsShuffle;
            
            // 从队列获取下一首
            var nextAudio = queueManager.AdvanceToNext(currentPlaylist, isShuffle, isExcludedFunc);
            
            if (nextAudio == null)
            {
                Plugin.Log.LogWarning("[PlayQueuePatch] No next audio available");
                return false;
            }
            
            // 播放
            return await PlayAudioAsync(musicService, nextAudio, kind);
        }
        
        #endregion
        
        #region PlayNextMusic Patch
        
        /// <summary>
        /// 拦截 PlayNextMusic
        /// nextCount > 0: 下一首（使用队列）
        /// nextCount < 0: 上一首（清除队列当前项，从歌单取上一首）
        /// </summary>
        [HarmonyPatch(typeof(MusicService), nameof(MusicService.PlayNextMusic))]
        [HarmonyPrefix]
        public static bool PlayNextMusic_Prefix(MusicService __instance, int nextCount, MusicChangeKind changeKind, ref UniTask<bool> __result)
        {
            if (!IsQueueSystemEnabled)
                return true;
            
            if (nextCount >= 0)
            {
                // 下一首：使用队列系统
                __result = PlayNextWithQueueAsync(__instance, nextCount, changeKind);
                return false;
            }
            
            // 上一首：清除队列当前项，从歌单取上一首
            __result = PlayPrevWithQueueAsync(__instance, changeKind);
            return false;
        }
        
        /// <summary>
        /// 上一首播放逻辑（使用播放历史记录，历史到头后使用扩展模式）
        /// </summary>
        private static async UniTask<bool> PlayPrevWithQueueAsync(MusicService musicService, MusicChangeKind changeKind)
        {
            var queueManager = PlayQueueManager.Instance;
            var currentPlaylist = musicService.CurrentPlayList;
            Func<GameAudioInfo, bool> isExcludedFunc = audio => musicService.IsContainsExcludedFromPlaylist(audio);
            
            GameAudioInfo prevAudio;
            
            // 如果已经在扩展模式中，继续使用扩展模式
            if (queueManager.IsInExtendedMode)
            {
                prevAudio = queueManager.GoPreviousExtended(currentPlaylist, queueManager.CurrentPlaying, isExcludedFunc);
                if (prevAudio != null)
                {
                    // 设置为当前播放（不添加到历史记录）
                    queueManager.SetCurrentPlaying(prevAudio, addToHistory: false);
                    Plugin.Log.LogInfo($"[PlayQueuePatch] PlayPrev from extended mode: {prevAudio.AudioClipName}");
                    return await PlayAudioAsync(musicService, prevAudio, changeKind);
                }
                // 扩展模式也没有了（不太可能发生），保持当前
                Plugin.Log.LogWarning("[PlayQueuePatch] Extended mode has no more previous songs");
                return false;
            }
            
            // 尝试从历史记录回退
            prevAudio = queueManager.GoPrevious();
            
            if (prevAudio == null)
            {
                // 历史到头了，尝试使用扩展模式
                Plugin.Log.LogInfo("[PlayQueuePatch] History exhausted, trying extended mode");
                prevAudio = queueManager.GoPreviousExtended(currentPlaylist, queueManager.CurrentPlaying, isExcludedFunc);
                
                if (prevAudio == null)
                {
                    Plugin.Log.LogInfo("[PlayQueuePatch] No previous song available (history and extended mode exhausted)");
                    return false;
                }
                
                // 设置为当前播放（不添加到历史记录）
                queueManager.SetCurrentPlaying(prevAudio, addToHistory: false);
                Plugin.Log.LogInfo($"[PlayQueuePatch] PlayPrev from extended mode (first): {prevAudio.AudioClipName}");
                return await PlayAudioAsync(musicService, prevAudio, changeKind);
            }
            
            // 设置为当前播放（不添加到历史记录）
            queueManager.SetCurrentPlaying(prevAudio, addToHistory: false);
            
            Plugin.Log.LogInfo($"[PlayQueuePatch] PlayPrev from history: {prevAudio.AudioClipName}");
            
            return await PlayAudioAsync(musicService, prevAudio, changeKind);
        }
        
        private static async UniTask<bool> PlayNextWithQueueAsync(MusicService musicService, int nextCount, MusicChangeKind changeKind)
        {
            var queueManager = PlayQueueManager.Instance;
            
            // 获取当前播放列表和设置
            var currentPlaylist = musicService.CurrentPlayList;
            bool isShuffle = musicService.IsShuffle;
            
            // 使用 MusicService.IsContainsExcludedFromPlaylist 检查排除
            Func<GameAudioInfo, bool> isExcludedFunc = audio => musicService.IsContainsExcludedFromPlaylist(audio);
            
            GameAudioInfo nextAudio = null;
            
            Plugin.Log.LogInfo($"[PlayQueuePatch] PlayNextWithQueueAsync: nextCount={nextCount}, IsInHistoryMode={queueManager.IsInHistoryMode}, IsInExtendedMode={queueManager.IsInExtendedMode}, HistoryPosition={queueManager.HistoryPosition}, ExtendedSteps={queueManager.ExtendedSteps}");
            
            // 如果在扩展模式中，先消耗扩展步数
            if (queueManager.IsInExtendedMode && nextCount > 0)
            {
                Plugin.Log.LogInfo("[PlayQueuePatch] In extended mode, trying GoNextExtended");
                for (int i = 0; i < nextCount; i++)
                {
                    nextAudio = queueManager.GoNextExtended(currentPlaylist, queueManager.CurrentPlaying, isExcludedFunc);
                    Plugin.Log.LogInfo($"[PlayQueuePatch] GoNextExtended returned: {nextAudio?.AudioClipName ?? "null"}");
                    if (nextAudio == null)
                    {
                        // 扩展步数用完了，回到历史最早记录
                        Plugin.Log.LogInfo("[PlayQueuePatch] Extended steps exhausted, returning to oldest history");
                        // 历史最早记录就是 _history[_historyPosition]
                        if (queueManager.IsInHistoryMode && queueManager.HistoryPosition >= 0)
                        {
                            // 保持在历史最早位置，不做操作
                            nextAudio = queueManager.CurrentPlaying;
                            if (nextAudio != null)
                            {
                                Plugin.Log.LogInfo($"[PlayQueuePatch] Staying at oldest history: {nextAudio.AudioClipName}");
                                return await PlayAudioAsync(musicService, nextAudio, changeKind);
                            }
                        }
                        break;
                    }
                }
                
                if (nextAudio != null)
                {
                    // 从扩展模式获取到了下一首
                    queueManager.SetCurrentPlaying(nextAudio, addToHistory: false);
                    Plugin.Log.LogInfo($"[PlayQueuePatch] Go next in extended mode: {nextAudio.AudioClipName}");
                    return await PlayAudioAsync(musicService, nextAudio, changeKind);
                }
            }
            
            // 如果在历史回溯模式中，先尝试从历史前进
            if (queueManager.IsInHistoryMode && nextCount > 0)
            {
                Plugin.Log.LogInfo("[PlayQueuePatch] In history mode, trying GoNext");
                for (int i = 0; i < nextCount; i++)
                {
                    nextAudio = queueManager.GoNext();
                    Plugin.Log.LogInfo($"[PlayQueuePatch] GoNext returned: {nextAudio?.AudioClipName ?? "null"}");
                    if (nextAudio == null)
                    {
                        // 历史用完了，继续使用队列
                        Plugin.Log.LogInfo("[PlayQueuePatch] History exhausted, using queue");
                        break;
                    }
                }
                
                if (nextAudio != null)
                {
                    // 从历史获取到了下一首
                    queueManager.SetCurrentPlaying(nextAudio, addToHistory: false);
                    Plugin.Log.LogInfo($"[PlayQueuePatch] Go next in history: {nextAudio.AudioClipName}");
                    return await PlayAudioAsync(musicService, nextAudio, changeKind);
                }
            }
            
            if (nextCount == 0)
            {
                // 播放当前（队列第一首，或者从播放列表获取）
                nextAudio = queueManager.CurrentPlaying;
                if (nextAudio == null)
                {
                    nextAudio = queueManager.AdvanceToNext(currentPlaylist, isShuffle, isExcludedFunc);
                }
            }
            else
            {
                // 跳过 nextCount 首（使用队列系统）
                for (int i = 0; i < nextCount; i++)
                {
                    nextAudio = queueManager.AdvanceToNext(currentPlaylist, isShuffle, isExcludedFunc);
                    if (nextAudio == null) break;
                }
            }
            
            if (nextAudio == null)
            {
                Plugin.Log.LogWarning("[PlayQueuePatch] No audio available");
                return false;
            }
            
            return await PlayAudioAsync(musicService, nextAudio, changeKind);
        }
        
        #endregion
        
        #region PlayMusicInPlaylist Patch
        
        /// <summary>
        /// 拦截点击播放列表播放
        /// 将选中的歌曲设为当前播放，并更新播放指针
        /// </summary>
        [HarmonyPatch(typeof(MusicService), nameof(MusicService.PlayMusicInPlaylist))]
        [HarmonyPrefix]
        public static bool PlayMusicInPlaylist_Prefix(MusicService __instance, int index, ref bool __result)
        {
            if (!IsQueueSystemEnabled)
                return true;
            
            __result = PlayFromPlaylistWithQueue(__instance, index);
            return false;
        }
        
        private static bool PlayFromPlaylistWithQueue(MusicService musicService, int index)
        {
            var currentPlaylist = musicService.CurrentPlayList;
            
            if (currentPlaylist.Count == 0 || index < 0 || index >= currentPlaylist.Count)
            {
                return false;
            }
            
            var audio = currentPlaylist[index];
            var audioClip = audio.AudioClip;
            
            if (audio == null || audioClip == null)
            {
                Plugin.Log.LogError($"[PlayQueuePatch] Invalid audio at index {index}");
                return false;
            }
            
            // 更新队列管理器
            var queueManager = PlayQueueManager.Instance;
            queueManager.SetCurrentPlaying(audio, updatePosition: true, newPosition: (index + 1) % currentPlaylist.Count);
            
            // 停止当前播放
            var playingMusic = musicService.PlayingMusic;
            if (playingMusic != null)
            {
                SingletonMonoBehaviour<MusicManager>.Instance.Stop(playingMusic.AudioClip);
            }
            
            // 加载并播放
            if (audioClip.loadState != AudioDataLoadState.Loaded)
            {
                audioClip.LoadAudioData();
            }
            
            SingletonMonoBehaviour<MusicManager>.Instance.Play(
                audioClip, 1f, 0f, 1f,
                musicService.IsRepeatOneMusic,
                true, "",
                () => musicService.SkipCurrentMusic(MusicChangeKind.Auto).Forget<bool>()
            );
            
            // 更新 MusicService 状态（使用反射，因为 PlayingMusic 是私有 setter）
            SetPlayingMusic(musicService, audio);
            
            // 触发事件
            InvokeOnChangeMusic(musicService, MusicChangeKind.Manual);
            InvokeOnPlayMusic(musicService, audio);
            
            return true;
        }
        
        #endregion
        
        #region PlayArugumentMusic Patch
        
        /// <summary>
        /// 拦截直接播放指定歌曲
        /// </summary>
        [HarmonyPatch(typeof(MusicService), nameof(MusicService.PlayArugumentMusic))]
        [HarmonyPrefix]
        public static bool PlayArugumentMusic_Prefix(MusicService __instance, GameAudioInfo audioInfo, MusicChangeKind changeKind)
        {
            if (!IsQueueSystemEnabled)
                return true;
            
            var audioClip = audioInfo?.AudioClip;
            if (audioInfo == null || audioClip == null)
            {
                Plugin.Log.LogError("[PlayQueuePatch] Invalid audioInfo in PlayArugumentMusic");
                return true;  // 让原始方法处理错误
            }
            
            var queueManager = PlayQueueManager.Instance;
            
            // 检查是否在队列视图中点击了队列中的项目
            if (MusicUI_VirtualScroll_Patch.IsShowingQueue)
            {
                int queueIndex = queueManager.IndexOf(audioInfo);
                if (queueIndex >= 0)
                {
                    // 在队列中找到了这个项目
                    if (queueIndex == 0)
                    {
                        // 已经是正在播放的，不需要处理
                        Plugin.Log.LogInfo("[PlayQueuePatch] Already playing this song");
                    }
                    else
                    {
                        // 移动到队列第一位，丢弃当前播放的
                        // 先移除点击的项目
                        queueManager.Remove(audioInfo);
                        // 移除当前播放的（队列第一个）
                        if (queueManager.Queue.Count > 0)
                        {
                            queueManager.RemoveAt(0);
                        }
                        // 把点击的项目设为当前播放
                        queueManager.SetCurrentPlaying(audioInfo);
                        
                        Plugin.Log.LogInfo($"[PlayQueuePatch] Moved queue item to play: {audioInfo.AudioClipName}");
                    }
                    
                    // 继续执行原始方法播放
                    return true;
                }
            }
            
            // 普通播放：更新队列管理器
            queueManager.SetCurrentPlaying(audioInfo);
            queueManager.SetPlaylistPositionByAudio(audioInfo, __instance.CurrentPlayList);
            
            return true;  // 继续执行原始方法
        }
        
        #endregion
        
        #region Helper Methods
        
        /// <summary>
        /// 播放指定歌曲
        /// </summary>
        private static async UniTask<bool> PlayAudioAsync(MusicService musicService, GameAudioInfo audio, MusicChangeKind changeKind)
        {
            if (audio == null)
            {
                Plugin.Log.LogWarning("[PlayQueuePatch] Audio is null");
                return false;
            }
            
            // 获取 AudioClip（可能需要异步加载）
            AudioClip audioClip;
            try
            {
                audioClip = await audio.GetAudioClip(CancellationToken.None);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"[PlayQueuePatch] Failed to load audio clip: {ex.Message}");
                return false;
            }
            
            if (audioClip == null)
            {
                Plugin.Log.LogWarning($"[PlayQueuePatch] AudioClip is null for {audio.AudioClipName}");
                return false;
            }
            
            // 停止当前播放
            var playingMusic = musicService.PlayingMusic;
            if (playingMusic != null)
            {
                SingletonMonoBehaviour<MusicManager>.Instance.Stop(playingMusic.AudioClip);
            }
            
            // 加载并播放
            if (audioClip.loadState != AudioDataLoadState.Loaded)
            {
                audioClip.LoadAudioData();
            }
            
            SingletonMonoBehaviour<MusicManager>.Instance.Play(
                audioClip, 1f, 0f, 1f,
                musicService.IsRepeatOneMusic,
                true, "",
                () => musicService.SkipCurrentMusic(MusicChangeKind.Auto).Forget<bool>()
            );
            
            // 更新 MusicService 状态
            SetPlayingMusic(musicService, audio);
            
            // 触发事件
            InvokeOnChangeMusic(musicService, changeKind);
            InvokeOnPlayMusic(musicService, audio);
            
            Plugin.Log.LogInfo($"[PlayQueuePatch] Playing: {audio.AudioClipName}");
            return true;
        }
        
        /// <summary>
        /// 设置 PlayingMusic（Publicize 后可直接访问 private setter）
        /// </summary>
        private static void SetPlayingMusic(MusicService musicService, GameAudioInfo audio)
        {
            musicService.PlayingMusic = audio;
        }
        
        /// <summary>
        /// 触发 OnChangeMusic 事件（Publicize 后可直接访问 private 字段）
        /// </summary>
        private static void InvokeOnChangeMusic(MusicService musicService, MusicChangeKind kind)
        {
            musicService.onChangeMusic.OnNext(kind);
        }
        
        /// <summary>
        /// 触发 OnPlayMusic 事件（Publicize 后可直接访问 private 字段）
        /// </summary>
        private static void InvokeOnPlayMusic(MusicService musicService, GameAudioInfo audio)
        {
            musicService.onPlayMusic.OnNext(audio);
        }
        
        #endregion
        
        #region Public API for UI
        
        /// <summary>
        /// 添加歌曲到队列（供 UI 调用）
        /// </summary>
        public static void AddToQueue(GameAudioInfo audio)
        {
            PlayQueueManager.Instance.Enqueue(audio);
        }
        
        /// <summary>
        /// 添加歌曲为下一首播放（供 UI 调用）
        /// </summary>
        public static void PlayNext(GameAudioInfo audio)
        {
            PlayQueueManager.Instance.InsertNext(audio);
        }
        
        /// <summary>
        /// 从队列移除歌曲（供 UI 调用）
        /// </summary>
        public static void RemoveFromQueue(GameAudioInfo audio)
        {
            PlayQueueManager.Instance.Remove(audio);
        }
        
        /// <summary>
        /// 从队列移除指定索引的歌曲（供 UI 调用）
        /// </summary>
        public static void RemoveFromQueueAt(int index)
        {
            PlayQueueManager.Instance.RemoveAt(index);
        }
        
        /// <summary>
        /// 重排序队列（供拖放 UI 调用）
        /// </summary>
        public static void ReorderQueue(int fromIndex, int toIndex)
        {
            PlayQueueManager.Instance.Move(fromIndex, toIndex);
        }
        
        /// <summary>
        /// 清空队列（保留当前播放）
        /// </summary>
        public static void ClearQueue()
        {
            PlayQueueManager.Instance.ClearPending();
        }
        
        /// <summary>
        /// 获取队列内容
        /// </summary>
        public static IReadOnlyList<GameAudioInfo> GetQueue()
        {
            return PlayQueueManager.Instance.Queue;
        }
        
        /// <summary>
        /// 直接设置播放指定歌曲（不走 SkipCurrentMusic 逻辑）
        /// 用于移除当前播放歌曲后，直接播放新的队首
        /// </summary>
        public static void SetPlayingMusicDirect(MusicService musicService, GameAudioInfo audio, MusicChangeKind kind)
        {
            if (musicService == null || audio == null) return;
            
            // 获取 AudioClip
            var audioClip = audio.AudioClip;
            if (audioClip == null)
            {
                Plugin.Log.LogWarning($"[SetPlayingMusicDirect] AudioClip is null for {audio.AudioClipName}");
                return;
            }
            
            // 停止当前播放
            var playingMusic = musicService.PlayingMusic;
            if (playingMusic?.AudioClip != null)
            {
                SingletonMonoBehaviour<MusicManager>.Instance.Stop(playingMusic.AudioClip);
            }
            
            // 加载并播放
            if (audioClip.loadState != AudioDataLoadState.Loaded)
            {
                audioClip.LoadAudioData();
            }
            
            SingletonMonoBehaviour<MusicManager>.Instance.Play(
                audioClip, 1f, 0f, 1f,
                musicService.IsRepeatOneMusic,
                true, "",
                () => musicService.SkipCurrentMusic(MusicChangeKind.Auto).Forget<bool>()
            );
            
            // 更新 MusicService 状态
            SetPlayingMusic(musicService, audio);
            
            // 触发事件
            InvokeOnChangeMusic(musicService, kind);
            InvokeOnPlayMusic(musicService, audio);
            
            Plugin.Log.LogInfo($"[SetPlayingMusicDirect] Now playing: {audio.AudioClipName}");
        }
        
        #endregion
    }
}
