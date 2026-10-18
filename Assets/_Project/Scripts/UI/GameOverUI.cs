using TMPro;
using UnityEngine;

// 挂在始终启用的 Canvas 上，不要挂在开局隐藏的 GameOverPanel 上。
public class GameOverUI : MonoBehaviour
{
    [SerializeField] private PlayerHealth player;
    [SerializeField] private SurvivalTimerUI survivalTimer;
    [SerializeField] private GameObject gameOverPanel;
    [SerializeField] private TextMeshProUGUI scoreText;
    [SerializeField] private TextMeshProUGUI bestScoreText;
    // 独立的新纪录文字，位置、字号和颜色由 Inspector 控制。
    [SerializeField] private TextMeshProUGUI newRecordText;

    // 固定的存档键名。以后改变计分规则时可换版本号，避免混用旧成绩。
    private const string BestScoreKey = "NotebookDodge.BestSurvivalScore.v1";
    private bool initialized;
    private bool resultShown;

    private void Start()
    {
        if (player == null || survivalTimer == null || gameOverPanel == null ||
            scoreText == null || bestScoreText == null)
        {
            Debug.LogError("Assign all references on GameOverUI.", this);
            enabled = false;
            return;
        }
        if (transform == gameOverPanel.transform || transform.IsChildOf(gameOverPanel.transform))
        {
            Debug.LogError("Place GameOverUI on Canvas, outside GameOverPanel.", this);
            enabled = false;
            return;
        }
        gameOverPanel.SetActive(false);
        if (newRecordText != null) newRecordText.gameObject.SetActive(false);
        initialized = true;
        player.HealthChanged += OnHealthChanged;
        if (player.IsDead) ShowResult();
    }

    private void OnHealthChanged(int current, int maximum)
    {
        if (current <= 0) ShowResult();
    }

    private void ShowResult()
    {
        // 结算只执行一次，防止重复显示或写入存档。
        if (resultShown) return;
        resultShown = true;
        // Score actual survival time, excluding the skipped part of a range test.
        int score = Mathf.Max(0, Mathf.FloorToInt(survivalTimer.ElapsedSeconds));
        int previousBest = Mathf.Max(0, PlayerPrefs.GetInt(BestScoreKey, 0));
        bool newRecord = score > previousBest;
        int best = Mathf.Max(score, previousBest);
        if (newRecord)
        {
            // PlayerPrefs 保存在本机，下次启动仍可读取；不是 GitHub 或云端存档。
            PlayerPrefs.SetInt(BestScoreKey, best);
            PlayerPrefs.Save();
        }
        scoreText.text = $"SCORE  {score}";
        bestScoreText.text = $"BEST  {best}";
        if (newRecordText != null)
        {
            newRecordText.text = "NEW RECORD!";
            newRecordText.gameObject.SetActive(newRecord);
        }
        gameOverPanel.SetActive(true);
    }

    private void OnEnable()
    {
        if (!initialized || player == null) return;
        player.HealthChanged += OnHealthChanged;
        if (player.IsDead) ShowResult();
    }

    private void OnDisable()
    {
        if (player != null) player.HealthChanged -= OnHealthChanged;
    }
}
