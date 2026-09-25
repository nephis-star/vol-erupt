using UnityEngine;

/// <summary>
/// Trigger zone just outside the house. Once the player walks out during evacuation,
/// it marks the exit reached so ObjectiveManager can complete the game.
/// </summary>
public class EvacuationExitZone : MonoBehaviour
{
    void OnTriggerEnter(Collider other)
    {
        GameObject player = GameObject.Find("First Person Controller");
        if (player == null) return;
        if (!other.transform.IsChildOf(player.transform)) return;

        ObjectiveManager om = ObjectiveManager.Instance;
        if (om == null) return;
        if (!om.evacuationPrepared) return;

        om.MarkExitReached();
    }
}