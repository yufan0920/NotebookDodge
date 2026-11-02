using System;
using UnityEngine;

public static class AudioPreferences
{
    private const string BgmKey = "NotebookDodge.BgmVolume.v1";
    private const string SfxKey = "NotebookDodge.SfxVolume.v1";
    public static float BgmVolume => Mathf.Clamp01(PlayerPrefs.GetFloat(BgmKey, 1));
    public static float SfxVolume => Mathf.Clamp01(PlayerPrefs.GetFloat(SfxKey, 1));
    public static event Action Changed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetListeners() => Changed = null;

    public static void SetBgmVolume(float value)
    {
        PlayerPrefs.SetFloat(BgmKey, Mathf.Clamp01(value));
        Changed?.Invoke();
    }

    public static void SetSfxVolume(float value)
    {
        PlayerPrefs.SetFloat(SfxKey, Mathf.Clamp01(value));
        Changed?.Invoke();
    }
}
