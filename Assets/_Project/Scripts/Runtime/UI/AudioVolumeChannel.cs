using UnityEngine;

// Put this on a music/effect AudioSource to apply the user's volume multiplier.
[RequireComponent(typeof(AudioSource))]
public class AudioVolumeChannel : MonoBehaviour
{
    public enum Channel { Effects, Music }
    [SerializeField] private AudioSource source;
    [SerializeField] private Channel channel;
    private float originalVolume;

    private void Awake()
    {
        if (source == null) source = GetComponent<AudioSource>();
        originalVolume = source.volume;
    }

    private void OnEnable()
    {
        AudioPreferences.Changed += ApplyVolume;
        ApplyVolume();
    }

    private void ApplyVolume()
    {
        if (source != null)
            source.volume = originalVolume * (channel == Channel.Music
                ? AudioPreferences.BgmVolume : AudioPreferences.SfxVolume);
    }

    public void Configure(AudioSource target, Channel value)
    {
        if (source != null) source.volume = originalVolume;
        source = target;
        channel = value;
        originalVolume = source != null ? source.volume : 1;
        ApplyVolume();
    }

    private void OnDisable()
    {
        AudioPreferences.Changed -= ApplyVolume;
        if (source != null) source.volume = originalVolume;
    }
}
