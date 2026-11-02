using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Shared serialized field names preserve the existing main-menu controls and callbacks.
public abstract class AudioSettingsMenu : MonoBehaviour
{
    [SerializeField] protected Slider bgmSlider;
    [SerializeField] protected Slider sfxSlider;
    [SerializeField] protected TMP_Text bgmValueText;
    [SerializeField] protected TMP_Text sfxValueText;

    public void SetBgmVolume(float value)
    {
        AudioPreferences.SetBgmVolume(value);
        RefreshVolumeLabels();
    }

    public void SetSfxVolume(float value)
    {
        AudioPreferences.SetSfxVolume(value);
        RefreshVolumeLabels();
    }

    protected void RefreshVolumeControls()
    {
        if (bgmSlider != null) bgmSlider.SetValueWithoutNotify(AudioPreferences.BgmVolume);
        if (sfxSlider != null) sfxSlider.SetValueWithoutNotify(AudioPreferences.SfxVolume);
        RefreshVolumeLabels();
    }

    private void RefreshVolumeLabels()
    {
        if (bgmValueText != null) bgmValueText.text = Mathf.RoundToInt(AudioPreferences.BgmVolume * 100) + "%";
        if (sfxValueText != null) sfxValueText.text = Mathf.RoundToInt(AudioPreferences.SfxVolume * 100) + "%";
    }
}
