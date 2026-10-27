using TMPro;
using UnityEngine;

// 挂在 Canvas 下的 TextMeshPro UI 文字上。
[RequireComponent(typeof(TextMeshProUGUI))]
public class SurvivalTimerUI : MonoBehaviour
{
    [SerializeField] private PlayerHealth player;
    private TextMeshProUGUI label;
    private AttackTimelineController timeline;
    public float ElapsedSeconds { get; private set; }
    private bool finished;
    private bool started;
    private int displayedSecond = -1;

    private void Awake() => label = GetComponent<TextMeshProUGUI>();

    private void Start()
    {
        if (player == null)
        {
            Debug.LogError("Assign Player on SurvivalTimerUI.", this);
            enabled = false;
            return;
        }
        // Start 时所有 Awake 已完成，玩家生命值已经初始化。
        // Only use the controller belonging to this scene, not another additive scene.
        foreach (var candidate in FindObjectsOfType<AttackTimelineController>())
        {
            if (candidate.isActiveAndEnabled && candidate.gameObject.scene == gameObject.scene)
            {
                timeline = candidate;
                break;
            }
        }
        ElapsedSeconds = 0f;
        finished = player.IsDead;
        started = true;
        player.HealthChanged += OnHealthChanged;
        RefreshLabel();
    }

    private void Update()
    {
        if (player == null || finished) return;
        // 每帧累加经过的游戏时间。timeScale=0 或编辑器暂停时不会继续计时。
        // 普通受伤、无敌期间仍然累计；死亡后永久停在本局成绩。
        if (player.IsDead)
        {
            RefreshLabel();
            finished = true;
            return;
        }
        ElapsedSeconds += Time.deltaTime;
        RefreshLabel();
    }

    private void OnHealthChanged(int current, int maximum)
    {
        // 在扣血事件发生时立即停表，避免之后的 Update 再累计一帧。
        if (current <= 0)
        {
            RefreshLabel();
            finished = true;
        }
    }

    private void RefreshLabel()
    {
        // The HUD follows absolute test time; scoring still uses actual survival time.
        float displaySeconds = timeline != null && timeline.HasStarted ? timeline.Elapsed : ElapsedSeconds;
        int totalSeconds = Mathf.FloorToInt(displaySeconds);
        if (totalSeconds == displayedSecond) return;
        displayedSecond = totalSeconds;
        int minutes = totalSeconds / 60;
        int seconds = totalSeconds % 60;
        // D2 表示至少显示两位数，例如 3 秒显示为 03。
        label.text = $"TIME {minutes:D2}:{seconds:D2}";
    }

    private void OnEnable()
    {
        // 首次启用由 Start 订阅；之后重新启用时恢复订阅，且不重置成绩。
        if (started && player != null)
        {
            player.HealthChanged += OnHealthChanged;
            if (player.IsDead) finished = true;
        }
    }

    private void OnDisable()
    {
        if (player != null) player.HealthChanged -= OnHealthChanged;
    }
}
