using UnityEngine;

public class SealingTape : Interactable
{
    [Tooltip("The close task that must be done before the tape can be picked up.")]
    public string requiredCloseTaskId = "window1";

    bool collected;

    void Awake()
    {
        prompt = "Pick Up Tape";
    }

    void Start()
    {
        started = true;
    }

    public override bool CanInteract
    {
        get
        {
            if (!canInteract || collected) return false;
            if (ObjectiveManager.Instance == null) return false;
            if (!ObjectiveManager.Instance.IsPreparationDone(requiredCloseTaskId)) return false;
            return true;
        }
    }

    public override void Interact(GameObject interactor)
    {
        if (collected || !CanInteract) return;
        collected = true;
        canInteract = false;

        if (ObjectiveManager.Instance != null)
            ObjectiveManager.Instance.SetHasSealingTape();

        if (InteractionUI.Instance != null)
            InteractionUI.Instance.ShowToast("Sealing tape collected.");

        InvokeInteractionComplete();
        gameObject.SetActive(false);
    }
}