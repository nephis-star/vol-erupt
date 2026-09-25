using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

/// <summary>
/// Plays the evacuation video as a full-screen UI overlay the moment the player has all four
/// protective items equipped (Mask, Shades, T-shirt, Pants). When the video actually finishes,
/// the player is teleported to the evacuation zone. Additive to the existing flow — it does not
/// replace the objective system, only reacts to the authoritative equipped state.
/// </summary>
public class EvacuationSequence : MonoBehaviour
{
    [Tooltip("Video file name, resolved relative to the project's Assets folder.")]
    public string videoFileName = "Bulkan/evacuation.mp4";

    [Tooltip("Destination the player is teleported to after the video ends. Defaults to the GameObject named 'EvacuationSpawnPoint'.")]
    public Transform evacuationSpawnPoint;

    [Tooltip("Fallback: if the video has not reported completion within this many seconds, teleport anyway.")]
    public float videoTimeout = 180f;

    [Tooltip("Sorting order of the full-screen overlay (keeps it above normal gameplay UI).")]
    public int overlaySortOrder = 32767;

    bool evacuationSequenceStarted;
    bool videoEnded;
    VideoPlayer videoPlayer;
    RenderTexture renderTexture;
    GameObject overlayRoot;
    GameObject player;

    readonly List<Behaviour> disabledControls = new List<Behaviour>();

    void Start()
    {
        if (evacuationSpawnPoint == null)
        {
            GameObject sp = GameObject.Find("EvacuationSpawnPoint");
            if (sp != null) evacuationSpawnPoint = sp.transform;
        }
        if (InventoryManager.Instance != null)
            InventoryManager.Instance.Changed += OnInventoryChanged;
    }

    void OnDestroy()
    {
        if (InventoryManager.Instance != null)
            InventoryManager.Instance.Changed -= OnInventoryChanged;
    }

    void OnInventoryChanged()
    {
        if (evacuationSequenceStarted) return;
        if (!AllProtectiveEquipped()) return;
        evacuationSequenceStarted = true;
        StartCoroutine(PlayEvacuationSequence());
    }

    bool AllProtectiveEquipped()
    {
        InventoryManager inv = InventoryManager.Instance;
        if (inv == null) return false;
        return inv.IsEquipped("Mask")
            && inv.IsEquipped("Shades")
            && inv.IsEquipped("T-shirt")
            && inv.IsEquipped("Pants");
    }

    IEnumerator PlayEvacuationSequence()
    {
        yield return null;

        Debug.Log("EVAC DEBUG: sequence started");
        player = FindPlayer();
        if (player == null)
        {
            Debug.LogWarning("EvacuationSequence: player not found; cannot lock controls or teleport.");
        }
        else
        {
            Debug.Log("EVAC DEBUG: player found = " + player.name);
        }

        CaptureAndDisableControls(player);

        overlayRoot = new GameObject("EvacuationVideoOverlay");
        Canvas canvas = overlayRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = overlaySortOrder;
        CanvasScaler scaler = overlayRoot.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        GameObject bg = new GameObject("Background");
        bg.transform.SetParent(overlayRoot.transform, false);
        Image bgImage = bg.AddComponent<Image>();
        bgImage.color = Color.black;
        StretchToScreen(bgImage.rectTransform);

        GameObject videoSurface = new GameObject("VideoSurface");
        videoSurface.transform.SetParent(overlayRoot.transform, false);
        RawImage raw = videoSurface.AddComponent<RawImage>();
        StretchToScreen(raw.rectTransform);
        AspectRatioFitter af = videoSurface.AddComponent<AspectRatioFitter>();
        af.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        af.aspectRatio = 16f / 9f;

        renderTexture = new RenderTexture(1920, 1080, 0);
        renderTexture.Create();
        raw.texture = renderTexture;

        videoPlayer = overlayRoot.AddComponent<VideoPlayer>();
        videoPlayer.playOnAwake = false;
        videoPlayer.source = VideoSource.Url;
        videoPlayer.url = "file:///" + Application.dataPath + "/" + videoFileName;
        videoPlayer.renderMode = VideoRenderMode.RenderTexture;
        videoPlayer.targetTexture = renderTexture;
        videoPlayer.isLooping = false;
        videoPlayer.skipOnDrop = true;
        videoPlayer.loopPointReached += OnVideoEnd;

        videoPlayer.Prepare();
        float start = Time.time;
        while (!videoPlayer.isPrepared && Time.time - start < 10f)
            yield return null;

        if (videoPlayer.isPrepared)
        {
            uint w = videoPlayer.width;
            uint h = videoPlayer.height;
            if (w > 0 && h > 0) af.aspectRatio = w / (float)h;
            else af.aspectRatio = 16f / 9f;
        }

        videoPlayer.Play();
        Debug.Log("EVAC DEBUG: video Play called");

        // Wait for the actual end-of-video event (not the start).
        float timeout = Time.time + videoTimeout;
        while (!videoEnded && Time.time < timeout)
            yield return null;

        Debug.Log("EVAC DEBUG: FinishSequence entered");
        FinishSequence();
    }

    void OnVideoEnd(VideoPlayer vp)
    {
        // Loop point reached = the video actually finished.
        Debug.Log("EVAC DEBUG: loopPointReached FIRED");
        videoEnded = true;
    }

    void FinishSequence()
    {
        if (videoPlayer != null) videoPlayer.loopPointReached -= OnVideoEnd;
        if (overlayRoot != null) Destroy(overlayRoot);
        if (renderTexture != null)
        {
            renderTexture.Release();
            renderTexture = null;
        }

        if (player == null) player = FindPlayer();

        if (evacuationSpawnPoint == null)
        {
            Debug.LogWarning("EvacuationSequence: no evacuationSpawnPoint; skipping teleport.");
        }
        else if (player != null)
        {
            TeleportPlayer(player);
        }

        RestoreControls(player);
    }

    void TeleportPlayer(GameObject player)
    {
        if (player == null) player = FindPlayer();
        if (player == null)
        {
            Debug.LogError("EVAC DEBUG: player found = NULL; cannot teleport.");
            return;
        }
        Debug.Log("EVAC DEBUG: player found = " + player.name);

        Rigidbody rb = player.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        Vector3 target = evacuationSpawnPoint.position;

        RaycastHit hit;
        float floorY = evacuationSpawnPoint.position.y;
        if (Physics.Raycast(target + Vector3.up * 2f, Vector3.down, out hit, 6f, ~0, QueryTriggerInteraction.Ignore))
            floorY = hit.point.y;

        target.y = floorY + 0.9f;

        Debug.Log("EVAC DEBUG: teleport target = " + target.ToString("F3"));

        // Teleport the Rigidbody itself (physics body), not just the Transform, then sync the
        // Transform so the movement controller sees the same position. Velocities stay zeroed.
        if (rb != null)
        {
            rb.position = target;
            rb.rotation = evacuationSpawnPoint.rotation;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
        player.transform.position = target;
        player.transform.rotation = evacuationSpawnPoint.rotation;

        Debug.Log("EVAC DEBUG: player position after teleport = " + player.transform.position.ToString("F3"));
        Debug.Log("EVAC DEBUG: evacuation teleport COMPLETE");
    }

    // Reliable player lookup that finds the ACTIVE existing First Person Controller (or an
    // inactive one momentarily hidden by the open inventory UI) without creating another player.
    GameObject FindPlayer()
    {
        GameObject go = GameObject.Find("First Person Controller");
        if (go != null) return go;

        GameObject[] all = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (GameObject g in all)
        {
            if (g == null || g.name != "First Person Controller") continue;
            if (!g.scene.IsValid()) continue;             // skip prefab assets
            if (g.GetComponent<GameSpawnManager>() != null) return g;  // authoritative player marker
        }
        return null;
    }

    void CaptureAndDisableControls(GameObject player)
    {
        if (player == null) return;
        Camera fpsCam = player.GetComponentInChildren<Camera>();

        // The player uses the Mini First-Person Controller (separate assembly), so the
        // movement/look behaviours are found and toggled by component name, like DiagWalker does.
        foreach (string typeName in new[] { "FirstPersonMovement", "Jump", "Crouch" })
        {
            TryDisableBehaviour(player, typeName);
        }
        if (fpsCam != null)
        {
            foreach (string typeName in new[] { "FirstPersonLook", "PlayerInteraction" })
            {
                TryDisableBehaviour(fpsCam.gameObject, typeName);
            }
        }
    }

    void TryDisableBehaviour(GameObject go, string typeName)
    {
        foreach (Component c in go.GetComponents<Component>())
        {
            if (c != null && c is Behaviour && c.GetType().Name == typeName)
            {
                Behaviour b = (Behaviour)c;
                if (b.enabled)
                {
                    b.enabled = false;
                    disabledControls.Add(b);
                }
                break;
            }
        }
    }

    void RestoreControls(GameObject player)
    {
        for (int i = 0; i < disabledControls.Count; i++)
        {
            if (disabledControls[i] != null) disabledControls[i].enabled = true;
        }
        disabledControls.Clear();
    }

    static void StretchToScreen(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}