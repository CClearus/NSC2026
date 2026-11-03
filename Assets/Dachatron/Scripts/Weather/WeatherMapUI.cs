using UnityEngine;
using UnityEngine.UI;

// Weather radar panel. Press G to show/hide. Starts itself, no scene setup needed.
// The map is drawn by the Dachatron/WeatherRadar shader (Resources/WeatherRadar material); the player is the red dot.
public class WeatherMapUI : MonoBehaviour
{
    static readonly int PingPhaseId = Shader.PropertyToID("_PingPhase");
    static readonly int WeatherTimeId = Shader.PropertyToID("_WeatherTime");

    public KeyCode toggleKey = KeyCode.G;
    [Tooltip("Gap to the left / top / bottom edge of the screen (reference pixels at 1080p)")]
    public float margin = 40f;

    [Header("Radar frames")]
    [Tooltip("Seconds between radar updates. Real radar loops step a few times per second, they don't glide.")]
    public float radarFrameInterval = 0.5f;
    [Tooltip("How much weather happens per radar frame (in the shader's time units)")]
    public float weatherPerFrame = 1.5f;

    [Header("Ping")]
    public float pingInterval = 1.6f;
    [Range(0f, 1f)] public float beepVolume = 0.25f;
    public float beepPitch = 1250f;

    static WeatherMapUI instance;
    GameObject panel;
    Material material;
    AudioSource beep;
    float openedAt;
    int lastPing = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (instance != null) return;
        var go = new GameObject("WeatherMap");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<WeatherMapUI>();
    }

    void Awake()
    {
        var source = Resources.Load<Material>("WeatherRadar");
        if (source == null)
        {
            Debug.LogWarning("[WeatherMap] Resources/WeatherRadar material is missing.");
            enabled = false;
            return;
        }
        material = new Material(source);

        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 1f;

        panel = new GameObject("Radar", typeof(RectTransform));
        panel.transform.SetParent(transform, false);
        // big square on the left, full height minus the margin; the right side stays free
        var rect = (RectTransform)panel.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 0.5f);
        rect.anchoredPosition = new Vector2(margin, 0f);
        float side = scaler.referenceResolution.y - margin * 2f;
        rect.sizeDelta = new Vector2(side, side);
        var image = panel.AddComponent<RawImage>();
        image.material = material;
        image.raycastTarget = false;

        beep = gameObject.AddComponent<AudioSource>();
        beep.playOnAwake = false;
        beep.spatialBlend = 0f;
        beep.clip = MakeBeep(beepPitch);

        panel.SetActive(false);
    }

    void OnDestroy()
    {
        if (material != null) Destroy(material);
        if (beep != null && beep.clip != null) Destroy(beep.clip);
    }

    void Update()
    {
        // weather runs on game time (keeps going while the map is closed, stops when paused), shown in discrete
        // radar frames: each frame jumps the weather forward a step
        int frame = Mathf.FloorToInt(Time.time / Mathf.Max(0.05f, radarFrameInterval));
        material.SetFloat(WeatherTimeId, frame * weatherPerFrame);

        if (!DevConsole.IsOpen && Input.GetKeyDown(toggleKey))
        {
            bool open = !panel.activeSelf;
            panel.SetActive(open);
            if (open) { openedAt = Time.unscaledTime; lastPing = -1; }
        }
        if (!panel.activeSelf) return;

        // one ping per interval: the shader's glow flash and the beep share the same clock
        float t = (Time.unscaledTime - openedAt) / pingInterval;
        int ping = Mathf.FloorToInt(t);
        material.SetFloat(PingPhaseId, t - ping);
        if (ping != lastPing)
        {
            lastPing = ping;
            beep.PlayOneShot(beep.clip, beepVolume);
        }
    }

    // short sine blip with a quick attack and fade, plus a soft octave for a "radar" tone
    static AudioClip MakeBeep(float frequency)
    {
        const int rate = 44100;
        const float length = 0.12f;
        int n = Mathf.RoundToInt(rate * length);
        var data = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)rate;
            float env = Mathf.Min(1f, t / 0.005f) * Mathf.Exp(-t * 28f);
            data[i] = env * (Mathf.Sin(2f * Mathf.PI * frequency * t) * 0.8f
                           + Mathf.Sin(2f * Mathf.PI * frequency * 2f * t) * 0.2f);
        }
        var clip = AudioClip.Create("RadarBeep", n, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
