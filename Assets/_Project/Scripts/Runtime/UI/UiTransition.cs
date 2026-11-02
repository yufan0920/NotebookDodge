using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Panel opacity is temporary: authored Image colors/opacity and transforms are untouched.
[AddComponentMenu("")]
public sealed class UiTransition : MonoBehaviour
{
    private static UiTransition current;
    private CanvasGroup screenFade;
    private bool loading;
    public static bool IsLoading => current != null && current.loading;

    public static float Ease(float progress)
    {
        progress = Mathf.Clamp01(progress);
        return progress * progress * (3 - 2 * progress);
    }

    private static IEnumerator Fade(CanvasGroup group, float target, float duration)
    {
        if (group == null) yield break;
        float from = group.alpha;
        float elapsed = 0;
        while (elapsed < duration && group != null)
        {
            elapsed += Time.unscaledDeltaTime;
            group.alpha = Mathf.Lerp(from, target, Ease(duration <= 0 ? 1 : elapsed / duration));
            yield return null;
        }
        if (group != null) group.alpha = target;
    }

    public static IEnumerator Panel(GameObject panel, bool show, float duration)
    {
        if (panel == null) yield break;
        CanvasGroup group = panel.GetComponent<CanvasGroup>();
        if (group == null) group = panel.AddComponent<CanvasGroup>();
        group.interactable = group.blocksRaycasts = false;
        if (show)
        {
            group.alpha = 0;
            panel.SetActive(true);
        }
        yield return Fade(group, show ? 1 : 0, duration);
        if (panel != null)
        {
            if (!show) panel.SetActive(false);
            ResetPanel(panel);
        }
    }

    public static void ResetPanel(GameObject panel)
    {
        if (panel == null) return;
        CanvasGroup group = panel.GetComponent<CanvasGroup>();
        if (group == null) return;
        group.alpha = 1;
        group.interactable = group.blocksRaycasts = true;
    }

    public static bool LoadScene(int index)
    {
        if (current != null && current.loading) return false;
        if (current == null)
        {
            var root = new GameObject("Screen Transition", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasGroup), typeof(GraphicRaycaster));
            root.layer = 5;
            DontDestroyOnLoad(root);
            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32760;
            current = root.AddComponent<UiTransition>();
            current.screenFade = root.GetComponent<CanvasGroup>();
            current.screenFade.alpha = 0;
            var overlay = new GameObject("Fade", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            overlay.layer = 5;
            overlay.transform.SetParent(root.transform, false);
            var rect = (RectTransform)overlay.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            overlay.GetComponent<Image>().color = new Color(0.14f, 0.12f, 0.11f, 1);
        }
        current.loading = true;
        current.gameObject.SetActive(true);
        current.StartCoroutine(current.ChangeScene(index));
        return true;
    }

    private IEnumerator ChangeScene(int index)
    {
        screenFade.blocksRaycasts = true;
        yield return Fade(screenFade, 1, 0.13f);
        AsyncOperation operation = SceneManager.LoadSceneAsync(index, LoadSceneMode.Single);
        if (operation != null)
            while (!operation.isDone) yield return null;
        Time.timeScale = 1;
        // Allow the new scene's Start/layout calls to finish under the opaque overlay.
        yield return null;
        yield return Fade(screenFade, 0, 0.18f);
        screenFade.blocksRaycasts = false;
        loading = false;
        gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (current == this) current = null;
    }
}
