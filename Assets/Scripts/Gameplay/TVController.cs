using UnityEngine;
using UnityEngine.Video;

/// <summary>
/// Plays the evacuation news video on the physical TV screen as soon as the player approaches.
/// The finished video delivers the evacuation advisory and advances the story.
/// </summary>
public class TVController : Interactable
{
    [Tooltip("Distance (m) from the TV that starts the evacuation news broadcast.")]
    public float activationRadius = 3.5f;

    [Tooltip("News video file name, resolved relative to the project's Assets folder.")]
    public string videoFileName = "tv animation.mp4";

    public bool NewsVideoPlayed { get; private set; }

    VideoPlayer videoPlayer;
    RenderTexture renderTexture;
    MeshRenderer screenRenderer;
    bool playing;

    void Awake()
    {
        videoPlayer = GetComponent<VideoPlayer>();
        if (videoPlayer == null)
            videoPlayer = gameObject.AddComponent<VideoPlayer>();

        screenRenderer = FindScreenRenderer(transform);
        if (screenRenderer == null)
            Debug.LogWarning("TVController: no 'Screen' renderer found under " + name + ".", this);
    }

    void Update()
    {
        if (playing || NewsVideoPlayed) return;

        ObjectiveManager om = ObjectiveManager.Instance;
        if (om == null || !om.newsWatchInstructed || om.newsVideoCompleted) return;

        VolcanoEventManager vem = VolcanoEventManager.Instance;
        if (vem == null || !vem.HasErupted) return;

        Camera cam = Camera.main;
        Vector3 p = cam != null ? cam.transform.position : transform.position;
        if ((p - transform.position).sqrMagnitude > activationRadius * activationRadius) return;

        PlayNews();
    }

    public override bool CanInteract
    {
        get { return false; } // The news plays by proximity; it is not an E-key interactable.
    }

    public override void Interact(GameObject interactor)
    {
    }

    void PlayNews()
    {
        playing = true;
        NewsVideoPlayed = true;

        renderTexture = new RenderTexture(1280, 720, 0);
        renderTexture.Create();

        if (screenRenderer != null)
        {
            Material videoMat = CreateVideoMaterial(renderTexture);
            if (videoMat != null)
            {
                Material[] mats = screenRenderer.sharedMaterials;
                if (mats == null || mats.Length == 0)
                    mats = new Material[] { videoMat };
                else
                    mats[0] = videoMat;
                screenRenderer.sharedMaterials = mats;
            }
        }

        videoPlayer.source = VideoSource.Url;
        videoPlayer.url = "file:///" + Application.dataPath + "/" + videoFileName;
        videoPlayer.playOnAwake = false;
        videoPlayer.renderMode = VideoRenderMode.RenderTexture;
        videoPlayer.targetTexture = renderTexture;
        videoPlayer.isLooping = false;
        videoPlayer.skipOnDrop = true;
        videoPlayer.loopPointReached += OnVideoEnd;
        videoPlayer.Prepare();
        videoPlayer.Play();
    }

    void OnVideoEnd(VideoPlayer vp)
    {
        NewsVideoPlayed = true;
        playing = false;
        ObjectiveManager om = ObjectiveManager.Instance;
        if (om != null) om.OnNewsVideoCompleted();
    }

    void OnDestroy()
    {
        if (videoPlayer != null) videoPlayer.loopPointReached -= OnVideoEnd;
        if (renderTexture != null)
        {
            renderTexture.Release();
            renderTexture = null;
        }
    }

    static MeshRenderer FindScreenRenderer(Transform root)
    {
        MeshRenderer fallback = null;
        foreach (MeshRenderer mr in root.GetComponentsInChildren<MeshRenderer>(true))
        {
            if (mr.gameObject.name == "Screen")
                return mr;
            if (fallback == null && mr.sharedMaterials != null)
            {
                foreach (Material m in mr.sharedMaterials)
                {
                    if (m != null && m.name.Contains("TVScreen"))
                    {
                        fallback = mr;
                        break;
                    }
                }
            }
        }
        return fallback;
    }

    static Material CreateVideoMaterial(RenderTexture rt)
    {
        Shader sh = Shader.Find("Universal Render Pipeline/Unlit");
        if (sh == null) sh = Shader.Find("Unlit/Texture");
        if (sh == null) sh = Shader.Find("Unlit");
        if (sh == null) return null;

        Material m = new Material(sh);
        if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", rt);
        if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", rt);
        m.mainTexture = rt;

        // The TV screen mesh (child "defaultMaterial.003" of "Screen") maps its UV V-axis
        // inverted: the top of the physical screen samples the bottom of the texture, so a
        // full-frame video appears upside down. Flip the material's V coordinate (scale.y = -1,
        // offset.y = 1) so the news video displays upright on the screen without touching the mesh.
        Vector2 flipScale = new Vector2(1f, -1f);
        Vector2 flipOffset = new Vector2(0f, 1f);
        if (m.HasProperty("_BaseMap")) { m.SetTextureScale("_BaseMap", flipScale); m.SetTextureOffset("_BaseMap", flipOffset); }
        if (m.HasProperty("_MainTex")) { m.SetTextureScale("_MainTex", flipScale); m.SetTextureOffset("_MainTex", flipOffset); }
        m.mainTextureScale = flipScale;
        m.mainTextureOffset = flipOffset;
        return m;
    }
}