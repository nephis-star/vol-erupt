using System.Collections;
using UnityEngine;
using UnityEngine.Video;
using UnityEngine.UI;

public class OutroVideo : MonoBehaviour
{
    static OutroVideo instance;
    public static OutroVideo Instance => instance;

    static readonly string PlayerName = "First Person Controller";

    VideoPlayer videoPlayer;
    RenderTexture renderTexture;
    GameObject overlayRoot;
    bool outroPlaying;
    bool videoEnded;
    bool wasInterviewActive;

    void Awake() { instance = this; }

    void Update()
    {
        if (outroPlaying) return;
        if (QuizUI.Instance == null) return;
        bool currentlyActive = QuizUI.Instance.IsInterviewActive();
        if (!currentlyActive && wasInterviewActive) StartCoroutine(PlayOutro());
        wasInterviewActive = currentlyActive;
    }

    IEnumerator PlayOutro()
    {
        outroPlaying = true;
        DisablePlayerControls();

        overlayRoot = new GameObject("OutroVideoOverlay");
        Canvas canvas = overlayRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32767;
        CanvasScaler scaler = overlayRoot.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        overlayRoot.AddComponent<GraphicRaycaster>();

        GameObject videoSurface = new GameObject("VideoSurface");
        videoSurface.transform.SetParent(overlayRoot.transform, false);
        RawImage raw = videoSurface.AddComponent<RawImage>();
        RectTransform rt = raw.rectTransform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        AspectRatioFitter af = videoSurface.AddComponent<AspectRatioFitter>();
        af.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;

        renderTexture = new RenderTexture(1920, 1080, 0);
        renderTexture.Create();
        raw.texture = renderTexture;

        videoPlayer = overlayRoot.AddComponent<VideoPlayer>();
        videoPlayer.playOnAwake = false;
        videoPlayer.source = VideoSource.Url;
        videoPlayer.url = "file:///" + Application.dataPath + "/Bulkan/evacuation-outro.mp4";
        videoPlayer.renderMode = VideoRenderMode.RenderTexture;
        videoPlayer.targetTexture = renderTexture;
        videoPlayer.isLooping = false;
        videoPlayer.waitForFirstFrame = true;
        videoPlayer.loopPointReached += OnVideoEnd;

        videoPlayer.Prepare();
        float start = Time.time;
        while (!videoPlayer.isPrepared && Time.time - start < 10f)
            yield return null;

        if (videoPlayer.isPrepared)
        {
            uint w = videoPlayer.width; uint h = videoPlayer.height;
            if (w > 0 && h > 0) af.aspectRatio = w / (float)h;
        }

        videoPlayer.Play();

        float timeout = Time.time + 300f;
        while (!videoEnded && Time.time < timeout)
            yield return null;

        Cleanup();
    }

    void OnVideoEnd(VideoPlayer vp) { videoEnded = true; }

    void Cleanup()
    {
        if (videoPlayer != null) { videoPlayer.loopPointReached -= OnVideoEnd; videoPlayer.Stop(); }
        if (overlayRoot != null) Destroy(overlayRoot);
        if (renderTexture != null) { renderTexture.Release(); renderTexture = null; }
        RestorePlayerControls();
        if (GameStateManager.Instance != null) GameStateManager.Instance.SetState(GameState.Completed);
        outroPlaying = false;
    }

    void DisablePlayerControls()
    {
        GameObject player = GameObject.Find(PlayerName);
        if (player == null) return;
        Camera fpsCam = player.GetComponentInChildren<Camera>();
        foreach (string typeName in new[] { "FirstPersonMovement", "Jump", "Crouch" })
            TryDisableBehaviour(player, typeName);
        if (fpsCam != null)
            foreach (string typeName in new[] { "FirstPersonLook", "PlayerInteraction" })
                TryDisableBehaviour(fpsCam.gameObject, typeName);
    }

    void TryDisableBehaviour(GameObject go, string typeName)
    {
        foreach (Component c in go.GetComponents<Component>())
        {
            if (c != null && c is Behaviour b && c.GetType().Name == typeName && b.enabled)
            { b.enabled = false; return; }
        }
    }

    void RestorePlayerControls()
    {
        GameObject player = GameObject.Find(PlayerName);
        if (player == null) return;
        Camera fpsCam = player.GetComponentInChildren<Camera>();
        foreach (string typeName in new[] { "FirstPersonMovement", "Jump", "Crouch" })
            TryEnableBehaviour(player, typeName);
        if (fpsCam != null)
            foreach (string typeName in new[] { "FirstPersonLook", "PlayerInteraction" })
                TryEnableBehaviour(fpsCam.gameObject, typeName);
    }

    void TryEnableBehaviour(GameObject go, string typeName)
    {
        foreach (Component c in go.GetComponents<Component>())
        {
            if (c != null && c is Behaviour b && c.GetType().Name == typeName && !b.enabled)
            { b.enabled = true; return; }
        }
    }
}
