using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Bulbul.Achievements;
using NestopiSystem.Steam;
using FastEnumUtility;

namespace ChillPatcher.Patches
{
    /// <summary>
    /// 成就同步器
    /// 在游戏启动时检测Steam是否在线，如果在线则同步缓存的成就
    /// </summary>
    public class AchievementSyncManager : MonoBehaviour
    {
        private static AchievementSyncManager _instance;
        private bool _syncAttempted = false;

        // 缓存基础目录（与AchievementCacheManager保持一致）
        private static readonly string CacheBaseDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData).Replace("Local", "LocalLow"),
            "Nestopi",
            "Chill With You",
            "ChillPatcherCache"
        );

        public static void Initialize()
        {
            if (_instance == null)
            {
                GameObject go = new GameObject("AchievementSyncManager");
                DontDestroyOnLoad(go);
                _instance = go.AddComponent<AchievementSyncManager>();
                Plugin.Logger.LogInfo("[AchievementSync] 成就同步管理器已初始化");
            }
        }

        private void Awake()
        {
            // 使用 C# 原生的 Timer，不依赖Unity的Update循环
            var timer = new System.Threading.Timer(_ =>
            {
                try
                {
                    CheckAndSync();
                }
                catch (Exception ex)
                {
                    Plugin.Logger.LogError($"[AchievementSync] 同步检查出错: {ex.Message}");
                }
            }, null, 5000, System.Threading.Timeout.Infinite); // 5000ms = 5秒，只执行一次
        }
        
        private void CheckAndSync()
        {
            if (!_syncAttempted)
            {
                _syncAttempted = true;
                
                if (!PluginConfig.EnableAchievementCache.Value)
                {
                    return;
                }
                
                TrySyncCachedAchievements();
            }
        }



        /// <summary>
        /// 尝试同步缓存的成就到Steam
        /// 直接使用Steamworks API，不依赖SteamManager实例
        /// </summary>
        public void TrySyncCachedAchievements()
        {
            try
            {
                // 获取当前用户的Steam ID
                string currentUserId = GetCurrentSteamUserId();
                
                if (string.IsNullOrEmpty(currentUserId))
                {
                    Plugin.Logger.LogWarning("[AchievementSync] 无法获取当前Steam用户ID");
                    return;
                }

                Plugin.Logger.LogInfo($"[AchievementSync] 当前Steam用户ID: {currentUserId}");

                if (!AchievementCacheManager.HasPendingAchievements(currentUserId))
                {
                    Plugin.Logger.LogInfo($"[AchievementSync] 用户 {currentUserId} 没有待同步的成就");
                    return;
                }

                Plugin.Logger.LogInfo($"[AchievementSync] 检测到用户 {currentUserId} 的待同步成就，开始同步...");
                Plugin.Logger.LogInfo(AchievementCacheManager.GetCacheInfo(currentUserId));

                // 检查Steam是否可用
                if (!Steamworks.SteamAPI.IsSteamRunning())
                {
                    Plugin.Logger.LogWarning("[AchievementSync] Steam未运行，10秒后重试");
                    
                    // 使用 Timer 延迟重试
                    var retryTimer = new System.Threading.Timer(_ => RetrySync(), 
                        null, 10000, System.Threading.Timeout.Infinite);
                    
                    return;
                }

                // 获取缓存的成就
                var cachedAchievements = AchievementCacheManager.GetCachedAchievements(currentUserId);
                int syncedCount = 0;
                int failedCount = 0;

                // 直接使用Steamworks API同步每个成就
                foreach (var kvp in cachedAchievements)
                {
                    try
                    {
                        if (Enum.TryParse<AchievementCategory>(kvp.Key, out var category))
                        {
                            // 使用FastEnumUtility.ToName()获取Steam API使用的成就名称
                            // 这与游戏原本的SteamAchievements类使用的方法一致
                            string achievementName = category.ToName();
                            
                            // 使用Steamworks API设置成就进度
                            bool success = Steamworks.SteamUserStats.SetStat(achievementName, kvp.Value);
                            
                            if (success)
                            {
                                Plugin.Logger.LogInfo($"[AchievementSync] 同步成功: {achievementName} = {kvp.Value}");
                                syncedCount++;
                            }
                            else
                            {
                                Plugin.Logger.LogWarning($"[AchievementSync] 同步失败: {achievementName}");
                                failedCount++;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Plugin.Logger.LogWarning($"[AchievementSync] 同步成就 {kvp.Key} 时出错: {ex.Message}");
                        failedCount++;
                    }
                }

                // 提交所有更改到Steam
                if (syncedCount > 0)
                {
                    bool stored = Steamworks.SteamUserStats.StoreStats();
                    if (stored)
                    {
                        Plugin.Logger.LogInfo($"[AchievementSync] Steam Stats 已提交");
                    }
                    else
                    {
                        Plugin.Logger.LogWarning($"[AchievementSync] Steam Stats 提交失败");
                    }
                }

                // 不清空缓存，保留作为成就系统的备份
                // Steam会自动处理重复的成就推送
                Plugin.Logger.LogInfo($"[AchievementSync] 用户 {currentUserId} 成就同步完成");
                Plugin.Logger.LogInfo($"  - 成功: {syncedCount} 个");
                Plugin.Logger.LogInfo($"  - 失败: {failedCount} 个");
                Plugin.Logger.LogInfo($"  - 缓存保留作为备份，位置: {CacheBaseDirectory}\\{currentUserId}");
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"[AchievementSync] 同步过程发生错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 重试同步
        /// </summary>
        private void RetrySync()
        {
            string currentUserId = GetCurrentSteamUserId();
            if (!string.IsNullOrEmpty(currentUserId) && AchievementCacheManager.HasPendingAchievements(currentUserId))
            {
                Plugin.Logger.LogInfo("[AchievementSync] 重试同步缓存的成就...");
                TrySyncCachedAchievements();
            }
        }

        /// <summary>
        /// 获取当前Steam用户ID
        /// </summary>
        private string GetCurrentSteamUserId()
        {
            try
            {
                var steamId = Steamworks.SteamUser.GetSteamID();
                if (steamId.IsValid())
                {
                    return steamId.ToString();
                }
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogWarning($"[AchievementSync] 无法获取Steam用户ID: {ex.Message}");
            }
            
            return null;
        }

        /// <summary>
        /// 手动触发同步（可以从UI或命令调用）
        /// </summary>
        public static void ManualSync()
        {
            if (_instance != null)
            {
                _instance.TrySyncCachedAchievements();
            }
            else
            {
                Plugin.Logger.LogWarning("[AchievementSync] 同步管理器未初始化");
            }
        }
    }
}
