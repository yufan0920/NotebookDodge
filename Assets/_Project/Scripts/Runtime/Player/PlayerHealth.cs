using System;
using System.Collections;
using UnityEngine;

public class PlayerHealth : MonoBehaviour
{
    [SerializeField, Min(1)] private int maxHealth = 5;
    [Header("Hit Feedback")]
    [SerializeField, Min(0f)] private float invincibilityDuration = 0.5f;
    [SerializeField, Min(0f)] private float redFlashDuration = 0.15f;
    [SerializeField] private Color hitColor = new Color(1f, 0.3f, 0.3f, 1f);
    [SerializeField, Min(0.01f)] private float blinkInterval = 0.08f;
    [SerializeField, Range(0f, 1f)] private float blinkAlpha = 0.35f;
    [Header("Hit Audio (Optional)")]
    [SerializeField] private AudioClip hurtClip;
    [SerializeField, Range(0f, 1f)] private float hurtVolume = 0.7f;
    private float invincibleUntil;
    private float nextGroundDamageAt;
    private bool inBurnArea;
    private Rigidbody2D body;
    private SpriteRenderer[] visuals;
    private Color[] originalColors;
    private AudioSource hurtSource;
    private Coroutine feedbackRoutine;
    public int CurrentHealth { get; private set; }
    public int MaxHealth => maxHealth;
    public bool IsDead => CurrentHealth <= 0;
    // 比较游戏时间，不依赖闪烁次数。暂停游戏时间时，无敌计时也会暂停。
    public bool IsInvincible => Time.time < invincibleUntil;
    public event Action<int, int> HealthChanged;

    private void Awake()
    {
        maxHealth = Mathf.Max(1, maxHealth);
        CurrentHealth = maxHealth;
        body = GetComponent<Rigidbody2D>();
        // 自动找到 Player 下面的角色图片，保存原色，结束反馈后恢复。
        visuals = GetComponentsInChildren<SpriteRenderer>(true);
        originalColors = new Color[visuals.Length];
        for (int i = 0; i < visuals.Length; i++) originalColors[i] = visuals[i].color;
        // 只有指定音频时才创建专用音源，避免改变其他 AudioSource 的设置。
        if (hurtClip != null)
        {
            hurtSource = gameObject.AddComponent<AudioSource>();
            hurtSource.playOnAwake = false;
            hurtSource.spatialBlend = 0f; // 2D 声音，不因摄像机距离而变小。
            hurtSource.loop = false;
            gameObject.AddComponent<AudioVolumeChannel>().Configure(hurtSource, AudioVolumeChannel.Channel.Effects);
        }
    }

    public void TakeDamage(int amount)
    {
        ApplyDamage(amount, false);
    }

    private void FixedUpdate()
    {
        if (IsDead) return;
        int amount;
        float interval;
        Vector2 position = body != null ? body.position : (Vector2)transform.position;
        if (!GroundArea.TryGetBurn(this, position, out amount, out interval))
        {
            inBurnArea = false;
            return;
        }
        if ((!inBurnArea || Time.time >= nextGroundDamageAt) && TakeGroundDamage(amount))
        {
            inBurnArea = true;
            nextGroundDamageAt = Time.time + interval;
        }
    }

    public bool TakeGroundDamage(int amount)
    {
        return ApplyDamage(amount, true);
    }

    private bool ApplyDamage(int amount, bool groundDamage)
    {
        // 无敌期间忽略新伤害；子弹是否消失由各自的攻击脚本决定。
        if (IsDead || IsInvincible || amount <= 0) return false;
        // 先开启保护，保证同一物理步里多颗子弹命中也只扣一次。
        if (!groundDamage) invincibleUntil = Time.time + invincibilityDuration;
        CurrentHealth = Mathf.Max(0, CurrentHealth - amount);
        if (feedbackRoutine != null) StopCoroutine(feedbackRoutine);
        RestoreColors();
        feedbackRoutine = StartCoroutine(PlayHitFeedback(groundDamage));
        // 只有实际接受伤害才播放一次声音，无敌期碰撞不会反复播放。
        if (hurtSource != null && hurtClip != null) hurtSource.PlayOneShot(hurtClip, hurtVolume);
        HealthChanged?.Invoke(CurrentHealth, maxHealth);
        Debug.Log($"HP: {CurrentHealth}/{maxHealth}", this);
        if (IsDead) Debug.Log("Game Over. Stop Play mode and Play again to restart.", this);
        return true;
    }

    private IEnumerator PlayHitFeedback(bool groundDamage)
    {
        float hitStarted = Time.time;
        // 非致命命中时，红闪不能超出保护期；致命命中仍展示一次短促红闪。
        float redDuration = IsDead || groundDamage ? redFlashDuration : Mathf.Min(redFlashDuration, invincibilityDuration);
        float feedbackUntil = IsDead || groundDamage ? hitStarted + redDuration : invincibleUntil;
        while (Time.time < feedbackUntil)
        {
            float elapsed = Time.time - hitStarted;
            bool red = elapsed < redDuration;
            bool dim = !red && Mathf.FloorToInt((elapsed - redDuration) / Mathf.Max(0.01f, blinkInterval)) % 2 == 0;
            for (int i = 0; i < visuals.Length; i++)
            {
                if (visuals[i] == null) continue;
                Color color = red ? hitColor : originalColors[i];
                color.a = originalColors[i].a * (dim ? blinkAlpha : 1f);
                visuals[i].color = color;
            }
            // 让出执行权，下一帧继续；这不是阻塞游戏的等待。
            yield return null;
        }
        RestoreColors();
        feedbackRoutine = null;
    }

    private void RestoreColors()
    {
        if (visuals == null) return;
        for (int i = 0; i < visuals.Length; i++)
            if (visuals[i] != null) visuals[i].color = originalColors[i];
    }

    private void OnDisable()
    {
        // 停用组件时取消视觉反馈，避免角色永久停在红色或半透明状态。
        if (feedbackRoutine != null) StopCoroutine(feedbackRoutine);
        feedbackRoutine = null;
        RestoreColors();
    }
}
