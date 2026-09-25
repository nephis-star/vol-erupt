using System.Collections;
using UnityEngine;

public class CameraShake : MonoBehaviour
{
    [Tooltip("Duration in seconds.")]
    public float defaultDuration = 5f;
    [Tooltip("Shake strength (0.15 - 0.35 works well).")]
    public float strength = 0.25f;
    [Tooltip("Shake frequency (jitter speed).")]
    public float frequency = 18f;

    public bool IsShaking { get; private set; }

    Transform t;
    Vector3 restPosition;

    void Awake()
    {
        t = transform;
        restPosition = t.localPosition;
    }

    void OnEnable()
    {
        if (t == null) t = transform;
        if (restPosition == Vector3.zero && t != null) restPosition = t.localPosition;
        IsShaking = false;
        t.localPosition = restPosition;
    }

    public void Shake()
    {
        Shake(defaultDuration, strength);
    }

    public void Shake(float duration, float shakeStrength)
    {
        StopAllCoroutines();
        StartCoroutine(ShakeRoutine(duration, shakeStrength));
    }

    public void ShakeSeismic(float duration)
    {
        StopAllCoroutines();
        StartCoroutine(SeismicRoutine(duration));
    }

    IEnumerator SeismicRoutine(float duration)
    {
        IsShaking = true;
        t.localPosition = restPosition;
        float start = Time.time;
        float end = start + duration;

        while (Time.time < end)
        {
            float progress = Mathf.Clamp01((Time.time - start) / duration);
            float rampIn = Mathf.Clamp01(progress / 0.25f);
            float rampOut = 1f - Mathf.Clamp01((progress - 0.75f) / 0.25f);
            float envelope = Mathf.Min(rampIn, rampOut);
            float intensity = strength * (0.25f + 0.75f * envelope);

            float nx = Mathf.PerlinNoise(Time.time * frequency, 0f) * 2f - 1f;
            float ny = Mathf.PerlinNoise(0f, Time.time * frequency) * 2f - 1f;
            float nz = Mathf.PerlinNoise(Time.time * frequency, Time.time * frequency) * 2f - 1f;

            t.localPosition = restPosition + new Vector3(nx, ny, nz) * intensity * 0.1f;
            yield return null;
        }

        t.localPosition = restPosition;
        IsShaking = false;
    }

    IEnumerator ShakeRoutine(float duration, float shakeStrength)
    {
        IsShaking = true;
        t.localPosition = restPosition;
        float end = Time.time + duration;

        while (Time.time < end)
        {
            float progress = 1f - Mathf.Clamp01((end - Time.time) / duration);
            float intensity = Mathf.Lerp(shakeStrength, 0f, Mathf.Clamp01(progress * 1.2f));
            float nx = Mathf.PerlinNoise(Time.time * frequency, 0f) * 2f - 1f;
            float ny = Mathf.PerlinNoise(0f, Time.time * frequency) * 2f - 1f;
            float nz = Mathf.PerlinNoise(Time.time * frequency, Time.time * frequency) * 2f - 1f;

            t.localPosition = restPosition + new Vector3(nx, ny, nz) * intensity * 0.1f;
            yield return null;
        }

        t.localPosition = restPosition;
        IsShaking = false;
    }
}