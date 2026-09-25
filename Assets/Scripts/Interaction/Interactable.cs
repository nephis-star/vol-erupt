using UnityEngine;

public class Interactable : MonoBehaviour, IInteractable
{
    [Tooltip("Action text shown after 'Press E to'. Example: 'pick up Water'")]
    public string prompt = "interact";
    public bool canInteract = true;
    protected bool started;

    protected virtual void OnStart()
    {
        started = true;
    }

    public virtual string InteractionPrompt
    {
        get { return prompt; }
    }

    public virtual bool CanInteract
    {
        get { return canInteract && started; }
    }

    public virtual void Interact(GameObject interactor)
    {
    }

    public event System.Action OnInteracted;
    protected void InvokeInteractionComplete()
    {
        if (OnInteracted != null) OnInteracted();
    }
}