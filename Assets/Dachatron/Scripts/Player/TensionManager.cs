using UnityEngine;

// Global "tension" stat - not tied to any one GameObject's health, just an
// ambient danger/stress level that any system (heartbeat UI, music, enemy AI...)
// can read or add to. Access it anywhere via TensionManager.Instance.
public class TensionManager : MonoBehaviour
{
    public static TensionManager Instance { get; private set; }

    [Header("Tension")]
    [Tooltip("Current tension, 0-MaxTension. Read this or Tension01 from other scripts.")]
    public float CurrentTension = 0f;
    public float MaxTension = 100f;

    [Header("Decay")]
    [Tooltip("How much tension drains per second once decay resumes.")]
    public float decayPerSecond = 5f;
    [Tooltip("Seconds to wait after the last AddTension() call before decay resumes.")]
    public float decayDelay = 1.5f;

    private float lastAddTime = -999f;

    // 0-1 fraction, handy for driving UI/heartbeat speed directly.
    public float Tension01 => MaxTension <= 0f ? 0f : Mathf.Clamp01(CurrentTension / MaxTension);

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Update()
    {
        if (CurrentTension > 0f && Time.time - lastAddTime >= decayDelay)
        {
            CurrentTension = Mathf.Max(0f, CurrentTension - decayPerSecond * Time.deltaTime);
        }
    }

    public void AddTension(float amount)
    {
        if (amount == 0f) return;
        lastAddTime = Time.time;
        CurrentTension = Mathf.Clamp(CurrentTension + amount, 0f, MaxTension);
    }

    public void SetTension(float value)
    {
        CurrentTension = Mathf.Clamp(value, 0f, MaxTension);
    }
}
