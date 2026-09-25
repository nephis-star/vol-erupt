using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class AshfallController : MonoBehaviour
{
    public static AshfallController Instance { get; private set; }
    public static bool Active { get; private set; }

    const float TransitionBaselineSunFactor = 0.42f;
    const float TransitionBaselineAmbient = 0.5f;
    static readonly Color SunDimColor = new Color(0.76f, 0.78f, 0.88f);

    [Header("Ash particles (medium layer)")]
    public float emissionRate = 380f;
    public float emitHalfExtent = 70f;
    public float emitHeightAboveRoof = 16f;
    public float emitHeight = 30f;
    public float lifeMin = 5.5f;
    public float lifeMax = 8.5f;
    public float sizeMin = 0.06f;
    public float sizeMax = 0.16f;
    public float opacityMin = 0.3f;
    public float opacityMax = 0.55f;
    public float speedDownMin = -2.2f;
    public float speedDownMax = -3.4f;
    public float windX = 2.4f;
    public float windZ = -0.9f;
    public float gravity = 0.45f;
    public float noiseStrength = 1.6f;
    public float noiseFrequency = 0.45f;
    public Color ashColor = new Color(0.12f, 0.12f, 0.14f);

    [Header("Near ash specks (fine layer)")]
    public int specksRate = 560;
    public float specksSizeMin = 0.04f;
    public float specksSizeMax = 0.1f;
    public float specksLifeMin = 4f;
    public float specksLifeMax = 7f;
    public float specksSpawnHeight = 36f;

    [Header("Eruption plume (layered)")]
    public int plumeRate = 120;
    public float plumeSizeMin = 4f;
    public float plumeSizeMax = 7f;
    public float plumeSpeedMin = 10f;
    public float plumeSpeedMax = 16f;
    public int plumeMidRate = 50;
    public float plumeMidSizeMin = 8f;
    public float plumeMidSizeMax = 18f;
    public float plumeMidSpeedMin = 4f;
    public float plumeMidSpeedMax = 7f;
    public int plumeUpperRate = 30;
    public float plumeUpperSizeMin = 15f;
    public float plumeUpperSizeMax = 30f;
    public float plumeUpperSpeedMin = 1f;
    public float plumeUpperSpeedMax = 3f;
    public float plumeMidLift = 12f;
    public float plumeUpperLift = 38f;

    [Header("Sky ash canopy (replaces the old sphere-cloud layer)")]
    public int canopyRate = 22;
    public float canopySizeMin = 20f;
    public float canopySizeMax = 38f;
    public float canopyLiftY = 190f;
    public int canopyLayers = 2;
    public float canopySpreadBase = 100f;
    public float canopySpreadGap = 130f;
    public float canopyFadeSeconds = 6f;

    [Header("Near haze atmosphere")]
    public int hazeRate = 8;
    public float hazeSizeMin = 8f;
    public float hazeSizeMax = 14f;
    public float hazeOpacityMin = 0.05f;
    public float hazeOpacityMax = 0.09f;
    public float hazeLiftY = 40f;
    public Color hazeColor = new Color(0.58f, 0.6f, 0.64f);

    [Header("Distant haze (toward the volcano)")]
    public int farHazeRate = 10;
    public float farHazeSizeMin = 12f;
    public float farHazeSizeMax = 22f;
    public float farHazeLiftY = 70f;

    [Header("Atmosphere / lighting (runtime only, restored on completion)")]
    [Tooltip("Final directional light intensity as a factor of the original. ~0.4 = moderate ashy daytime (not night).")]
    [Range(0f, 1f)] public float sunDimFactor = 0.4f;
    [Tooltip("Final ambient/environment lighting as a factor of the original. ~0.45 = overcast yet readable, trees stay colored.")]
    [Range(0f, 1f)] public float ambientDimFactor = 0.45f;
    [Tooltip("Start fog density applied instantly at eruption (must be > 0 for the tests).")]
    public float fogDensityStart = 0.012f;
    [Tooltip("Final fog density reached after the smooth transition (depth-based dusty volcanic haze).")]
    public float fogDensity = 0.018f;
    [Tooltip("Seconds to blend from the instant baseline to the fully dark volcanic atmosphere.")]
    public float atmosphereTransitionSeconds = 1.6f;
    public Color fogColor = new Color(0.58f, 0.62f, 0.70f);

    [Tooltip("Skybox exposure factor during ashfall (dimmed overcast ash sky, not deep blue).")]
    [Range(0.1f, 1f)] public float skyExposureFactor = 0.64f;
    [Tooltip("Skybox tint during ashfall; desaturated gray-blue ash sky.")]
    public Color skyTint = new Color(0.58f, 0.61f, 0.68f);
    [Tooltip("Skybox atmosphere thickness factor (reduces blue scattering).")]
    [Range(0.1f, 1f)] public float skyAtmosphereThicknessFactor = 0.65f;
    [Tooltip("Skybox horizon ground color during ashfall (muted ash-brown).")]
    public Color skyGroundColor = new Color(0.52f, 0.5f, 0.48f);

    [Header("Roof blocker")]
    public float roofBlockerHeight = 1f;

    public BoxCollider RoofBlocker { get; private set; }
    public ParticleSystem AshSystem { get; private set; }
    public ParticleSystem HazeSystem { get; private set; }
    public ParticleSystem NearSpecksSystem { get; private set; }
    public ParticleSystem PlumeSystem { get; private set; }
    public ParticleSystem FarHazeSystem { get; private set; }
    public bool AtmosphereActive { get; private set; }

    readonly List<ParticleSystem> managedSystems = new List<ParticleSystem>();
    bool activeInstance;

    ParticleSystem ashPs;
    ParticleSystem hazePs;
    ParticleSystem specksPs;
    ParticleSystem plumePs;
    ParticleSystem plumeMidPs;
    ParticleSystem plumeUpperPs;
    ParticleSystem farHazePs;
    readonly List<ParticleSystem> canopySystems = new List<ParticleSystem>();

    Material ashMaterial;
    Material grainMaterial;
    Material puffMaterial;
    Material canopyMaterial;
    GameObject blockerObject;

    Light sunLight;
    float sunIntensity0;
    Color sunColor0;
    Color ambientLight0;
    Color ambientSky0;
    Color ambientEquator0;
    Color ambientGround0;
    bool fogEnabled0;
    Color fogColor0;
    FogMode fogMode0;
    float fogDensity0;
    float fogStart0;
    float fogEnd0;
    Coroutine atmosphereTransitionRoutine;

    Material skyboxMat0;
    bool hasSkyExposure;
    bool hasSkyTint;
    bool hasSkyThickness;
    bool hasSkyGround;
    float skyExposure0 = -1f;
    Color skyTint0 = Color.white;
    float skyThickness0 = 1f;
    Color skyGround0 = Color.gray;

    [Header("Start immediately (bypasses eruption trigger)")]
    public bool startImmediately = true;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        Configure();
        if (startImmediately)
        {
            activeInstance = true;
            Active = true;
            StartLayerSequence();
            ApplyAshAtmosphere();
        }
    }

    void OnDestroy()
    {
        RestoreAtmosphere();
    }

    void Update()
    {
        VolcanoEventManager vem = VolcanoEventManager.Instance;
        GameStateManager gs = GameStateManager.Instance;
        if (vem == null || gs == null) return;

        if (vem.HasErupted && !activeInstance)
        {
            activeInstance = true;
            Active = true;
            StartLayerSequence();
            ApplyAshAtmosphere();
        }
        else if (activeInstance && gs.CurrentState == GameState.Completed)
        {
            activeInstance = false;
            Active = false;
            StopAll();
            RestoreAtmosphere();
        }
    }

    void StartLayerSequence()
    {
        // 0-1 s: plume core + fine ash immediately (prewarmed column visible at once).
        PlayImmediately(ashPs, specksPs, plumePs);
        StartCoroutine(RampPlumeCore());
        // 1-5 s: mid/upper plume layers rise and grow the column.
        StartCoroutine(PlayDelayed(plumeMidPs, 1.05f));
        StartCoroutine(PlayDelayed(plumeUpperPs, 2.2f));
        // Sky ash canopy fades in over the first seconds (main overcast source),
        // then haze thickens on top.
        StartCoroutine(PlayDelayed(canopySystems.Count > 0 ? canopySystems[0] : null, 0.6f));
        StartCoroutine(PlayDelayed(canopySystems.Count > 1 ? canopySystems[1] : null, 2.5f));
        StartCoroutine(FadeCanopyIn());
        StartCoroutine(PlayDelayed(hazePs, 1.2f));
        StartCoroutine(PlayDelayed(farHazePs, 1.4f));
    }

    IEnumerator FadeCanopyIn()
    {
        if (canopyMaterial == null) yield break;
        float dur = Mathf.Max(1f, canopyFadeSeconds);
        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / dur));
            canopyMaterial.SetColor("_BaseColor", new Color(1f, 1f, 1f, k));
            yield return null;
        }
        canopyMaterial.SetColor("_BaseColor", Color.white);
    }

    IEnumerator RampPlumeCore()
    {
        if (plumePs == null) yield break;
        ParticleSystem.EmissionModule e = plumePs.emission;
        e.enabled = true;
        float target = plumeRate;
        float t = 0f;
        float duration = 1.1f;
        while (t < duration)
        {
            t += Time.deltaTime;
            e.rateOverTime = Mathf.Lerp(target * 0.45f, target, Mathf.Clamp01(t / duration));
            yield return null;
        }
        e.rateOverTime = target;
    }

    void PlayImmediately(ParticleSystem ps, params ParticleSystem[] others)
    {
        if (ps != null) ps.Play();
        for (int i = 0; i < others.Length; i++)
            if (others[i] != null) others[i].Play();
    }

    IEnumerator PlayDelayed(ParticleSystem ps, float delay)
    {
        if (ps == null) yield break;
        yield return new WaitForSeconds(delay);
        if (activeInstance) ps.Play();
    }

    void StopAll()
    {
        for (int i = 0; i < managedSystems.Count; i++)
            if (managedSystems[i] != null)
                managedSystems[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    void Configure()
    {
        ashPs = GetComponent<ParticleSystem>();
        if (ashPs == null) ashPs = GetComponentInChildren<ParticleSystem>();

        Bounds house = ComputeHouseBounds();
        float roofY = house.max.y;
        Vector3 center = new Vector3(house.center.x, roofY, house.center.z);

        Transform at = transform;
        at.SetParent(null, false);
        at.position = new Vector3(center.x, roofY + emitHeightAboveRoof, center.z);
        at.localScale = Vector3.one;

        CacheAtmosphere();

        VolcanoEventManager vem0 = VolcanoEventManager.Instance;
        if (vem0 != null && vem0.volcanoSmokeEffect != null)
        {
            // Legacy scene crater smoke is fully replaced by the layered plume below.
            // Neutralise its emitter so it can never draw its own (potentially broken) smoke.
            ParticleSystem.MainModule sm = vem0.volcanoSmokeEffect.main;
            sm.startColor = new ParticleSystem.MinMaxGradient(
                WithAlphaMul(new Color(0.72f, 0.74f, 0.80f), 0.55f),
                WithAlphaMul(new Color(0.90f, 0.91f, 0.94f), 0.85f));
            ParticleSystem.EmissionModule sem = vem0.volcanoSmokeEffect.emission;
            sem.enabled = false;
            sem.rateOverTime = 0f;
            vem0.volcanoSmokeEffect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        if (ashPs != null)
        {
            ashMaterial = CreateMaterial(CreateAshFlakeTexture());
            grainMaterial = CreateMaterial(CreateGrainTexture());
            puffMaterial = CreateMaterial(CreateSmokePuffTexture());
            canopyMaterial = CreateMaterial(CreateSkyCanopyTexture());

            if (vem0 != null && vem0.volcanoSmokeEffect != null)
                FixCraterSmokeMaterial(vem0.volcanoSmokeEffect, puffMaterial);

            ConfigureAsh(ashPs);
            SetupRenderer(ashPs, ashMaterial);
            AshSystem = ashPs;
            managedSystems.Add(ashPs);

            RoofBlocker = CreateRoofBlocker(house);

            hazePs = CreateNearHaze(house);
            HazeSystem = hazePs;
            managedSystems.Add(hazePs);

            specksPs = CreateNearSpecks(house);
            NearSpecksSystem = specksPs;
            managedSystems.Add(specksPs);

            farHazePs = CreateFarHaze(house);
            FarHazeSystem = farHazePs;
            managedSystems.Add(farHazePs);

            plumePs = CreatePlume(house);
            PlumeSystem = plumePs;
            managedSystems.Add(plumePs);

            plumeMidPs = CreatePlumeMid(house);
            managedSystems.Add(plumeMidPs);

            plumeUpperPs = CreatePlumeUpper(house);
            managedSystems.Add(plumeUpperPs);

            CreateSkyCanopy(house);
        }
    }

    #region L1 - Eruption plume

    Vector3 ComputePlumeAnchor()
    {
        VolcanoEventManager vem = VolcanoEventManager.Instance;
        if (vem != null && vem.volcanoSmokeEffect != null)
            return vem.volcanoSmokeEffect.transform.position;

        GameObject go = GameObject.Find("VolcanoSmokeEffect");
        if (go != null) return go.transform.position;

        return new Vector3(-64.86282f, 33.61087f, 48.47305f);
    }

    Vector3 PlumeWindOffset(float dist)
    {
        float len = Mathf.Max(0.01f, Mathf.Sqrt(windX * windX + windZ * windZ));
        return new Vector3(windX / len * dist, 0f, windZ / len * dist);
    }

    void ConfigurePlumeCommon(ParticleSystem ps,
        float lifeMin, float lifeMax, float sizeMin, float sizeMax,
        Color colorA, float alphaA, Color colorB, float alphaB,
        float velYMin, float velYMax, float windFactor,
        float noiseMin, float noiseMax, float noiseFreq,
        float growStart, float growEnd,
        float alphaPeak,
        int maxParticles)
    {
        ParticleSystem.MainModule main = ps.main;
        main.duration = 1f;
        main.loop = true;
        main.playOnAwake = false;
        main.prewarm = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(lifeMin, lifeMax);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, 6.28f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            WithAlphaMul(colorA, alphaA),
            WithAlphaMul(colorB, alphaB));
        main.gravityModifier = -0.35f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = maxParticles;

        ParticleSystem.EmissionModule emission = ps.emission;
        emission.enabled = true;

        ParticleSystem.VelocityOverLifetimeModule velocity = ps.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = new ParticleSystem.MinMaxCurve(windX * windFactor - 1.2f, windX * windFactor + 1.2f);
        velocity.y = new ParticleSystem.MinMaxCurve(velYMin, velYMax);
        velocity.z = new ParticleSystem.MinMaxCurve(windZ * windFactor - 1.2f, windZ * windFactor + 1.2f);

        ParticleSystem.NoiseModule noise = ps.noise;
        noise.enabled = true;
        noise.strength = new ParticleSystem.MinMaxCurve(noiseMin, noiseMax);
        noise.frequency = noiseFreq;
        noise.scrollSpeed = 0.5f;
        noise.damping = true;
        noise.octaveCount = 2;
        noise.quality = ParticleSystemNoiseQuality.Low;

        ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        AnimationCurve grow = new AnimationCurve(
            new Keyframe(0f, growStart),
            new Keyframe(0.55f, 1f),
            new Keyframe(1f, growEnd));
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, grow);

        Color m1 = Color.Lerp(colorA, colorB, 0.4f);
        Color m2 = Color.Lerp(colorB, Color.white, 0.35f);
        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient g = new Gradient();
        g.SetKeys(
            new[]
            {
                new GradientColorKey(colorA, 0f),
                new GradientColorKey(m1, 0.45f),
                new GradientColorKey(m2, 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(alphaPeak, 0.14f),
                new GradientAlphaKey(alphaPeak * 0.68f, 0.55f),
                new GradientAlphaKey(0f, 1f)
            });
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(g);
    }

    ParticleSystem CreatePlume(Bounds house)
    {
        Vector3 anchor = ComputePlumeAnchor();
        ParticleSystem ps = CreateSystem("Eruption Plume Core", anchor);

        ParticleSystem.ShapeModule shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(4f, 3f, 4f);

        ConfigurePlumeCommon(ps,
            2.2f, 3.6f,
            plumeSizeMin, plumeSizeMax,
            new Color(0.78f, 0.77f, 0.79f), 0.5f,
            new Color(0.92f, 0.90f, 0.90f), 0.75f,
            plumeSpeedMin, plumeSpeedMax, 0.2f,
            2f, 3.2f, 0.5f,
            0.5f, 1.7f,
            0.6f,
            700);

        ParticleSystem.EmissionModule emission = ps.emission;
        emission.rateOverTime = plumeRate;

        SetupRenderer(ps, puffMaterial);
        return ps;
    }

    ParticleSystem CreatePlumeMid(Bounds house)
    {
        Vector3 anchor = ComputePlumeAnchor();
        Vector3 pos = anchor + PlumeWindOffset(4f) + Vector3.up * plumeMidLift;
        ParticleSystem ps = CreateSystem("Eruption Plume Mid", pos);

        ParticleSystem.ShapeModule shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(12f, 7f, 12f);

        ConfigurePlumeCommon(ps,
            4f, 6f,
            plumeMidSizeMin, plumeMidSizeMax,
            new Color(0.80f, 0.81f, 0.84f), 0.35f,
            new Color(0.93f, 0.93f, 0.95f), 0.55f,
            plumeMidSpeedMin, plumeMidSpeedMax, 0.55f,
            3f, 4.5f, 0.4f,
            0.6f, 1.5f,
            0.4f,
            900);

        ParticleSystem.EmissionModule emission = ps.emission;
        emission.rateOverTime = plumeMidRate;

        SetupRenderer(ps, puffMaterial);
        return ps;
    }

    ParticleSystem CreatePlumeUpper(Bounds house)
    {
        Vector3 anchor = ComputePlumeAnchor();
        Vector3 pos = anchor + PlumeWindOffset(12f) + Vector3.up * plumeUpperLift;
        ParticleSystem ps = CreateSystem("Eruption Plume Upper", pos);

        ParticleSystem.ShapeModule shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(28f, 12f, 28f);

        ConfigurePlumeCommon(ps,
            7f, 10f,
            plumeUpperSizeMin, plumeUpperSizeMax,
            new Color(0.84f, 0.85f, 0.88f), 0.16f,
            new Color(0.95f, 0.95f, 0.97f), 0.3f,
            plumeUpperSpeedMin, plumeUpperSpeedMax, 1.2f,
            4f, 6f, 0.3f,
            0.7f, 1.5f,
            0.22f,
            1600);

        // Upper layer drifts on the wind more horizontally than vertically.
        ParticleSystem.VelocityOverLifetimeModule velocity = ps.velocityOverLifetime;
        velocity.x = new ParticleSystem.MinMaxCurve(windX * 1.5f - 3f, windX * 1.5f + 3f);
        velocity.z = new ParticleSystem.MinMaxCurve(windZ * 1.5f - 3f, windZ * 1.5f + 3f);

        ParticleSystem.EmissionModule emission = ps.emission;
        emission.rateOverTime = plumeUpperRate;

        SetupRenderer(ps, puffMaterial);
        return ps;
    }

    #endregion

    #region L2 - Ash cloud

    void CreateSkyCanopy(Bounds house)
    {
        Vector3 anchor = ComputePlumeAnchor();
        Vector3 toPlay = new Vector3(house.center.x - anchor.x, 0f, house.center.z - anchor.z);
        Vector3 dirXZ = toPlay.sqrMagnitude > 0.001f ? toPlay.normalized : Vector3.forward;

        for (int i = 0; i < canopyLayers; i++)
        {
            float spread = canopySpreadBase + i * canopySpreadGap;
            Vector3 pos = anchor
                + dirXZ * spread
                + PlumeWindOffset(spread * 0.35f)
                + Vector3.up * canopyLiftY;

            ParticleSystem ps = CreateSystem(i == 0 ? "Sky Ash Canopy A" : "Sky Ash Canopy B", pos);

            ParticleSystem.MainModule main = ps.main;
            main.duration = 1f;
            main.loop = true;
            main.playOnAwake = false;
            main.prewarm = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(25f, 40f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(canopySizeMin, canopySizeMax);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, 6.28f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                WithAlphaMul(new Color(0.83f, 0.84f, 0.89f), 0.5f),
                WithAlphaMul(new Color(0.94f, 0.94f, 0.97f), 0.85f));
            main.gravityModifier = -0.03f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 2600;

            ParticleSystem.EmissionModule emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = canopyRate;

            ParticleSystem.ShapeModule shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(280f, 60f, 240f);

            ParticleSystem.VelocityOverLifetimeModule velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(windX * 0.9f - 1.4f, windX * 0.9f + 1.4f);
            velocity.y = new ParticleSystem.MinMaxCurve(-1.2f, 2f);
            velocity.z = new ParticleSystem.MinMaxCurve(windZ * 0.9f - 1.4f, windZ * 0.9f + 1.4f);

            ParticleSystem.NoiseModule noise = ps.noise;
            noise.enabled = true;
            noise.strength = new ParticleSystem.MinMaxCurve(4f, 7f);
            noise.frequency = 0.16f;
            noise.scrollSpeed = 0.4f;
            noise.damping = true;
            noise.octaveCount = 2;
            noise.quality = ParticleSystemNoiseQuality.Low;

            ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            AnimationCurve grow = new AnimationCurve(
                new Keyframe(0f, 0.7f),
                new Keyframe(1f, 1.5f));
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, grow);

            ParticleSystem.ColorOverLifetimeModule colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient g = new Gradient();
            g.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(0.83f, 0.84f, 0.89f), 0f),
                    new GradientColorKey(new Color(0.8f, 0.81f, 0.86f), 0.5f),
                    new GradientColorKey(new Color(0.9f, 0.9f, 0.93f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(0.16f, 0.22f),
                    new GradientAlphaKey(0.16f, 0.8f),
                    new GradientAlphaKey(0f, 1f)
                });
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(g);

            SetupRenderer(ps, canopyMaterial);
            canopySystems.Add(ps);
            managedSystems.Add(ps);
        }
    }

    #endregion

    #region L3 - Distant ash haze

    ParticleSystem CreateNearHaze(Bounds house)
    {
        ParticleSystem ps = CreateSystem("Volcanic Haze", Vector3.zero);

        ParticleSystem.MainModule main = ps.main;
        main.duration = 1f;
        main.loop = true;
        main.playOnAwake = false;
        main.prewarm = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(20f, 30f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(hazeSizeMin, hazeSizeMax);
        main.startColor = new ParticleSystem.MinMaxGradient(
            WithAlphaMul(hazeColor, hazeOpacityMin),
            WithAlphaMul(hazeColor, hazeOpacityMax));
        main.gravityModifier = 0f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 400;

        ParticleSystem.EmissionModule emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = hazeRate;

        ParticleSystem.ShapeModule shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(60f, 30f, 60f);

        ParticleSystem.VelocityOverLifetimeModule velocity = ps.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = new ParticleSystem.MinMaxCurve(windX * 0.9f - 0.6f, windX * 0.9f + 0.6f);
        velocity.y = new ParticleSystem.MinMaxCurve(1f, 2.5f);
        velocity.z = new ParticleSystem.MinMaxCurve(windZ * 0.9f - 0.6f, windZ * 0.9f + 0.6f);

        ParticleSystem.NoiseModule noise = ps.noise;
        noise.enabled = true;
        noise.strength = new ParticleSystem.MinMaxCurve(2.5f, 4.5f);
        noise.frequency = 0.26f;
        noise.scrollSpeed = 0.5f;
        noise.damping = true;
        noise.octaveCount = 1;
        noise.quality = ParticleSystemNoiseQuality.Low;

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(hazeColor, 0f), new GradientColorKey(hazeColor, 1f) },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(0.5f, 0.25f),
                new GradientAlphaKey(0.5f, 0.8f),
                new GradientAlphaKey(0f, 1f)
            });
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(g);

        Vector3 crater = ComputePlumeAnchor();
        ps.transform.position = crater + Vector3.up * 25f;
        ps.transform.localScale = Vector3.one;

        SetupRenderer(ps, puffMaterial);
        return ps;
    }

    ParticleSystem CreateFarHaze(Bounds house)
    {
        Vector3 anchor = ComputePlumeAnchor();
        ParticleSystem ps = CreateSystem("Distant Ash Haze", Vector3.zero);

        ParticleSystem.MainModule main = ps.main;
        main.duration = 1f;
        main.loop = true;
        main.playOnAwake = false;
        main.prewarm = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(24f, 35f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(farHazeSizeMin, farHazeSizeMax);
        main.startColor = new ParticleSystem.MinMaxGradient(
            WithAlphaMul(hazeColor, hazeOpacityMin * 0.8f),
            WithAlphaMul(hazeColor, hazeOpacityMax * 0.8f));
        main.gravityModifier = 0f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 450;

        ParticleSystem.EmissionModule emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = farHazeRate;

        ParticleSystem.ShapeModule shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(110f, 28f, 110f);

        ParticleSystem.VelocityOverLifetimeModule velocity = ps.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = new ParticleSystem.MinMaxCurve(windX * 1.4f - 1f, windX * 1.4f + 1f);
        velocity.y = new ParticleSystem.MinMaxCurve(0f, 0.8f);
        velocity.z = new ParticleSystem.MinMaxCurve(windZ * 1.4f - 1f, windZ * 1.4f + 1f);

        ParticleSystem.NoiseModule noise = ps.noise;
        noise.enabled = true;
        noise.strength = new ParticleSystem.MinMaxCurve(3f, 5f);
        noise.frequency = 0.22f;
        noise.scrollSpeed = 0.5f;
        noise.damping = true;
        noise.octaveCount = 1;
        noise.quality = ParticleSystemNoiseQuality.Low;

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(hazeColor, 0f), new GradientColorKey(hazeColor, 1f) },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(0.45f, 0.3f),
                new GradientAlphaKey(0.45f, 0.8f),
                new GradientAlphaKey(0f, 1f)
            });
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(g);

        ps.transform.position = anchor + PlumeWindOffset(20f) + Vector3.up * farHazeLiftY;
        ps.transform.localScale = Vector3.one;

        SetupRenderer(ps, puffMaterial);
        return ps;
    }

    #endregion

    #region L4 - Medium ashfall (scene AshFallEffect)

    void ConfigureAsh(ParticleSystem ps)
    {
        if (ps.isPlaying || ps.main.playOnAwake)
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = ps.main;
        main.duration = 1f;
        main.loop = true;
        main.playOnAwake = false;
        main.prewarm = false;
        main.startDelay = 0f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(lifeMin, lifeMax);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, 6.28f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            WithAlphaMul(ashColor, opacityMin),
            WithAlphaMul(ashColor, opacityMax));
        main.gravityModifier = gravity;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 4500;

        ParticleSystem.EmissionModule emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = emissionRate;

        ParticleSystem.ShapeModule shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(emitHalfExtent * 2f, emitHeight, emitHalfExtent * 2f);

        ParticleSystem.VelocityOverLifetimeModule velocity = ps.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = new ParticleSystem.MinMaxCurve(windX - 0.9f, windX + 0.9f);
        velocity.y = new ParticleSystem.MinMaxCurve(speedDownMin, speedDownMax);
        velocity.z = new ParticleSystem.MinMaxCurve(windZ - 0.8f, windZ + 0.8f);

        ParticleSystem.NoiseModule noise = ps.noise;
        noise.enabled = true;
        noise.strength = new ParticleSystem.MinMaxCurve(noiseStrength * 0.6f, noiseStrength);
        noise.frequency = noiseFrequency;
        noise.scrollSpeed = 0.4f;
        noise.damping = true;
        noise.octaveCount = 1;
        noise.quality = ParticleSystemNoiseQuality.Low;

        ParticleSystem.RotationOverLifetimeModule rot = ps.rotationOverLifetime;
        rot.enabled = true;
        rot.z = new ParticleSystem.MinMaxCurve(-0.9f, 0.9f);

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(ashColor, 0f), new GradientColorKey(ashColor, 1f) },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(0.7f, 0.15f),
                new GradientAlphaKey(0.7f, 0.75f),
                new GradientAlphaKey(0f, 1f)
            });
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(g);

        ParticleSystem.CollisionModule collision = ps.collision;
        collision.enabled = true;
        collision.type = ParticleSystemCollisionType.World;
        collision.mode = ParticleSystemCollisionMode.Collision3D;
        collision.collidesWith = 1 << 0;
        collision.dampen = 0f;
        collision.bounce = 0f;
        collision.lifetimeLoss = 1f;
        collision.radiusScale = 0.04f;
        collision.quality = ParticleSystemCollisionQuality.Medium;
        collision.voxelSize = 0.5f;
        collision.sendCollisionMessages = false;
        collision.maxCollisionShapes = 32;
        collision.enableDynamicColliders = false;
    }

    #endregion

    #region L5 - Near ash specks

    ParticleSystem CreateNearSpecks(Bounds house)
    {
        ParticleSystem ps = CreateSystem("Near Ash Specks", Vector3.zero);

        ParticleSystem.MainModule main = ps.main;
        main.duration = 1f;
        main.loop = true;
        main.playOnAwake = false;
        main.prewarm = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(specksLifeMin, specksLifeMax);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(specksSizeMin, specksSizeMax);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, 6.28f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            WithAlphaMul(new Color(0.06f, 0.06f, 0.07f), 0.5f),
            WithAlphaMul(new Color(0.09f, 0.09f, 0.11f), 0.9f));
        main.gravityModifier = 0.4f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 3400;

        ParticleSystem.EmissionModule emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = specksRate;

        ParticleSystem.ShapeModule shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(emitHalfExtent * 2f + 20f, specksSpawnHeight, emitHalfExtent * 2f + 20f);

        ParticleSystem.VelocityOverLifetimeModule velocity = ps.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = new ParticleSystem.MinMaxCurve(windX * 1.1f - 0.8f, windX * 1.1f + 0.8f);
        velocity.y = new ParticleSystem.MinMaxCurve(-2.2f, -3.4f);
        velocity.z = new ParticleSystem.MinMaxCurve(windZ * 1.1f - 0.8f, windZ * 1.1f + 0.8f);

        ParticleSystem.NoiseModule noise = ps.noise;
        noise.enabled = true;
        noise.strength = new ParticleSystem.MinMaxCurve(0.6f, 1.4f);
        noise.frequency = 0.5f;
        noise.scrollSpeed = 0.4f;
        noise.damping = true;
        noise.octaveCount = 1;
        noise.quality = ParticleSystemNoiseQuality.Low;

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient g = new Gradient();
        g.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(0.06f, 0.06f, 0.07f), 0f),
                new GradientColorKey(new Color(0.09f, 0.09f, 0.11f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(0.9f, 0.18f),
                new GradientAlphaKey(0.9f, 0.8f),
                new GradientAlphaKey(0f, 1f)
            });
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(g);

        ParticleSystem.CollisionModule collision = ps.collision;
        collision.enabled = true;
        collision.type = ParticleSystemCollisionType.World;
        collision.mode = ParticleSystemCollisionMode.Collision3D;
        collision.collidesWith = 1 << 0;
        collision.dampen = 0f;
        collision.bounce = 0f;
        collision.lifetimeLoss = 1f;
        collision.radiusScale = 0.03f;
        collision.quality = ParticleSystemCollisionQuality.Medium;
        collision.voxelSize = 0.5f;
        collision.sendCollisionMessages = false;
        collision.maxCollisionShapes = 32;
        collision.enableDynamicColliders = false;

        ps.transform.position = new Vector3(
            house.center.x,
            house.max.y + specksSpawnHeight * 0.5f,
            house.center.z);
        ps.transform.localScale = Vector3.one;

        SetupRenderer(ps, grainMaterial);
        return ps;
    }

    #endregion

    #region Shared particle helpers

    ParticleSystem CreateSystem(string name, Vector3 position)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(null, false);
        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        go.transform.position = position;
        go.transform.localScale = Vector3.one;
        return ps;
    }

    BoxCollider CreateRoofBlocker(Bounds house)
    {
        if (blockerObject == null)
        {
            blockerObject = new GameObject("AshRoofBlocker");
            blockerObject.transform.SetParent(null, false);
        }

        BoxCollider bc = blockerObject.GetComponent<BoxCollider>();
        if (bc == null) bc = blockerObject.AddComponent<BoxCollider>();

        bc.size = new Vector3(house.size.x + 8f, roofBlockerHeight, house.size.z + 8f);
        bc.center = Vector3.zero;
        bc.isTrigger = false;
        bc.enabled = true;
        blockerObject.transform.position = new Vector3(house.center.x, house.max.y, house.center.z);
        blockerObject.transform.localScale = Vector3.one;
        blockerObject.name = "AshRoofBlocker";
        return bc;
    }

    void SetupRenderer(ParticleSystem ps, Material mat)
    {
        ParticleSystemRenderer r = ps.GetComponent<ParticleSystemRenderer>();
        if (r == null) return;
        r.renderMode = ParticleSystemRenderMode.Billboard;
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
    }

    void FixCraterSmokeMaterial(ParticleSystem ps, Material mat)
    {
        ParticleSystemRenderer r = ps.GetComponent<ParticleSystemRenderer>();
        if (r == null) return;
        r.renderMode = ParticleSystemRenderMode.Billboard;
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
    }

    Material CreateMaterial(Texture2D baseMap)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null) shader = Shader.Find("Particles/Standard Unlit");
        Material mat = new Material(shader);

        if (baseMap != null) mat.SetTexture("_BaseMap", baseMap);
        mat.SetColor("_BaseColor", Color.white);
        mat.SetFloat("_Surface", 1f);
        mat.SetFloat("_Blend", 0f);
        mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetFloat("_SrcBlendAlpha", 1f);
        mat.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetFloat("_ZWrite", 0f);
        mat.SetFloat("_Cull", 0f);
        mat.SetFloat("_AlphaClip", 0f);
        mat.SetFloat("_ColorMode", 0f);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        return mat;
    }

    Texture2D CreateAshFlakeTexture()
    {
        const int size = 64;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color[] px = new Color[size * size];
        for (int i = 0; i < px.Length; i++) px[i] = new Color(0f, 0f, 0f, 0f);

        int flakes = Random.Range(4, 7);
        for (int f = 0; f < flakes; f++)
        {
            Vector2 c = new Vector2(Random.Range(14f, size - 14f), Random.Range(14f, size - 14f));
            float baseR = Random.Range(2.5f, 4.5f);
            float aspect = Random.Range(1.5f, 3.2f);
            float angle = Random.Range(0f, Mathf.PI);
            float shade = Random.Range(0.45f, 0.7f);
            float shadeB = shade * Random.Range(0.92f, 1.06f);

            int sides = Random.Range(5, 9);
            Vector2[] pts = new Vector2[sides];
            for (int i = 0; i < sides; i++)
            {
                float th = i * (Mathf.PI * 2f) / sides;
                float r = baseR * Random.Range(0.6f, 1.25f);
                float lx = Mathf.Cos(th) * r;
                float ly = Mathf.Sin(th) * r * aspect;
                float lc = Mathf.Cos(angle), ls = Mathf.Sin(angle);
                pts[i] = c + new Vector2(lx * lc - ly * ls, lx * ls + ly * lc);
            }

            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
            for (int i = 0; i < sides; i++)
            {
                minX = Mathf.Min(minX, pts[i].x); maxX = Mathf.Max(maxX, pts[i].x);
                minY = Mathf.Min(minY, pts[i].y); maxY = Mathf.Max(maxY, pts[i].y);
            }
            for (int py = Mathf.Max(0, Mathf.FloorToInt(minY)); py <= Mathf.Min(size - 1, Mathf.CeilToInt(maxY)); py++)
            {
                for (int pxx = Mathf.Max(0, Mathf.FloorToInt(minX)); pxx <= Mathf.Min(size - 1, Mathf.CeilToInt(maxX)); pxx++)
                {
                    if (!PointInPolygon(new Vector2(pxx + 0.5f, py + 0.5f), pts)) continue;
                    Color c0 = px[py * size + pxx];
                    px[py * size + pxx] = new Color(shade, shade * 0.98f, shadeB, 1f);
                }
            }
        }

        for (int py = 1; py < size - 1; py++)
        {
            for (int pxx = 1; pxx < size - 1; pxx++)
            {
                int idx = py * size + pxx;
                if (px[idx].a <= 0.5f) continue;
                bool onEdge = px[(py - 1) * size + pxx].a < 0.5f || px[(py + 1) * size + pxx].a < 0.5f
                    || px[py * size + pxx - 1].a < 0.5f || px[py * size + pxx + 1].a < 0.5f;
                if (onEdge)
                    px[idx] = new Color(px[idx].r, px[idx].g, px[idx].b, 0.72f);
            }
        }

        for (int s = 0; s < 26; s++)
        {
            int pxx = Random.Range(1, size - 1);
            int pyy = Random.Range(1, size - 1);
            int idx = pyy * size + pxx;
            if (px[idx].a < 0.5f)
                px[idx] = new Color(0.3f, 0.3f, 0.34f, 1f);
        }

        tex.SetPixels(px);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        tex.Apply();
        return tex;
    }

    static bool PointInPolygon(Vector2 p, Vector2[] poly)
    {
        bool inside = false;
        int j = poly.Length - 1;
        for (int i = 0; i < poly.Length; i++)
        {
            if ((poly[i].y > p.y) != (poly[j].y > p.y) &&
                p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x)
                inside = !inside;
            j = i;
        }
        return inside;
    }

    Texture2D CreateGrainTexture()
    {
        const int size = 48;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[size * size];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color(0f, 0f, 0f, 0f);

        int specks = Random.Range(60, 90);
        for (int s = 0; s < specks; s++)
        {
            int pxx = Random.Range(1, size - 2);
            int pyy = Random.Range(1, size - 2);
            float alpha = Random.Range(0.55f, 1f);
            float shade = Random.Range(0.4f, 0.6f);
            int len = Random.value < 0.35f ? Random.Range(2, 4) : 1;
            bool horiz = Random.value < 0.5f;
            for (int l = 0; l < len; l++)
            {
                int lx = pxx + (horiz ? l : 0);
                int ly = pyy + (horiz ? 0 : l);
                if (lx >= size || ly >= size) break;
                pixels[ly * size + lx] = new Color(
                    shade, shade * 0.97f, shade * 1.05f,
                    Mathf.Max(pixels[ly * size + lx].a, alpha));
            }
        }

        tex.SetPixels(pixels);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        tex.Apply();
        return tex;
    }

    Texture2D CreateSkyCanopyTexture()
    {
        return CreateSmokePuffTexture();
    }

    Texture2D CreateSmokePuffTexture()
    {
        const int size = 256;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[size * size];

        // Low-frequency value-noise fields drive the ragged silhouette. No smooth
        // radial falloff survives to the edge, so a billboard never reads as a disc.
        const int lattice = 16;
        int latN = size / lattice + 2;
        float[,] n1 = new float[latN, latN];
        float[,] n2 = new float[latN, latN];
        for (int gy = 0; gy < latN; gy++)
        {
            for (int gx = 0; gx < latN; gx++)
            {
                n1[gy, gx] = Random.value;
                n2[gy, gx] = 0.5f + 0.5f * Mathf.Sin((gy * 17 + gx * 13) * 1.7f + Random.value * 0.9f);
            }
        }

        float Interp(float[,] grid, float x, float y)
        {
            float fx = Mathf.Clamp(x / lattice, 0f, latN - 2f);
            float fy = Mathf.Clamp(y / lattice, 0f, latN - 2f);
            int ix = (int)fx;
            int iy = (int)fy;
            float tx = fx - ix;
            float ty = fy - iy;
            float a = Mathf.Lerp(grid[iy, ix], grid[iy, ix + 1], tx);
            float b = Mathf.Lerp(grid[iy + 1, ix], grid[iy + 1, ix + 1], tx);
            return Mathf.Lerp(a, b, ty);
        }

        // Irregular asymmetrical lobes clustered loosely around the center. Every
        // lobe is an ellipse with random orientation/aspect/power; the union is an
        // organic smoke rag, not a radial disc.
        const int blobs = 13;
        float[] bx = new float[blobs];
        float[] by = new float[blobs];
        float[] br = new float[blobs];
        float[] asp = new float[blobs];
        float[] ang = new float[blobs];
        float[] pw = new float[blobs];
        for (int b = 0; b < blobs; b++)
        {
            bx[b] = size * (0.5f + Random.Range(-0.18f, 0.18f));
            by[b] = size * (0.5f + Random.Range(-0.18f, 0.18f));
            br[b] = Random.Range(38f, 72f);
            asp[b] = Random.Range(1.1f, 2.2f);
            ang[b] = Random.Range(0f, Mathf.PI);
            pw[b] = Random.Range(1.4f, 3.2f);
        }

        for (int py = 0; py < size; py++)
        {
            for (int px = 0; px < size; px++)
            {
                float acc = 0f;
                for (int b = 0; b < blobs; b++)
                {
                    float dx = px - bx[b];
                    float dy = (py - by[b]) / asp[b];
                    float c = Mathf.Cos(ang[b]);
                    float s = Mathf.Sin(ang[b]);
                    float rx = dx * c - dy * s;
                    float ry = dx * s + dy * c;
                    float d = (rx * rx + ry * ry) / (br[b] * br[b]);
                    if (d < 1f)
                    {
                        float fall = Mathf.Pow(1f - d, pw[b]);
                        if (fall > acc) acc = fall;
                    }
                }

                float n = 0.72f * Interp(n1, px, py) + 0.28f * Interp(n2, px + 30, py + 17);
                float a = acc * (0.42f + 0.58f * n);
                if (n < 0.28f && a > 0.14f) a *= 0.2f;

                float edge = Mathf.Min(
                    Mathf.Clamp01(px / 12f),
                    Mathf.Min(
                        Mathf.Clamp01(py / 12f),
                        Mathf.Min(
                            Mathf.Clamp01((size - 1 - px) / 12f),
                            Mathf.Clamp01((size - 1 - py) / 12f))));
                a *= edge;

                if (a <= 0.003f) continue;

                float g = 0.80f + 0.08f * n;
                pixels[py * size + px] = new Color(g, g * 0.985f, g * 1.045f, a);
            }
        }

        tex.SetPixels(pixels);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        tex.Apply();
        return tex;
    }

    Bounds ComputeHouseBounds()
    {
        GameObject house = GameObject.Find("VOLh1 1") ?? GameObject.Find("House");
        if (house == null)
            return new Bounds(new Vector3(-52.722f, 25f, 130.841f), new Vector3(140f, 25f, 140f));

        Bounds b = new Bounds();
        bool any = false;
        Renderer[] renderers = house.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (!renderers[i].enabled) continue;
            if (!any) { b = renderers[i].bounds; any = true; }
            else b.Encapsulate(renderers[i].bounds);
        }

        Collider[] colliders = house.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            if (!colliders[i].enabled) continue;
            if (!any) { b = colliders[i].bounds; any = true; }
            else b.Encapsulate(colliders[i].bounds);
        }

        if (!any)
            return new Bounds(house.transform.position + Vector3.up * 20f, new Vector3(140f, 25f, 140f));

        return b;
    }

    #endregion

    #region Atmosphere

    void CacheAtmosphere()
    {
        Light[] lights = FindObjectsOfType<Light>();
        for (int i = 0; i < lights.Length; i++)
        {
            if (lights[i].type == LightType.Directional)
            {
                sunLight = lights[i];
                break;
            }
        }
        if (sunLight != null)
        {
            sunIntensity0 = sunLight.intensity;
            sunColor0 = sunLight.color;
        }
        ambientLight0 = RenderSettings.ambientLight;
        ambientSky0 = RenderSettings.ambientSkyColor;
        ambientEquator0 = RenderSettings.ambientEquatorColor;
        ambientGround0 = RenderSettings.ambientGroundColor;
        fogEnabled0 = RenderSettings.fog;
        fogColor0 = RenderSettings.fogColor;
        fogMode0 = RenderSettings.fogMode;
        fogDensity0 = RenderSettings.fogDensity;
        fogStart0 = RenderSettings.fogStartDistance;
        fogEnd0 = RenderSettings.fogEndDistance;

        skyboxMat0 = RenderSettings.skybox;
        if (skyboxMat0 != null)
        {
            hasSkyExposure = skyboxMat0.HasProperty("_Exposure");
            hasSkyTint = skyboxMat0.HasProperty("_SkyTint");
            hasSkyThickness = skyboxMat0.HasProperty("_AtmosphereThickness");
            hasSkyGround = skyboxMat0.HasProperty("_GroundColor");
            if (hasSkyExposure) skyExposure0 = skyboxMat0.GetFloat("_Exposure");
            if (hasSkyTint) skyTint0 = skyboxMat0.GetColor("_SkyTint");
            if (hasSkyThickness) skyThickness0 = skyboxMat0.GetFloat("_AtmosphereThickness");
            if (hasSkyGround) skyGround0 = skyboxMat0.GetColor("_GroundColor");
        }
    }

    void ApplyAshAtmosphere()
    {
        if (AtmosphereActive) return;
        AtmosphereActive = true;

        if (sunLight != null)
        {
            sunLight.intensity = Mathf.Max(0.15f, sunIntensity0 * TransitionBaselineSunFactor);
            sunLight.color = Color.Lerp(sunColor0, SunDimColor, 0.35f);
        }

        SetAmbient(new Color(TransitionBaselineAmbient, TransitionBaselineAmbient, TransitionBaselineAmbient));

        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = fogColor;
        RenderSettings.fogDensity = fogDensityStart;

        ApplySky(true);

        atmosphereTransitionRoutine = StartCoroutine(DarkenAtmosphereRoutine());
    }

    IEnumerator DarkenAtmosphereRoutine()
    {
        float duration = Mathf.Max(0.25f, atmosphereTransitionSeconds);
        float t = 0f;

        float sunStart = sunLight != null ? sunLight.intensity : 0f;
        float sunEnd = Mathf.Max(0.15f, sunIntensity0 * sunDimFactor);
        Color tintStart = sunLight != null ? sunLight.color : sunColor0;
        Color tintEnd = Color.Lerp(sunColor0, SunDimColor, 0.55f);

        Color ambientStart = new Color(TransitionBaselineAmbient, TransitionBaselineAmbient, TransitionBaselineAmbient);
        Color ambientEnd = new Color(ambientDimFactor, ambientDimFactor, ambientDimFactor * 1.15f);

        float fogStart = RenderSettings.fogDensity;
        float fogEnd = fogDensity;

        while (t < duration)
        {
            t += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / duration));

            if (sunLight != null)
            {
                sunLight.intensity = Mathf.Lerp(sunStart, sunEnd, k);
                sunLight.color = Color.Lerp(tintStart, tintEnd, k);
            }
            SetAmbient(Color.Lerp(ambientStart, ambientEnd, k));
            RenderSettings.fogDensity = Mathf.Lerp(fogStart, fogEnd, k);
            ApplySky(false, k);
            yield return null;
        }

        if (sunLight != null)
        {
            sunLight.intensity = sunEnd;
            sunLight.color = tintEnd;
        }
        SetAmbient(ambientEnd);
        RenderSettings.fogDensity = fogEnd;
        ApplySky(false, 1f);
        atmosphereTransitionRoutine = null;
    }

    void SetAmbient(Color c)
    {
        if (RenderSettings.ambientMode == AmbientMode.Flat)
        {
            RenderSettings.ambientLight = ambientLight0 * c;
        }
        else
        {
            RenderSettings.ambientSkyColor = ambientSky0 * c;
            RenderSettings.ambientEquatorColor = ambientEquator0 * c * 0.8f;
            RenderSettings.ambientGroundColor = ambientGround0 * c * 0.6f;
        }
    }

    void ApplySky(bool baseline, float t = 1f)
    {
        if (skyboxMat0 == null) return;
        if (hasSkyExposure && skyExposure0 > 0f)
            skyboxMat0.SetFloat("_Exposure", skyExposure0 * Mathf.Lerp(0.8f, skyExposureFactor, baseline ? 0f : t));
        if (hasSkyTint)
        {
            Color eased = baseline ? Color.Lerp(skyTint0, skyTint, 0.5f) : Color.Lerp(skyTint0, skyTint, t);
            skyboxMat0.SetColor("_SkyTint", eased);
        }
        if (hasSkyThickness)
            skyboxMat0.SetFloat("_AtmosphereThickness", skyThickness0 * Mathf.Lerp(0.8f, skyAtmosphereThicknessFactor, baseline ? 0f : t));
        if (hasSkyGround)
        {
            Color eased = baseline ? Color.Lerp(skyGround0, skyGroundColor, 0.5f) : Color.Lerp(skyGround0, skyGroundColor, t);
            skyboxMat0.SetColor("_GroundColor", eased);
        }
    }

    void RestoreAtmosphere()
    {
        if (!AtmosphereActive) return;
        AtmosphereActive = false;
        if (atmosphereTransitionRoutine != null)
        {
            StopCoroutine(atmosphereTransitionRoutine);
            atmosphereTransitionRoutine = null;
        }

        if (sunLight != null)
        {
            sunLight.intensity = sunIntensity0;
            sunLight.color = sunColor0;
        }

        RenderSettings.ambientLight = ambientLight0;
        RenderSettings.ambientSkyColor = ambientSky0;
        RenderSettings.ambientEquatorColor = ambientEquator0;
        RenderSettings.ambientGroundColor = ambientGround0;

        RenderSettings.fog = fogEnabled0;
        RenderSettings.fogColor = fogColor0;
        RenderSettings.fogMode = fogMode0;
        RenderSettings.fogDensity = fogDensity0;
        RenderSettings.fogStartDistance = fogStart0;
        RenderSettings.fogEndDistance = fogEnd0;

        if (skyboxMat0 != null)
        {
            if (hasSkyExposure && skyExposure0 > 0f) skyboxMat0.SetFloat("_Exposure", skyExposure0);
            if (hasSkyTint) skyboxMat0.SetColor("_SkyTint", skyTint0);
            if (hasSkyThickness) skyboxMat0.SetFloat("_AtmosphereThickness", skyThickness0);
            if (hasSkyGround) skyboxMat0.SetColor("_GroundColor", skyGround0);
        }
    }

    #endregion

    Color WithAlphaMul(Color c, float a)
    {
        c.a = a;
        return c;
    }
}