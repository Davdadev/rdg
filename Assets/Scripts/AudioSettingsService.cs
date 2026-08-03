using System;
using UnityEngine;

public static class AudioSettingsService
{
    public const string SfxVolumeKey = "SfxVolume";
    public const string MusicVolumeKey = "MusicVolume";
    public const string HapticsKey = "HapticsEnabled";

    public static event Action OnSettingsChanged;

    public static float SfxVolume
    {
        get => PlayerPrefs.GetFloat(SfxVolumeKey, 1f);
        set { PlayerPrefs.SetFloat(SfxVolumeKey, Mathf.Clamp01(value)); PlayerPrefs.Save(); OnSettingsChanged?.Invoke(); }
    }

    public static float MusicVolume
    {
        get => PlayerPrefs.GetFloat(MusicVolumeKey, 1f);
        set { PlayerPrefs.SetFloat(MusicVolumeKey, Mathf.Clamp01(value)); PlayerPrefs.Save(); OnSettingsChanged?.Invoke(); }
    }

    public static bool HapticsEnabled
    {
        get => PlayerPrefs.GetInt(HapticsKey, 1) == 1;
        set { PlayerPrefs.SetInt(HapticsKey, value ? 1 : 0); PlayerPrefs.Save(); OnSettingsChanged?.Invoke(); }
    }
}
