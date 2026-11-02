using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Uses Button's pointer/keyboard/disabled states; the hit rectangle never scales.
[AddComponentMenu("UI/Ribbon Button")]
public class RibbonButton : Button
{
    [SerializeField] private Image artwork;
    [SerializeField] private TMP_Text label;
    [SerializeField] private RectTransform visualRoot;
    [SerializeField] private Sprite normalSprite;
    [SerializeField] private Sprite selectedSprite;
    [SerializeField] private Color selectedTextColor = Color.black;
    [SerializeField, Range(1, 1.1f)] private float hoverScale = 1.035f;
    [SerializeField, Range(0.85f, 1)] private float pressedScale = 0.97f;
    [SerializeField, Min(0)] private float transitionSeconds = 0.08f;
    private Vector3 baseScale, targetScale;
    private Color normalTextColor;
    private bool cached;

    protected override void OnEnable()
    {
        if (visualRoot != null)
        {
            baseScale = targetScale = visualRoot.localScale;
        }
        if (label != null) normalTextColor = label.color;
        cached = true;
        base.OnEnable();
    }

    protected override void DoStateTransition(SelectionState state, bool instant)
    {
        base.DoStateTransition(state, instant);
        // CanvasGroup changes can reach an inactive button before its first OnEnable.
        // Keep the authored colors until OnEnable has captured them.
        if (!cached) return;
        bool inverted = state == SelectionState.Highlighted || state == SelectionState.Selected ||
            state == SelectionState.Pressed;
        if (artwork != null)
        {
            artwork.sprite = inverted && selectedSprite != null ? selectedSprite : normalSprite;
            // A complete backdrop may supply the idle artwork; show an overlay only on selection.
            artwork.enabled = artwork.sprite != null;
        }
        if (label != null) label.color = inverted ? selectedTextColor : normalTextColor;
        if (!cached || visualRoot == null) return;
        Vector3 factor = state == SelectionState.Pressed
            ? new Vector3(hoverScale * pressedScale, hoverScale * pressedScale * 0.94f, 1)
            : Vector3.one * (inverted ? hoverScale : 1);
        targetScale = Vector3.Scale(baseScale, factor);
        if (instant || !Application.isPlaying || transitionSeconds <= 0) visualRoot.localScale = targetScale;
    }

    private void Update()
    {
        if (!Application.isPlaying || !cached || visualRoot == null) return;
        float blend = transitionSeconds <= 0 ? 1 : 1 - Mathf.Exp(-5 * Time.unscaledDeltaTime / transitionSeconds);
        visualRoot.localScale = Vector3.Lerp(visualRoot.localScale, targetScale, blend);
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        if (!cached) return;
        if (artwork != null)
        {
            artwork.sprite = normalSprite;
            artwork.enabled = normalSprite != null;
        }
        if (label != null) label.color = normalTextColor;
        if (cached && visualRoot != null) visualRoot.localScale = baseScale;
        cached = false;
    }
}
