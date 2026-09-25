using System.Collections;
using UnityEngine;

public class DrinkBottledWater : PreparationTask
{
    bool drinkDone;

    protected override void OnStart()
    {
        if (ObjectiveManager.Instance != null)
            ObjectiveManager.Instance.RegisterSafetyStep("drinkwater", "Drink bottled water");
    }

    public override string InteractionPrompt
    {
        get
        {
            if (drinkDone) return "";
            if (!BottledWater.HasBottle) return "collect bottled water first";
            return "drink bottled water";
        }
    }

    public override bool CanInteract
    {
        get { return canInteract && !drinkDone && BottledWater.HasBottle && ObjectiveManager.Instance != null && ObjectiveManager.Instance.equipmentEnabled; }
    }

    public override void Interact(GameObject interactor)
    {
        if (drinkDone || !BottledWater.HasBottle) return;
        drinkDone = true;

        if (ObjectiveManager.Instance != null)
            ObjectiveManager.Instance.CompleteSafetyStep("drinkwater");

        if (InteractionUI.Instance != null)
            InteractionUI.Instance.ShowToast("Drink safe, protected water.");

        if (SubtitleUI.Instance != null)
            SubtitleUI.Instance.Show(
                "Use bottled or properly protected water.\nAsh contamination can affect water supplies.",
                5f);

        StartCoroutine(DrinkRoutine());
        InvokeInteractionComplete();
    }

    IEnumerator DrinkRoutine()
    {
        float t = 0f;
        while (t < 1.2f)
        {
            t += Time.deltaTime;
            yield return null;
        }
    }
}