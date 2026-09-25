using UnityEngine;

public class NPCDialogue : Interactable
{
    [TextArea] public string dialogue = "Hello.";

    void Start() { OnStart(); }

    public override void Interact(GameObject interactor)
    {
        if (!CanInteract) return;
        InteractionUI.Instance.ShowToast(dialogue);
    }
}