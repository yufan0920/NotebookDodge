using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using System.Collections;

public class MainMenuUI : AudioSettingsMenu
{
    [SerializeField] private TMP_Text bestScoreText;
    [SerializeField] private GameObject firstSelectedButton;
    [Header("Settings")]
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private GameObject settingsFirstSelectedButton;
    [SerializeField, Min(0)] private float panelFadeSeconds = 0.14f;
    private bool leaving;
    private bool switchingPanel;

    private void Start()
    {
        Time.timeScale = 1;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (mainMenuPanel != null) mainMenuPanel.SetActive(true);
        RefreshVolumeControls();
        if (bestScoreText != null) bestScoreText.text = "BEST  " + GameOverUI.BestScore;
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
    }

    private void Update()
    {
        // Start without a highlight; establish keyboard/gamepad focus only on navigation input.
        EventSystem events = EventSystem.current;
        if (leaving || switchingPanel || events == null || events.currentSelectedGameObject != null ||
            mainMenuPanel == null || !mainMenuPanel.activeInHierarchy) return;
        var input = events.currentInputModule as InputSystemUIInputModule;
        if (input != null && input.move != null && input.move.action != null &&
            input.move.action.ReadValue<Vector2>().sqrMagnitude > 0.25f)
            Select(firstSelectedButton);
    }

    public void OpenSettings()
    {
        if (leaving || switchingPanel || settingsPanel == null) return;
        RefreshVolumeControls();
        StartCoroutine(SwitchPanel(mainMenuPanel, settingsPanel, settingsFirstSelectedButton));
    }

    public void CloseSettings()
    {
        if (leaving || switchingPanel) return;
        PlayerPrefs.Save();
        StartCoroutine(SwitchPanel(settingsPanel, mainMenuPanel, null));
    }

    private IEnumerator SwitchPanel(GameObject from, GameObject to, GameObject focus)
    {
        switchingPanel = true;
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        yield return UiTransition.Panel(from, false, panelFadeSeconds);
        yield return UiTransition.Panel(to, true, panelFadeSeconds);
        switchingPanel = false;
        Select(focus);
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        switchingPanel = false;
        UiTransition.ResetPanel(mainMenuPanel);
        UiTransition.ResetPanel(settingsPanel);
    }

    private static void Select(GameObject target)
    {
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(target);
    }

    public void Play()
    {
        if (leaving || switchingPanel) return;
        leaving = true;
        if (!SceneNavigation.Load(SceneNavigation.GameplayPath, this)) leaving = false;
    }

    public void Quit()
    {
        if (leaving || switchingPanel) return;
        leaving = true;
        SceneNavigation.Quit();
    }
}
