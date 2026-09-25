using System.Collections;
using UnityEngine;

public class SealPrepVisual : MonoBehaviour
{
    public string taskId = "window1";
    [Tooltip("Optional second task that must also complete before this visual reveals (e.g. a tape-seal step). Leave empty to reveal on taskId alone.")]
    public string tapeTaskId = "";
    public GameObject[] barricadeObjects;
    public float revealDuration = 0.4f;

    public bool IsRevealed { get; private set; }

    void Start()
    {
        if (barricadeObjects != null)
        {
            foreach (GameObject go in barricadeObjects)
                if (go != null) go.SetActive(false);
        }
    }

    void Update()
    {
        if (IsRevealed) return;
        if (ObjectiveManager.Instance == null) return;

        if (!ObjectiveManager.Instance.IsPreparationDone(taskId)) return;
        if (!string.IsNullOrEmpty(tapeTaskId) && !ObjectiveManager.Instance.IsPreparationDone(tapeTaskId))
            return;

        Reveal();
    }

    public void Reveal()
    {
        if (IsRevealed) return;
        IsRevealed = true;

        if (barricadeObjects == null) return;
        StartCoroutine(RevealRoutine());
    }

    IEnumerator RevealRoutine()
    {
        foreach (GameObject go in barricadeObjects)
            if (go != null) go.SetActive(true);

        float t = 0f;
        while (t < revealDuration)
        {
            t += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / revealDuration));
            foreach (GameObject go in barricadeObjects)
            {
                if (go == null) continue;
                go.transform.localScale = Vector3.one * k;
            }
            yield return null;
        }

        foreach (GameObject go in barricadeObjects)
            if (go != null) go.transform.localScale = Vector3.one;
    }
}