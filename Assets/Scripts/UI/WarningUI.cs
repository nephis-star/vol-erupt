using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class WarningUI : MonoBehaviour
{
    public static WarningUI Instance { get; private set; }

    Image overlay;
    Text titleText;
    Text bodyText;

    void Awake()
    {
        Instance = this;
        Build();
        SetScreenVisible(false);
    }

    void Build()
    {
        RectTransform canvasRt = (RectTransform)UICanvasBootstrap.EnsureCanvas().transform;

        overlay = UICanvasBootstrap.MakeStretchedImage(canvasRt, "Warning Overlay", new Color(0f, 0f, 0f, 0f));

        titleText = UICanvasBootstrap.MakeText(canvasRt, "Warning Title",
            new Vector2(0f, 240f), new Vector2(1600f, 120f), 64, TextAnchor.MiddleCenter,
            new Color(1f, 0.3f, 0.2f));

        bodyText = UICanvasBootstrap.MakeText(canvasRt, "Warning Body",
            new Vector2(0f, -40f), new Vector2(1600f, 640f), 46, TextAnchor.MiddleCenter, Color.white);
    }

    public void PlayTVAnnouncement(System.Action onComplete)
    {
        PlayTVAnnouncement(onComplete, "VOLCANIC ERUPTION WARNING",
            "Residents are advised to remain calm\nand prepare for volcanic ashfall.\n\nStay indoors when ashfall occurs.");
    }

    public void PlayTVAnnouncement(System.Action onComplete, string title, string body)
    {
        PlayTVAnnouncement(onComplete, title, body, body);
    }

    public void PlayTVAnnouncement(System.Action onComplete, string title, string body, string subtitle)
    {
        StartCoroutine(TVSequence(onComplete, title, body, subtitle));
    }

    public void ShowWarning(string title, string body, float holdSeconds)
    {
        StartCoroutine(WarningSequence(title, body, holdSeconds));
    }

    /// <summary>
    /// Runs a warning to completion. Yields the fade-in / hold / fade-out sequence so callers
    /// (e.g. the volcano event sequence) can await it.
    /// </summary>
    public IEnumerator ShowWarningAwait(string title, string body, float holdSeconds)
    {
        yield return WarningSequence(title, body, holdSeconds);
    }

    IEnumerator TVSequence(System.Action onComplete, string title, string body, string subtitle)
    {
        SetScreenVisible(true);
        overlay.color = new Color(0f, 0f, 0f, 0.98f);
        titleText.text = "";
        bodyText.text = "\n\n\n\n--- SIGNAL ---";

        float flickerEnd = Time.time + 1.4f;
        while (Time.time < flickerEnd)
        {
            float a = Random.value > 0.72f ? 0.15f : 0.95f;
            overlay.color = new Color(0f, 0f, 0f, a);
            yield return new WaitForSeconds(0.04f + Random.value * 0.12f);
        }
        overlay.color = new Color(0f, 0f, 0f, 0.96f);

        titleText.text = title;
        bodyText.text = body;

        if (SubtitleUI.Instance != null)
            SubtitleUI.Instance.Show(subtitle, 4.5f);
        yield return new WaitForSeconds(5f);

        yield return StartCoroutine(FadeOut(0.8f));
        SetScreenVisible(false);
        if (onComplete != null) onComplete();
    }

    IEnumerator WarningSequence(string title, string body, float holdSeconds)
    {
        SetScreenVisible(true);
        overlay.color = new Color(0f, 0f, 0f, 0f);
        titleText.text = title;
        bodyText.text = body;

        float t = 0f;
        while (t < 0.6f)
        {
            t += Time.deltaTime;
            overlay.color = new Color(0f, 0f, 0f, Mathf.Lerp(0f, 0.85f, 0.6f == 0f ? 1f : t / 0.6f));
            yield return null;
        }

        yield return new WaitForSeconds(holdSeconds);

        yield return StartCoroutine(FadeOut(0.8f));
        SetScreenVisible(false);
    }

    IEnumerator FadeOut(float duration)
    {
        float t = 0f;
        float start = overlay.color.a;
        while (t < duration)
        {
            t += Time.deltaTime;
            Color c = overlay.color;
            c.a = Mathf.Lerp(start, 0f, Mathf.Clamp01(t / duration));
            overlay.color = c;
            yield return null;
        }
        overlay.color = new Color(0f, 0f, 0f, 0f);
    }

    void SetScreenVisible(bool visible)
    {
        if (overlay != null) overlay.gameObject.SetActive(visible);
        if (titleText != null) titleText.gameObject.SetActive(visible);
        if (bodyText != null) bodyText.gameObject.SetActive(visible);
    }
}