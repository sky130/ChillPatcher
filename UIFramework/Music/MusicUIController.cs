using ChillPatcher.Patches.UIFramework;

namespace ChillPatcher.UIFramework.Music.ThirdPlayer;

public class MusicUIController
{
    public static MusicUI MusicUI;

    public static void Init()
    {
        MusicUI = ThirdPlayerMedia_Patch.FacilityMusic._musicUI;
    }

    public static void UpdatePlayback(int progress, bool isPlaying)
    {
        float i = progress;
        var value = i / 100f;
        MusicUI.musicProgressSlider.value = value;
        Plugin.Logger.LogInfo($"[MusicUIController] Setting progress is {value}.");

        if (isPlaying)
        {
            MusicUI.OnPlayMusic();
        }
        else
        {
            MusicUI.OnPauseMusic();
        }
    }

    public static void UpdateMusic(string title, string artist)
    {
        MusicUI._musicTitleText.text = title;
        MusicUI._artistNameText.text = artist;
    }
}