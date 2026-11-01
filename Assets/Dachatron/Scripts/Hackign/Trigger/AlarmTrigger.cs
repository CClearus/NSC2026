  using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// On trigger enter: shakes the camera, flickers a light, and flashes emission
/// on a set of renderers. Each renderer flashes with its own randomized phase/timing
/// (via MaterialPropertyBlock, not the shared material) so they don't all pulse in sync.
/// </summary>
public class AlarmTrigger : MonoBehaviour
{
    [Header("Trigger Filter")]
    [Tooltip("Only trigger when this tag walks into the collider (leave empty to allow anything).")]
    public string playerTag = "Player";
    public bool triggerOnce = true;

    [Header("Camera Shake")]
    public Transform cameraTransform;
    public float shakeDuration = 1.5f;
    public float shakeMagnitude = 0.15f;

    [System.Serializable]
    public class LightFixture
    {
        [Tooltip("Optional real Light component for this ceiling block.")]
        public Light light;
        [Tooltip("Renderers whose emission belongs to this same fixture (flashes in sync with the Light above, but out of sync with other fixtures).")]
        public Renderer[] renderers;
    }

    [Header("Light Flicker")]
    [Tooltip("Each fixture (ceiling light block) flashes on its own randomized phase/speed so they don't all pulse together.")]
    public LightFixture[] fixtures;
    public float lightMinIntensity = 0f;
    public float lightMaxIntensity = 3f;
    public Color emissionColor = Color.red;
    public float emissionMinIntensity = 0f;
    public float emissionMaxIntensity = 4f;

    [Header("Timing")]
    public float effectDuration = 3f;
    [Tooltip("Base flashes per second.")]
    public float flashSpeed = 4f;
    [Tooltip("Extra random offset added per-renderer/light so flashes desync instead of all pulsing together.")]
    public float perObjectPhaseJitter = 1f;
    [Tooltip("Extra random speed variance per-renderer so flashes don't stay in lockstep over time.")]
    public float perObjectSpeedJitter = 1.5f;

    private bool _hasTriggered;
    private MaterialPropertyBlock _mpb;
    private float[] _fixturePhase;
    private float[] _fixtureSpeed;
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    void OnTriggerEnter(Collider other)
    {
        if (!string.IsNullOrEmpty(playerTag) && !other.CompareTag(playerTag))
            return;

        if (triggerOnce && _hasTriggered)
            return;

        _hasTriggered = true;
        StartCoroutine(RunAlarm());
    }

    private IEnumerator RunAlarm()
    {
        _mpb ??= new MaterialPropertyBlock();

        if (fixtures != null && fixtures.Length > 0)
        {
            _fixturePhase = new float[fixtures.Length];
            _fixtureSpeed = new float[fixtures.Length];
            for (int i = 0; i < fixtures.Length; i++)
            {
                _fixturePhase[i] = Random.Range(0f, Mathf.PI * 2f) + Random.Range(0f, perObjectPhaseJitter);
                _fixtureSpeed[i] = flashSpeed + Random.Range(-perObjectSpeedJitter, perObjectSpeedJitter);
            }
        }

        Vector3 originalCamPos = cameraTransform != null ? cameraTransform.localPosition : Vector3.zero;

        float elapsed = 0f;
        while (elapsed < effectDuration)
        {
            elapsed += Time.deltaTime;

            if (cameraTransform != null)
            {
                float damper = 1f - Mathf.Clamp01(elapsed / effectDuration);
                Vector3 offset = Random.insideUnitSphere * shakeMagnitude * damper;
                cameraTransform.localPosition = originalCamPos + offset;
            }

            if (fixtures != null)
            {
                for (int i = 0; i < fixtures.Length; i++)
                {
                    LightFixture fixture = fixtures[i];
                    if (fixture == null) continue;

                    float t = (Mathf.Sin(elapsed * _fixtureSpeed[i] + _fixturePhase[i]) + 1f) * 0.5f;

                    if (fixture.light != null)
                        fixture.light.intensity = Mathf.Lerp(lightMinIntensity, lightMaxIntensity, t);

                    if (fixture.renderers != null)
                    {
                        float intensity = Mathf.Lerp(emissionMinIntensity, emissionMaxIntensity, t);
                        foreach (var r in fixture.renderers)
                        {
                            if (r == null) continue;
                            r.GetPropertyBlock(_mpb);
                            _mpb.SetColor(EmissionColorId, emissionColor * intensity);
                            r.SetPropertyBlock(_mpb);
                        }
                    }
                }
            }

            yield return null;
        }

        if (cameraTransform != null)
            cameraTransform.localPosition = originalCamPos;

        if (fixtures != null)
        {
            foreach (var fixture in fixtures)
            {
                if (fixture == null) continue;

                if (fixture.light != null)
                    fixture.light.intensity = lightMinIntensity;

                if (fixture.renderers != null)
                {
                    foreach (var r in fixture.renderers)
                    {
                        if (r == null) continue;
                        r.GetPropertyBlock(_mpb);
                        _mpb.SetColor(EmissionColorId, Color.black);
                        r.SetPropertyBlock(_mpb);
                    }
                }
            }
        }
    }
}
