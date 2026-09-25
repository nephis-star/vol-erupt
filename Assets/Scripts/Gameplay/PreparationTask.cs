using System.Collections;
using UnityEngine;

public class PreparationTask : Interactable
{
    public string taskId = "window";
    public string displayName = "Window";

    [Tooltip("Transform that pivots when closing (hinge).")]
    public Transform hinge;
    public Vector3 openRotation = new Vector3(0f, 0f, 0f);
    public Vector3 closedRotation = new Vector3(0f, 0f, 0f);
    public float closeDuration = 1.2f;
    public AudioSource audioSource;

    public bool IsClosed { get; protected set; }

    void Start()
    {
        OnStart();
    }

    protected override void OnStart()
    {
        base.OnStart();
        if (ObjectiveManager.Instance != null)
            ObjectiveManager.Instance.RegisterPreparation(taskId, displayName, "Close " + displayName);
    }

    public override string InteractionPrompt
    {
        get { return IsClosed ? "" : "close " + displayName; }
    }

    public override bool CanInteract
    {
        get { return canInteract && started && !IsClosed; }
    }

    public override void Interact(GameObject interactor)
    {
        if (IsClosed) return;
        StartCoroutine(CloseRoutine());

        if (ObjectiveManager.Instance != null)
            ObjectiveManager.Instance.CompleteTask(taskId);

        if (InteractionUI.Instance != null)
            InteractionUI.Instance.ShowToast(displayName + " secured.");

        if (audioSource != null && audioSource.clip != null)
            audioSource.Play();

        InvokeInteractionComplete();
    }

    IEnumerator CloseRoutine()
    {
        float t = 0f;
        while (t < closeDuration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / closeDuration);
            if (hinge != null)
                hinge.localEulerAngles = Vector3.LerpUnclamped(openRotation, closedRotation, k);
            yield return null;
        }
        if (hinge != null)
            hinge.localEulerAngles = closedRotation;
        IsClosed = true;
    }

    public bool IsOpened { get; set; }

    /// <summary>
    /// Re-opens an already closed preparation (used to open the Main Door for evacuation).
    /// </summary>
    public virtual void Reopen()
    {
        if (!IsClosed) return;
        if (gameObject.activeInHierarchy)
            StartCoroutine(OpenRoutine());
        else
        {
            if (hinge != null) hinge.localEulerAngles = openRotation;
            IsClosed = false;
        }
    }

    IEnumerator OpenRoutine()
    {
        float t = 0f;
        while (t < closeDuration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / closeDuration);
            if (hinge != null)
                hinge.localEulerAngles = Vector3.LerpUnclamped(closedRotation, openRotation, k);
            yield return null;
        }
        if (hinge != null)
            hinge.localEulerAngles = openRotation;
        IsClosed = false;
        IsOpened = true;
    }

    public static PreparationTask FindDoor()
    {
        PreparationTask[] all = FindObjectsByType<PreparationTask>(FindObjectsSortMode.None);
        foreach (PreparationTask t in all)
        {
            if (t.taskId == "door") return t;
        }
        return null;
    }
}