using UnityEngine;
using UnityEngine.UI;

// Scrolling ECG-style heartbeat trace, drawn into a RawImage on a UI panel.
//
//  - Beat RATE is driven by TensionManager (global stat) - higher tension = faster beeping.
//  - Line COLOR is driven by PlayerHealth - green at full health, red near 0, flatline on death.
//  - Rate/amplitude jitter a little every beat so it never looks perfectly metronomic,
//    even while standing still - like a real pulse.
//
// Attach this to a UI GameObject that has a RawImage component (an empty child
// of your health panel works well). See the setup notes at the bottom of this
// file / the chat for wiring instructions.
[RequireComponent(typeof(RawImage))]
public class HeartbeatMonitorUI : MonoBehaviour
{
    [Header("Data Sources")]
    [Tooltip("Leave empty to auto-find on this object or its parents.")]
    public PlayerHealth playerHealth;
    [Tooltip("Leave empty to use TensionManager.Instance.")]
    public TensionManager tensionManager;

    [Header("Texture")]
    public int textureWidth = 256;
    public int textureHeight = 64;
    [Range(1, 8)] public int lineThickness = 2;
    public Color backgroundColor = new Color(0f, 0f, 0f, 0.35f);

    [Header("Beat Rate (BPM)")]
    [Tooltip("Heart rate at 0 tension.")]
    public float minBPM = 60f;
    [Tooltip("Heart rate at max (100) tension.")]
    public float maxBPM = 160f;

    [Header("Realism / Variability")]
    [Tooltip("How much each beat's length can randomly drift from the target BPM (0.12 = up to ±12%). Real pulses aren't a perfect metronome.")]
    [Range(0f, 0.4f)] public float rateVariability = 0.12f;
    [Tooltip("How much each beat's spike height can randomly vary (0.1 = up to ±10%).")]
    [Range(0f, 0.5f)] public float amplitudeVariability = 0.1f;
    [Tooltip("Small continuous wobble on the baseline so the line is never perfectly flat between beats.")]
    [Range(0f, 0.1f)] public float baselineNoiseAmount = 0.015f;
    public float baselineNoiseSpeed = 2.5f;

    [Header("Scroll")]
    [Tooltip("How many texture columns scroll past per second.")]
    public float scrollSpeed = 80f;

    [Header("Colors")]
    public Color healthyColor = new Color(0.2f, 1f, 0.3f);
    public Color criticalColor = new Color(1f, 0.15f, 0.1f);
    [Tooltip("Health fraction (0-1) at/below which the line is fully criticalColor.")]
    [Range(0f, 1f)] public float lowHealthThreshold = 0.3f;

    [Header("Leading Edge Flash")]
    [Tooltip("Glowing pulse at the right edge - where each new beat writes in - synced to the spike.")]
    public bool showLeadingFlash = true;
    public Color flashColor = Color.white;
    [Tooltip("How quickly the flash fades out after each beat.")]
    public float flashDecaySpeed = 4f;
    [Tooltip("Glow radius in pixels at full brightness.")]
    public int flashMaxRadius = 7;

    // (phase 0-1, amplitude -1..1) keyframes for one beat, linearly interpolated
    // between points - sharp straight segments read as a proper ECG spike
    // instead of a rounded blip.
    private static readonly Vector2[] waveKeyframes =
    {
        new Vector2(0.00f, 0f),
        new Vector2(0.10f, 0.06f),   // P wave
        new Vector2(0.16f, 0f),
        new Vector2(0.20f, 0f),
        new Vector2(0.22f, -0.12f),  // Q dip
        new Vector2(0.25f, 1.00f),   // R spike
        new Vector2(0.28f, -0.45f),  // S dip
        new Vector2(0.32f, 0f),
        new Vector2(0.48f, 0.18f),   // T wave
        new Vector2(0.58f, 0f),
        new Vector2(1.00f, 0f),
    };

    // Phase (matches the R spike keyframe above) at which the leading-edge flash fires.
    private const float FlashTriggerPhase = 0.25f;

    private RawImage rawImage;
    private Texture2D texture;
    private Color32[] pixels;
    private float[] samples;   // one waveform sample per column, roughly -1..1
    private float scrollAccumulator;
    private float beatClock;          // seconds into the current beat
    private float currentBeatPeriod = -1f;
    private float currentBeatAmplitude = 1f;
    private float prevPhase;
    private float edgeFlash;          // 0-1, decays after each beat's spike

    private void Awake()
    {
        rawImage = GetComponent<RawImage>();

        if (playerHealth == null) playerHealth = GetComponentInParent<PlayerHealth>();
        if (tensionManager == null) tensionManager = TensionManager.Instance;

        BuildTexture();
    }

    private void BuildTexture()
    {
        texture = new Texture2D(textureWidth, textureHeight, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;
        pixels = new Color32[textureWidth * textureHeight];
        samples = new float[textureWidth];
        rawImage.texture = texture;
    }

    private void Update()
    {
        if (texture == null) return;

        float health01 = playerHealth != null ? playerHealth.Health01 : 1f;
        float tension01 = tensionManager != null ? tensionManager.Tension01 : 0f;
        bool isDead = playerHealth != null && playerHealth.IsDead;

        float bpm = Mathf.Lerp(minBPM, maxBPM, tension01);
        float targetBeatPeriod = 60f / Mathf.Max(1f, bpm);

        AdvanceSamples(targetBeatPeriod, isDead);
        edgeFlash = Mathf.Max(0f, edgeFlash - flashDecaySpeed * Time.deltaTime);

        Color lineColor = Color.Lerp(criticalColor, healthyColor, RemapHealth(health01));
        Redraw(lineColor);
    }

    private void AdvanceSamples(float targetBeatPeriod, bool isDead)
    {
        if (currentBeatPeriod < 0f) StartNewBeat(targetBeatPeriod);

        // Advance the scroll by however many whole columns worth of time passed
        // this frame, so trace speed stays correct regardless of framerate.
        scrollAccumulator += scrollSpeed * Time.deltaTime;
        int columnsToAdvance = Mathf.FloorToInt(scrollAccumulator);
        scrollAccumulator -= columnsToAdvance;

        for (int i = 0; i < columnsToAdvance; i++)
        {
            beatClock += 1f / scrollSpeed;
            if (beatClock >= currentBeatPeriod)
            {
                beatClock -= currentBeatPeriod;
                StartNewBeat(targetBeatPeriod);
            }

            float phase = beatClock / currentBeatPeriod;

            // Fire the leading-edge flash right as the spike (the loud part of the
            // beat) writes in at the right edge, not at the quiet start of the cycle.
            if (!isDead && phase >= FlashTriggerPhase && prevPhase < FlashTriggerPhase)
            {
                edgeFlash = 1f;
            }
            prevPhase = phase;

            float noise = (Mathf.PerlinNoise(Time.time * baselineNoiseSpeed, 0.37f) - 0.5f) * 2f * baselineNoiseAmount;
            float sample = isDead ? 0f : HeartbeatWave(phase) * currentBeatAmplitude + noise;

            for (int x = 0; x < textureWidth - 1; x++) samples[x] = samples[x + 1];
            samples[textureWidth - 1] = sample;
        }
    }

    // Rolls a fresh, slightly-off-target length/height for the beat that's about
    // to play - this is what keeps the trace from looking perfectly mechanical.
    private void StartNewBeat(float targetBeatPeriod)
    {
        currentBeatPeriod = targetBeatPeriod * (1f + Random.Range(-rateVariability, rateVariability));
        currentBeatAmplitude = 1f + Random.Range(-amplitudeVariability, amplitudeVariability);
    }

    // Maps health fraction so the line is fully "critical" at/under lowHealthThreshold
    // and fully "healthy" at 1, instead of spreading the color change across 0-1.
    private float RemapHealth(float health01)
    {
        if (lowHealthThreshold >= 1f) return health01;
        return Mathf.InverseLerp(lowHealthThreshold, 1f, health01);
    }

    private float HeartbeatWave(float phase01)
    {
        for (int i = 0; i < waveKeyframes.Length - 1; i++)
        {
            Vector2 a = waveKeyframes[i];
            Vector2 b = waveKeyframes[i + 1];
            if (phase01 >= a.x && phase01 <= b.x)
            {
                float t = (b.x - a.x) <= 0f ? 0f : (phase01 - a.x) / (b.x - a.x);
                return Mathf.Lerp(a.y, b.y, t);
            }
        }
        return 0f;
    }

    private void Redraw(Color32 lineColor)
    {
        Color32 bg = backgroundColor;
        for (int i = 0; i < pixels.Length; i++) pixels[i] = bg;

        int midY = textureHeight / 2;
        int amplitude = textureHeight / 2 - 2;
        int prevY = midY + Mathf.RoundToInt(samples[0] * amplitude);

        for (int x = 0; x < textureWidth; x++)
        {
            int y = midY + Mathf.RoundToInt(samples[x] * amplitude);
            PlotLine(x == 0 ? x : x - 1, prevY, x, y, lineColor);
            prevY = y;
        }

        if (showLeadingFlash && edgeFlash > 0.01f)
        {
            int edgeY = midY + Mathf.RoundToInt(samples[textureWidth - 1] * amplitude);
            DrawGlow(textureWidth - 1, edgeY, lineColor, edgeFlash);
        }

        texture.SetPixels32(pixels);
        texture.Apply(false);
    }

    // Soft radial glow blended into the existing pixels (rather than overwriting them),
    // centered on the write edge - deliberately clipped by the texture's right border so
    // it reads as the beat "flashing in" from off-screen.
    private void DrawGlow(int cx, int cy, Color32 baseColor, float strength)
    {
        int radius = Mathf.Max(1, flashMaxRadius);
        Color glow = Color.Lerp(baseColor, flashColor, 0.6f);

        for (int oy = -radius; oy <= radius; oy++)
        {
            int y = cy + oy;
            if (y < 0 || y >= textureHeight) continue;

            for (int ox = -radius; ox <= radius; ox++)
            {
                int x = cx + ox;
                if (x < 0 || x >= textureWidth) continue;

                float dist = Mathf.Sqrt(ox * ox + oy * oy);
                float falloff = Mathf.Clamp01(1f - dist / radius);
                float alpha = falloff * falloff * strength;
                if (alpha <= 0.02f) continue;

                int idx = y * textureWidth + x;
                pixels[idx] = Color32.Lerp(pixels[idx], glow, alpha);
            }
        }
    }

    // Simple Bresenham line so consecutive samples read as a continuous curve
    // instead of a scatter of dots.
    private void PlotLine(int x0, int y0, int x1, int y1, Color32 color)
    {
        int dx = Mathf.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
        int dy = -Mathf.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1;
        int err = dx + dy;

        while (true)
        {
            PlotThick(x0, y0, color);
            if (x0 == x1 && y0 == y1) break;
            int e2 = 2 * err;
            if (e2 >= dy) { err += dy; x0 += sx; }
            if (e2 <= dx) { err += dx; y0 += sy; }
        }
    }

    private void PlotThick(int x, int y, Color32 color)
    {
        int half = lineThickness / 2;
        for (int oy = -half; oy <= half; oy++) SetPixelSafe(x, y + oy, color);
    }

    private void SetPixelSafe(int x, int y, Color32 color)
    {
        if (x < 0 || x >= textureWidth || y < 0 || y >= textureHeight) return;
        pixels[y * textureWidth + x] = color;
    }
}
