using UnityEngine;

// Drives the global "dryness" of every Dachatron/Wet Lit material.
// wetness 1 = materials look as wet as their own settings, 0 = everything dry.
[ExecuteAlways]
public class WetnessController : MonoBehaviour
{
    static readonly int GlobalDrynessId = Shader.PropertyToID("_GlobalDryness");

    [Range(0f, 1f)] public float wetness = 1f;

    [Header("Optional drying over time (play mode)")]
    public bool autoDry = false;
    [Tooltip("Wetness lost per second")]
    public float dryPerSecond = 0.01f;

    void OnEnable() => Apply();

    void OnDisable() => Shader.SetGlobalFloat(GlobalDrynessId, 0f);

    void OnValidate() => Apply();

    void Update()
    {
        if (autoDry && Application.isPlaying)
            wetness = Mathf.Max(0f, wetness - dryPerSecond * Time.deltaTime);
        Apply();
    }

    // Call from rain / weather scripts.
    public void SetWetness(float value)
    {
        wetness = Mathf.Clamp01(value);
        Apply();
    }

    void Apply() => Shader.SetGlobalFloat(GlobalDrynessId, 1f - Mathf.Clamp01(wetness));
}
