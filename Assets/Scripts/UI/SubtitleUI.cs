using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class SubtitleUI : MonoBehaviour
{
    public static SubtitleUI Instance { get; private set; }

    Text subtitleText;
    Coroutine currentRoutine;

    void Awake()
    {
        Instance = this;
        Build();
        subtitleText.gameObject.SetActive(false);
    }

    void Build()
    {
        RectTransform canvasRt = (RectTransform)UICanvasBootstrap.EnsureCanvas().transform;
        subtitleText = UICanvasBootstrap.MakeText(canvasRt, "Subtitle Text",
            new Vector2(0f, -420f), new Vector2(1700f, 220f), 40, TextAnchor.MiddleCenter,
            new Color(1f, 0.92f, 0.55f));
    }

    public void Show(string message, float holdSeconds)
    {
        if (subtitleText == null) return;
        if (currentRoutine != null) StopCoroutine(currentRoutine);
        currentRoutine = StartCoroutine(SubtitleRoutine(message, holdSeconds));
    }

    public void Hide()
    {
        if (currentRoutine != null)
        {
            StopCoroutine(currentRoutine);
            currentRoutine = null;
        }
        if (subtitleText != null)
            subtitleText.gameObject.SetActive(false);
    }

    IEnumerator SubtitleRoutine(string message, float holdSeconds)
    {
        subtitleText.text = message;
        subtitleText.gameObject.SetActive(true);

        float t = 0f;
        while (t < 0.5f)
        {
            t += Time.deltaTime;
            subtitleText.color = new Color(1f, 0.92f, 0.55f, Mathf.Lerp(0f, 1f, Mathf.Clamp01(t / 0.5f)));
            yield return null;
        }

        yield return new WaitForSeconds(holdSeconds);

        t = 0f;
        while (t < 0.6f)
        {
            t += Time.deltaTime;
            Color c = subtitleText.color;
            c.a = Mathf.Lerp(1f, 0f, Mathf.Clamp01(t / 0.6f));
            subtitleText.color = c;
            yield return null;
        }

        subtitleText.gameObject.SetActive(false);
        currentRoutine = null;
    }
}