using UnityEngine;

public class EvacuationItem : MonoBehaviour
{
    [Tooltip("Must match the equipment entry name so the evacuation checklist can tick it off.")]
    public string itemName = "Emergency Bag";

    void Awake()
    {
        if (ObjectiveManager.Instance != null)
            ObjectiveManager.Instance.SetEvacuationBagId(itemName);
    }
}