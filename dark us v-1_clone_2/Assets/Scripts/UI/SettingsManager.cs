using UnityEngine;

public static class SettingsManager
{
    public const string ScreenModeKey = "setting_screen_mode";
    public const string FpsLimitKey = "setting_fps_limit";
    public const string LanguageKey = "setting_language";
    public const string MasterVolumeKey = "setting_master_volume";
    public const string BgmVolumeKey = "setting_bgm_volume";
    public const string SfxVolumeKey = "setting_sfx_volume";
    public const string VoiceVolumeKey = "setting_voice_volume";
    public const string MouseXKey = "setting_mouse_x";
    public const string MouseYKey = "setting_mouse_y";
    public const string HudOpacityKey = "setting_hud_opacity";

    public static readonly int[] FpsLimits = { 30, 60, 120, 144, -1 };

    public static FullScreenMode ScreenMode
    {
        get
        {
            int raw = PlayerPrefs.GetInt(ScreenModeKey, (int)FullScreenMode.FullScreenWindow);
            if (!System.Enum.IsDefined(typeof(FullScreenMode), raw))
            {
                return FullScreenMode.FullScreenWindow;
            }

            FullScreenMode mode = (FullScreenMode)raw;
            return mode == FullScreenMode.Windowed ? FullScreenMode.Windowed : FullScreenMode.FullScreenWindow;
        }
        set => PlayerPrefs.SetInt(ScreenModeKey, (int)value);
    }

    public static int FpsLimit
    {
        get => PlayerPrefs.GetInt(FpsLimitKey, 120);
        set => PlayerPrefs.SetInt(FpsLimitKey, value);
    }

    public static int Language
    {
        get => Mathf.Clamp(PlayerPrefs.GetInt(LanguageKey, 0), 0, 2);
        set => PlayerPrefs.SetInt(LanguageKey, Mathf.Clamp(value, 0, 2));
    }

    public static float MasterVolume
    {
        get => PlayerPrefs.GetFloat(MasterVolumeKey, 1f);
        set => PlayerPrefs.SetFloat(MasterVolumeKey, Mathf.Clamp01(value));
    }

    public static float BgmVolume
    {
        get => PlayerPrefs.GetFloat(BgmVolumeKey, 0.65f);
        set => PlayerPrefs.SetFloat(BgmVolumeKey, Mathf.Clamp01(value));
    }

    public static float SfxVolume
    {
        get => PlayerPrefs.GetFloat(SfxVolumeKey, 0.55f);
        set => PlayerPrefs.SetFloat(SfxVolumeKey, Mathf.Clamp01(value));
    }

    public static float VoiceVolume
    {
        get => PlayerPrefs.GetFloat(VoiceVolumeKey, 1f);
        set => PlayerPrefs.SetFloat(VoiceVolumeKey, Mathf.Clamp01(value));
    }

    public static float MouseSensitivityX
    {
        get => PlayerPrefs.GetFloat(MouseXKey, 1f);
        set => PlayerPrefs.SetFloat(MouseXKey, Mathf.Clamp(value, 0.1f, 5f));
    }

    public static float MouseSensitivityY
    {
        get => PlayerPrefs.GetFloat(MouseYKey, 1f);
        set => PlayerPrefs.SetFloat(MouseYKey, Mathf.Clamp(value, 0.1f, 5f));
    }

    public static float HudOpacity
    {
        get => PlayerPrefs.GetFloat(HudOpacityKey, 1f);
        set => PlayerPrefs.SetFloat(HudOpacityKey, Mathf.Clamp(value, 0.45f, 1f));
    }

    public static void Apply()
    {
        Resolution resolution = Screen.currentResolution;
        Screen.SetResolution(resolution.width, resolution.height, ScreenMode, resolution.refreshRate);
        Application.targetFrameRate = FpsLimit;
        ApplyAudio();
        ApplyLowGraphics();
        PlayerPrefs.Save();
    }

    public static void ApplyAudio()
    {
        AudioListener.volume = MasterVolume;
        PlayerVoiceChat.ApplySavedVoiceVolumeToAll();
        GameAudioManager.ApplyVolumes();
    }

    public static void ResetAll()
    {
        PlayerPrefs.DeleteKey(ScreenModeKey);
        PlayerPrefs.DeleteKey(FpsLimitKey);
        PlayerPrefs.DeleteKey(LanguageKey);
        PlayerPrefs.DeleteKey(MasterVolumeKey);
        PlayerPrefs.DeleteKey(BgmVolumeKey);
        PlayerPrefs.DeleteKey(SfxVolumeKey);
        PlayerPrefs.DeleteKey(VoiceVolumeKey);
        PlayerPrefs.DeleteKey(MouseXKey);
        PlayerPrefs.DeleteKey(MouseYKey);
        PlayerPrefs.DeleteKey(HudOpacityKey);
        GameInputBindings.ResetAll();
        PlayerPrefs.Save();
        Apply();
    }

    public static void ApplyLowGraphics()
    {
        QualitySettings.globalTextureMipmapLimit = 3;
        QualitySettings.shadows = ShadowQuality.Disable;
        QualitySettings.shadowResolution = ShadowResolution.Low;
        QualitySettings.antiAliasing = 0;
        QualitySettings.vSyncCount = 0;
    }
}
