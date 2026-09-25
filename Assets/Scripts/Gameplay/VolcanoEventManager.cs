using System.Collections;
using UnityEngine;

public class VolcanoEventManager : MonoBehaviour
{
    public static VolcanoEventManager Instance { get; private set; }

    [Header("Timing")]
    public float eruptionDelay = 10f;
    public float earthquakeDuration = 5f;
    public bool autoStart = true;

    [Header("References (assigned in scene)")]
    public CameraShake cameraShake;
    public ParticleSystem volcanoSmokeEffect;
    public ParticleSystem ashFallEffect;

    [Header("Audio (optional, empty fields are fine)")]
    public AudioSource audioSource;
    public AudioClip earthquakeSound;
    public AudioClip volcanoRumbleSound;
    public AudioClip warningSound;

    public bool HasErupted { get; private set; }
    public bool effectsActive { get; private set; }
    public float EruptionStartTime { get; private set; } = -1f;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        if (cameraShake == null) cameraShake = FindObjectOfType<CameraShake>();
        if (audioSource == null) audioSource = GetComponent<AudioSource>();
        if (autoStart) StartCoroutine(GameSequence());
    }

    IEnumerator GameSequence()
    {
        yield return new WaitForSeconds(eruptionDelay);

        // Eruption begins: visual first, NO instant on-screen text.
        GameState current = GameStateManager.Instance != null ? GameStateManager.Instance.CurrentState : GameState.Exploration;
        if (current == GameState.Completed) yield break;
        HasErupted = true;
        EruptionStartTime = Time.time;
        if (GameStateManager.Instance != null) GameStateManager.Instance.SetState(GameState.Eruption);
        StartEffects();

        if (cameraShake != null) cameraShake.Shake(0.6f, cameraShake.strength * 0.4f);

        yield return new WaitForSeconds(0.8f);

        // Earthquake + camera shake + audio.
        if (GameStateManager.Instance != null) GameStateManager.Instance.SetState(GameState.Earthquake);
        if (cameraShake != null) cameraShake.ShakeSeismic(earthquakeDuration);
        PlayClip(earthquakeSound);

        yield return new WaitForSeconds(earthquakeDuration);

        StopEffects();
        yield return new WaitForSeconds(1.2f);

        // On-screen eruption advisory. Home preparation starts from here - no TV visit required.
        WarningUI wu = WarningUI.Instance;
        if (wu != null)
        {
            yield return wu.ShowWarningAwait("VOLCANIC ERUPTION WARNING",
                "Mayon has erupted.\nVolcanic ash may affect the surrounding area.\n\nPrepare your home for volcanic ashfall.",
                5f);
        }
        PlayClip(warningSound);

        if (GameStateManager.Instance != null) GameStateManager.Instance.SetState(GameState.Preparation);

        ObjectiveManager om = ObjectiveManager.Instance;
        if (om != null) om.StartPreparation();
    }

    public void StartEffects()
    {
        effectsActive = true;
        if (volcanoSmokeEffect != null) volcanoSmokeEffect.Play();
        if (ashFallEffect != null) ashFallEffect.Play();
        PlayClip(volcanoRumbleSound);
    }

    public void StopEffects()
    {
        effectsActive = false;
        if (volcanoSmokeEffect != null)
            volcanoSmokeEffect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    public void PlayClip(AudioClip clip)
    {
        if (clip == null || audioSource == null) return;
        audioSource.clip = clip;
        audioSource.Play();
    }
}