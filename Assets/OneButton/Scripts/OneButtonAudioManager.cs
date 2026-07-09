using UnityEngine;

public sealed class OneButtonAudioManager : MonoBehaviour
{
    [Header("Audio Sources")]
    [SerializeField] private AudioSource bgmSource;
    [SerializeField] private AudioSource seSource;
    [SerializeField] private AudioSource heartbeatSlowSource;
    [SerializeField] private AudioSource heartbeatFastSource;
    [SerializeField] private AudioSource stepSource;
    [SerializeField] private AudioSource warningSource;
    [SerializeField] private AudioSource closeSource;

    [Header("Clips")]
    [SerializeField] private AudioClip bgmClip;
    [SerializeField] private AudioClip attackClip;
    [SerializeField] private AudioClip deadClip;
    [SerializeField] private AudioClip heartbeatSlowClip;
    [SerializeField] private AudioClip heartbeatFastClip;
    [SerializeField] private AudioClip stepClip;
    [SerializeField] private AudioClip warningClip;
    [SerializeField] private AudioClip closeClip;

    private void Awake()
    {
        ConfigureSource(bgmSource, bgmClip, true, 0.08f);
        ConfigureSource(seSource, null, false, 1f);
        ConfigureSource(heartbeatSlowSource, heartbeatSlowClip, true, 1.2f);
        ConfigureSource(heartbeatFastSource, heartbeatFastClip, true, 1.2f);
        ConfigureSource(stepSource, stepClip, true, 1.2f);
        ConfigureSource(warningSource, warningClip, true, 0.7f);
        ConfigureSource(closeSource, closeClip, true, 0.7f);
    }

    public void PlayBgm() => PlayLoop(bgmSource);

    public void PlayAttack() => PlayOneShot(attackClip, 1.5f);

    public void PlayDead() => PlayOneShot(deadClip, 2f);

    public void PlayHeartbeatSlow() => PlayLoop(heartbeatSlowSource);

    public void StopHeartbeatSlow() => StopLoop(heartbeatSlowSource);

    public void PlayHeartbeatFast() => PlayLoop(heartbeatFastSource);

    public void PlayStep() => PlayLoop(stepSource);

    public void PlayWarning() => PlayLoop(warningSource);

    public void PlayCloseEye() => PlayLoop(closeSource);

    public void StopChargeLoops()
    {
        StopLoop(heartbeatSlowSource);
        StopLoop(heartbeatFastSource);
        StopLoop(stepSource);
        StopLoop(warningSource);
        StopLoop(closeSource);
    }

    private void PlayOneShot(AudioClip clip, float volumeScale)
    {
        if (seSource != null && clip != null)
        {
            seSource.PlayOneShot(clip, volumeScale);
        }
    }

    private static void ConfigureSource(AudioSource source, AudioClip clip, bool loop, float volume)
    {
        if (source == null)
        {
            return;
        }

        source.playOnAwake = false;
        source.loop = loop;
        source.volume = volume;
        if (clip != null)
        {
            source.clip = clip;
        }
    }

    private static void PlayLoop(AudioSource source)
    {
        if (source != null && source.clip != null && !source.isPlaying)
        {
            source.Play();
        }
    }

    private static void StopLoop(AudioSource source)
    {
        if (source != null && source.isPlaying)
        {
            source.Stop();
        }
    }
}
