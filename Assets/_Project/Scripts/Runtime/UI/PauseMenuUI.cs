using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

[DefaultExecutionOrder(-100)]
public class PauseMenuUI : AudioSettingsMenu
{
    [SerializeField] private PlayerHealth player;
    [SerializeField] private GameObject pausePanel;
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private GameObject resumeButton;
    [SerializeField] private GameObject settingsFirstSelected;
    [SerializeField, Min(0)] private float fadeSeconds = 0.14f;
    private bool paused, settingsOpen, switching, leaving;
    private float previousTimeScale;
    private bool previousAudioPause, previousCursorVisible;
    private CursorLockMode previousCursorLock;
    public bool IsPaused => paused;

    private void Start()
    {
        if (pausePanel != null) pausePanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(false);
    }

    private void OnEnable()
    {
        if (player != null) player.HealthChanged += OnHealthChanged;
    }

    private void Update()
    {
        if (leaving || switching || UiTransition.IsLoading || player == null || player.IsDead) return;
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            if (!paused) Pause();
            else if (settingsOpen) CloseSettings();
            else Resume();
            return;
        }
        EventSystem events = EventSystem.current;
        if (!paused || events == null || events.currentSelectedGameObject != null) return;
        var input = events.currentInputModule as InputSystemUIInputModule;
        if (input != null && input.move != null && input.move.action != null &&
            input.move.action.ReadValue<Vector2>().sqrMagnitude > 0.25f)
            Select(settingsOpen ? settingsFirstSelected : resumeButton);
    }

    public void Pause()
    {
        if (paused || leaving || switching || pausePanel == null || player == null || player.IsDead) return;
        previousTimeScale = Time.timeScale;
        previousAudioPause = AudioListener.pause;
        previousCursorVisible = Cursor.visible;
        previousCursorLock = Cursor.lockState;
        paused = true;
        Time.timeScale = 0;
        AudioListener.pause = true;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        StartCoroutine(ShowPause());
    }

    private IEnumerator ShowPause()
    {
        switching = true;
        Select(null);
        yield return UiTransition.Panel(pausePanel, true, fadeSeconds);
        switching = false;
    }

    public void Resume()
    {
        if (!paused || switching || leaving) return;
        StartCoroutine(ResumeAfterFade());
    }

    private IEnumerator ResumeAfterFade()
    {
        switching = true;
        Select(null);
        yield return UiTransition.Panel(settingsOpen ? settingsPanel : pausePanel, false, fadeSeconds);
        RestoreGame();
        switching = false;
    }

    public void Restart()
    {
        if (!paused || switching || leaving) return;
        leaving = true;
        if (!SceneNavigation.Load(gameObject.scene.path, this)) leaving = false;
    }

    public void OpenSettings()
    {
        if (!paused || switching || leaving || settingsPanel == null) return;
        RefreshVolumeControls();
        StartCoroutine(SwitchPanels(true));
    }

    public void CloseSettings()
    {
        if (!paused || !settingsOpen || switching || leaving) return;
        PlayerPrefs.Save();
        StartCoroutine(SwitchPanels(false));
    }

    private IEnumerator SwitchPanels(bool open)
    {
        switching = true;
        Select(null);
        yield return UiTransition.Panel(open ? pausePanel : settingsPanel, false, fadeSeconds);
        settingsOpen = open;
        yield return UiTransition.Panel(open ? settingsPanel : pausePanel, true, fadeSeconds);
        switching = false;
        // Mouse users return without a sticky selected ribbon; keyboard users can navigate again.
        Select(null);
    }

    private static void Select(GameObject target)
    {
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(target);
    }

    private void RestoreGame()
    {
        if (!paused) return;
        Time.timeScale = previousTimeScale;
        AudioListener.pause = previousAudioPause;
        Cursor.lockState = previousCursorLock;
        Cursor.visible = previousCursorVisible;
        paused = settingsOpen = false;
    }

    private void OnHealthChanged(int current, int maximum)
    {
        if (current > 0 || !paused) return;
        StopAllCoroutines();
        if (pausePanel != null) pausePanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(false);
        RestoreGame();
        switching = false;
    }

    private void OnDisable()
    {
        if (player != null) player.HealthChanged -= OnHealthChanged;
        StopAllCoroutines();
        if (pausePanel != null) pausePanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(false);
        RestoreGame();
        switching = false;
        UiTransition.ResetPanel(pausePanel);
        UiTransition.ResetPanel(settingsPanel);
    }
}
