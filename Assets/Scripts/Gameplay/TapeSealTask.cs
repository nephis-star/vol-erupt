using UnityEngine;

public class TapeSealTask : PreparationTask
{
    [Tooltip("The close-task id that must complete before this tape step activates.")]
    public string requiredCloseTaskId = "window1";

    [Tooltip("Visual objects activated when this tape step completes.")]
    public GameObject[] tapeObjects;

    Collider tapeCollider;
    bool activated;

    protected override void OnStart()
    {
        started = true;
        tapeCollider = GetComponent<Collider>();
        if (tapeCollider != null) tapeCollider.enabled = false;

        if (tapeObjects != null)
        {
            foreach (GameObject go in tapeObjects)
                if (go != null) go.SetActive(false);
        }

        if (ObjectiveManager.Instance != null)
            ObjectiveManager.Instance.RegisterTapePreparation(taskId, displayName, "Seal " + displayName + " Gaps");
    }

    void Update()
    {
        if (activated) return;
        if (ObjectiveManager.Instance == null) return;
        if (!ObjectiveManager.Instance.IsPreparationDone(requiredCloseTaskId)) return;

        activated = true;
        if (tapeCollider != null) tapeCollider.enabled = true;
        ObjectiveManager.Instance.MarkTaskActive(taskId);
    }

    public override string InteractionPrompt
    {
        get
        {
            if (IsClosed) return "";
            if (ObjectiveManager.Instance != null && !ObjectiveManager.Instance.hasSealingTape)
                return "Find sealing tape first";
            return "Tape " + displayName + " gaps";
        }
    }

    public override bool CanInteract
    {
        get { return canInteract && started && activated && !IsClosed; }
    }

    public override void Interact(GameObject interactor)
    {
        if (!CanInteract) return;
        if (ObjectiveManager.Instance != null && !ObjectiveManager.Instance.hasSealingTape)
        {
            if (InteractionUI.Instance != null)
                InteractionUI.Instance.ShowToast("Find sealing tape first.");
            return;
        }
        IsClosed = true;

        if (tapeObjects != null)
        {
            foreach (GameObject go in tapeObjects)
                if (go != null) go.SetActive(true);
        }

        if (ObjectiveManager.Instance != null)
            ObjectiveManager.Instance.CompleteTask(taskId);

        if (InteractionUI.Instance != null)
            InteractionUI.Instance.ShowToast(displayName + " gaps sealed.");

        if (audioSource != null && audioSource.clip != null)
            audioSource.Play();

        InvokeInteractionComplete();
    }
}