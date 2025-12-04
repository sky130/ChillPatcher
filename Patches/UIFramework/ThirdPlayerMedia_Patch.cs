using Bulbul;
using ChillPatcher.UIFramework.Music;
using ChillPatcher.UIFramework.Music.ThirdPlayer;
using HarmonyLib;

namespace ChillPatcher.Patches.UIFramework
{
    [HarmonyPatch]
    public static class ThirdPlayerMedia_Patch
    {
        public static FacilityMusic FacilityMusic;

        [HarmonyPatch(typeof(FacilityMusic), "Setup")]
        [HarmonyPostfix]
        static void Setup_Postfix(FacilityMusic __instance)
        {
            FacilityMusic = __instance;
            if (UIFrameworkConfig.EnableThirdPlayerMediaTransportControls.Value)
            {
                PlayQueuePatch.IsQueueSystemEnabled = false;
                MusicUIController.Init();
                ThirdPlayerController.Init();
                Plugin.Logger.LogInfo("[ThirdPlayer] ThirdPlayerMedia Enabled");
            }
        }

        /// <summary>
        /// Harmony补丁 - 拦截内置播放器播放
        /// </summary>
        [HarmonyPatch(typeof(FacilityMusic), "PlayMusic")]
        [HarmonyPrefix]
        static bool PlayMusic_Prefix(int index)
        {
            if (UIFrameworkConfig.EnableThirdPlayerMediaTransportControls.Value)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Harmony补丁 - 拦截内置播放器播放
        /// </summary>
        [HarmonyPatch(typeof(FacilityMusic), "PlayShuffle")]
        [HarmonyPrefix]
        static bool PlayShuffle_Prefix()
        {
            if (UIFrameworkConfig.EnableThirdPlayerMediaTransportControls.Value)
            {
                Plugin.Logger.LogInfo("[ThirdPlayer] PlayShuffle_Prefix");
                return false;
            }

            return true;
        }


        /// <summary>
        /// Harmony补丁 - 拦截内置播放器播放
        /// </summary>
        [HarmonyPatch(typeof(FacilityMusic), "UnPauseMusic")]
        [HarmonyPrefix]
        static bool UnPauseMusic_Prefix()
        {
            if (UIFrameworkConfig.EnableThirdPlayerMediaTransportControls.Value)
            {
                FacilityMusic._musicUI.OnPlayMusic();
                // TODO 控制外部
                return false;
            }

            return true;
        }

        /// <summary>
        /// Harmony补丁 - 拦截内置播放器播放
        /// </summary>
        [HarmonyPatch(typeof(FacilityMusic), "PauseMusic")]
        [HarmonyPrefix]
        static bool PauseMusic_Prefix()
        {
            if (UIFrameworkConfig.EnableThirdPlayerMediaTransportControls.Value)
            {
                FacilityMusic._musicUI.OnPauseMusic();
                // TODO 控制外部
                return false;
            }

            return true;
        }
        
        /// <summary>
        /// Harmony补丁 - 拦截内置播放器播放
        /// </summary>
        [HarmonyPatch(typeof(FacilityMusic), "UpdateFacility")]
        [HarmonyPrefix]
        static bool UpdateFacility_Prefix()
        {
            return !UIFrameworkConfig.EnableThirdPlayerMediaTransportControls.Value;
        }
    }
}