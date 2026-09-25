using UnityEngine;

/// <summary>
/// A water source the player can drink during the thirst step.
/// isSafe = sealed/covered water (recommended); isSafe = false is ash-contaminated tap water.
/// Registering a choice reports to ObjectiveManager; the unsafe option shows a reaction
/// and any choice advances the story (the game never blocks on the choice).
/// </summary>
public class WaterChoice : Interactable
{
    public bool isSafe = true;
    public string itemName = "Sealed Bottled Water";

    public bool IsDrunk { get; protected set; }

    protected override void OnStart()
    {
        base.OnStart();
        prompt = "drink " + itemName;
    }

    public override bool CanInteract
    {
        get
        {
            if (!canInteract) return false;
            ObjectiveManager om = ObjectiveManager.Instance;
            if (om == null) return false;
            if (!om.waterStepActive) return false;
            if (om.waterChoiceMade) return false;
            return true;
        }
    }

    public override void Interact(GameObject interactor)
    {
        if (IsDrunk) return;
        IsDrunk = true;

        ObjectiveManager om = ObjectiveManager.Instance;
        if (om != null) om.RegisterWaterChoice(isSafe);

        if (isSafe)
        {
            if (InteractionUI.Instance != null)
                InteractionUI.Instance.ShowToast(itemName + " is clean and safe to drink.");
            if (SubtitleUI.Instance != null)
                SubtitleUI.Instance.Show("Sealed bottled water is safe to drink.", 3f);
        }
        else
        {
            if (InteractionUI.Instance != null)
                InteractionUI.Instance.ShowToast("Cough! That water tastes of ash...");
            if (SubtitleUI.Instance != null)
                SubtitleUI.Instance.Show("Cough! Ash can contaminate water. Sealed water is safer.", 3f);

            // Brief exposure reaction: camera shake + sound if a clip is available.
            VolcanoEventManager vem = VolcanoEventManager.Instance;
            if (vem != null && vem.cameraShake != null)
                vem.cameraShake.Shake(0.25f, vem.cameraShake.strength * 0.3f);
            AudioSource src = GetComponent<AudioSource>();
            if (src != null && src.clip != null) src.Play();
        }

        InvokeInteractionComplete();
    }
}