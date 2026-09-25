using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class InteractionUI : MonoBehaviour
{
    public static InteractionUI Instance { get; private set; }

    Text promptText;
    Text toastText;
    Coroutine toastRoutine;

    void Awake()
    {
        Instance = this;
        Build();
    }

    void Build()
    {
        RectTransform canvasRt = (RectTransform)UICanvasBootstrap.EnsureCanvas().transform;

        promptText = UICanvasBootstrap.MakeText(canvasRt, "Interaction Prompt",
            new Vector2(0f, 140f), new Vector2(1400f, 64f), 40, TextAnchor.MiddleCenter, Color.white);

        toastText = UICanvasBootstrap.MakeText(canvasRt, "Interaction Toast",
            new Vector2(0f, 40f), new Vector2(1400f, 56f), 34, TextAnchor.MiddleCenter,
            new Color(1f, 0.85f, 0.3f));

        promptText.gameObject.SetActive(false);
        toastText.gameObject.SetActive(false);
    }

    public void ShowPrompt(string prompt)
    {
        if (promptText == null) return;
        if (string.IsNullOrEmpty(prompt))
        {
            promptText.gameObject.SetActive(false);
            return;
        }
        promptText.text = "Press E to " + prompt;
        promptText.gameObject.SetActive(true);
    }

    public void HidePrompt()
    {
        if (promptText != null) promptText.gameObject.SetActive(false);
    }

    public void ShowToast(string message)
    {
        if (toastText == null) return;
        toastText.text = message;
        toastText.gameObject.SetActive(true);
        if (toastRoutine != null) StopCoroutine(toastRoutine);
        toastRoutine = StartCoroutine(ToastRoutine());
    }

    IEnumerator ToastRoutine()
    {
        yield return new WaitForSeconds(2.4f);
        if (toastText != null) toastText.gameObject.SetActive(false);
    }
}