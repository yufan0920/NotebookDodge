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
    [SerializeField] private NewRecordConfetti newRecordConfetti;
    [SerializeField, Min(0)] private float resultFadeSeconds = 0.22f;

    // 固定的存档键名。以后改变计分规则时可换版本号，避免混用旧成绩。
    private const string BestScoreKey = "NotebookDodge.BestSurvivalScore.v1";
    public static int BestScore => Mathf.Max(0, PlayerPrefs.GetInt(BestScoreKey, 0));
    private bool initialized;
    private bool resultShown;
    private bool leaving;
    private Coroutine resultFade;

    public void TryAgain()
    {
        if (!resultShown || leaving) return;
        leaving = true;
        if (!SceneNavigation.Load(gameObject.scene.path, this)) leaving = false;
    }

    public void MainMenu()
    {
        if (!resultShown || leaving) return;
        leaving = true;
        if (!SceneNavigation.Load(SceneNavigation.MainMenuPath, this)) leaving = false;
    }

    public void Quit()
    {
        if (!resultShown || leaving) return;
        leaving = true;
        SceneNavigation.Quit();
    }

    [ContextMenu("Reset Best Score")]
    private void ResetBestScore()
    {
        PlayerPrefs.DeleteKey(BestScoreKey);
        PlayerPrefs.Save();
        if (bestScoreText != null) bestScoreText.text = "BEST  0";
        if (newRecordText != null) newRecordText.gameObject.SetActive(false);
        if (newRecordConfetti != null) newRecordConfetti.Stop();
        Debug.Log("Best score reset to 0.", this);
    }

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
        int previousBest = BestScore;
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
        resultFade = StartCoroutine(UiTransition.Panel(gameOverPanel, true, resultFadeSeconds));
        if (newRecordConfetti != null)
        {
            if (newRecord) newRecordConfetti.Play();
            else newRecordConfetti.Stop();
        }
    }

    private void OnEnable()
    {
        if (!initialized || player == null) return;
        player.HealthChanged += OnHealthChanged;
        if (player.IsDead) ShowResult();
    }

    private void OnDisable()
    {
        if (resultFade != null) StopCoroutine(resultFade);
        UiTransition.ResetPanel(gameOverPanel);
        if (player != null) player.HealthChanged -= OnHealthChanged;
        if (newRecordConfetti != null) newRecordConfetti.Stop();
    }
}
