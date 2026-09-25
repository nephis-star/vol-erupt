using UnityEngine;

public class GameSpawnManager : MonoBehaviour
{
    [Tooltip("Optional. Defaults to this object. The existing player to teleport.")]
    public Transform playerToSpawn;
    [Tooltip("Optional. Defaults to the GameObject named 'SpawnPoint' (normally under TemporaryHome).")]
    public Transform spawnPoint;
    [Tooltip("Extra vertical offset applied on top of the SpawnPoint position (e.g. to lift the player above a mattress).")]
    public float spawnVerticalOffset;
    public bool spawnOnStart = true;

    void Start()
    {
        if (spawnOnStart) SpawnPlayer();
    }

    public void SpawnPlayer()
    {
        if (playerToSpawn == null)
            playerToSpawn = transform;
        if (spawnPoint == null)
        {
            GameObject sp = GameObject.Find("SpawnPoint");
            if (sp != null) spawnPoint = sp.transform;
        }

        if (playerToSpawn == null || spawnPoint == null)
        {
            Debug.LogWarning("GameSpawnManager: spawnPoint or player missing.");
            return;
        }

        Vector3 spawnPos = spawnPoint.position + Vector3.up * spawnVerticalOffset;
        playerToSpawn.position = spawnPos;
        playerToSpawn.rotation = spawnPoint.rotation;

        Rigidbody rb = playerToSpawn.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.MovePosition(spawnPos);
            rb.MoveRotation(spawnPoint.rotation);
        }

        if (GameStateManager.Instance != null)
            GameStateManager.Instance.SetState(GameState.Exploration);
    }
}
