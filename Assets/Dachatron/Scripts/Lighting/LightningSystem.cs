using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

// Two kinds of lightning, both summonable from the console (` key):
//   /summon light_struck [intensity]  - a strike: a huge white point light flashes and lights up the whole area
//                                       (plus a sky flash). Intensity defaults to strikeIntensity.
//   /summon lighting [strength]        - sky lightning: one patch of cloud lights up from inside, far away.
// Works without setup (creates itself on first use). Drop it in a scene to tweak values or set the strike point.
public class LightningSystem : MonoBehaviour
{
    static readonly int ExposureId = Shader.PropertyToID("_Exposure");
    static readonly int TintId = Shader.PropertyToID("_Tint");

    static LightningSystem instance;

    [Header("Strike (light_struck)")]
    [Tooltip("Where the strike light sits. Empty = above the main camera.")]
    public Transform strikePoint;
    public Vector3 strikeOffsetFromCamera = new Vector3(0f, 12f, 10f);
    [Tooltip("Random sideways spread when using the camera position")]
    public float strikeSpread = 10f;
    public float strikeIntensity = 100f;
    public float strikeRange = 300f;
    public Color strikeColor = new Color(0.85f, 0.9f, 1f);
    [Tooltip("None = lights the whole environment through walls/ceilings. Soft = realistic, only through openings.")]
    public LightShadows strikeShadows = LightShadows.None;
    [Tooltip("How much a strike also flashes the sky (sky flash strength)")]
    public float strikeSkyFlash = 3f;

    [Header("Cloud flash (lighting)")]
    [Tooltip("Brightness of the lit patch of cloud (HDR)")]
    public float cloudFlashStrength = 3f;
    [ColorUsage(false, true)] public Color cloudFlashColor = new Color(0.92f, 0.85f, 1f);
    [Tooltip("Size of the lit patch, in degrees")]
    public Vector2 cloudFlashSize = new Vector2(10f, 22f);
    [Tooltip("Height above the horizon, in degrees")]
    public Vector2 cloudFlashElevation = new Vector2(8f, 35f);
    [Tooltip("How far left/right of where the camera looks, in degrees")]
    public float cloudFlashSpread = 55f;
    [Tooltip("Faint brightening of the whole sky with each cloud flash. 0 = none")]
    public float cloudFlashSkyBleed = 0.15f;

    [Header("Whole-sky flash (part of a strike)")]
    public Color skyFlashTint = Color.white;

    Light strikeLight;
    Material skyInstance, skyOriginal;
    float skyBaseExposure = 1f;
    Color skyBaseTint = Color.gray;
    bool skyHasExposure, skyHasTint;

    readonly List<Flash> strikes = new List<Flash>();
    readonly List<Flash> skyFlashes = new List<Flash>();

    // a glowing patch of cloud: a camera-facing quad far out in the sky
    class CloudGlow
    {
        public GameObject go;
        public Renderer renderer;
        public Flash flash;
        public Vector3 direction;
        public float angularSize, spin;
        public Texture2D texture;
    }

    static readonly int MainTexId = Shader.PropertyToID("_MainTex");
    static readonly int ColorId = Shader.PropertyToID("_Color");
    readonly List<CloudGlow> glows = new List<CloudGlow>();
    readonly Stack<CloudGlow> glowPool = new Stack<CloudGlow>();
    readonly List<GameObject> glowObjects = new List<GameObject>();
    Material cloudMaterial;
    Texture2D[] glowTextures;
    MaterialPropertyBlock glowBlock;

    // A few rapid pulses, each decaying fast, the last one lingering - reads as lightning.
    class Flash
    {
        public float start, peak;
        public readonly List<(float time, float amp, float decay)> pulses = new List<(float, float, float)>();
        public float end;

        public Flash(float peak)
        {
            this.peak = peak;
            start = Time.time;
            int count = Random.Range(2, 5);
            float t = 0f;
            for (int i = 0; i < count; i++)
            {
                bool last = i == count - 1;
                pulses.Add((t, i == 0 ? 1f : Random.Range(0.45f, 0.95f), last ? Random.Range(0.18f, 0.3f) : Random.Range(0.05f, 0.09f)));
                t += Random.Range(0.06f, 0.16f);
            }
            var lastPulse = pulses[pulses.Count - 1];
            end = start + lastPulse.time + lastPulse.decay * 6f;
        }

        public float Evaluate(float now)
        {
            float t = now - start, v = 0f;
            foreach (var p in pulses)
                if (t >= p.time) v = Mathf.Max(v, p.amp * Mathf.Exp(-(t - p.time) / p.decay));
            return v * peak;
        }
    }

    // ------------------------------------------------------------------ console hookup
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void RegisterCommands()
    {
        DevConsole.RegisterSummon("light_struck", "light_struck [intensity]", args =>
        {
            float intensity = args.Length > 0 ? ParseFloat(args[0]) : Instance.strikeIntensity;
            Instance.Strike(intensity);
            DevConsole.Log($"Lightning strike, intensity {intensity}");
        });
        DevConsole.RegisterSummon("lighting", "lighting [strength]", args =>
        {
            float strength = args.Length > 0 ? ParseFloat(args[0]) : Instance.cloudFlashStrength;
            Instance.CloudFlash(strength);
            DevConsole.Log($"Sky lightning, strength {strength}");
        });
    }

    static float ParseFloat(string s)
    {
        if (!float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
            throw new System.ArgumentException($"'{s}' is not a number");
        return v;
    }

    public static LightningSystem Instance
    {
        get
        {
            if (instance == null) instance = FindAnyObjectByType<LightningSystem>();
            if (instance == null) instance = new GameObject("LightningSystem").AddComponent<LightningSystem>();
            return instance;
        }
    }

    // ------------------------------------------------------------------ public API
    public void Strike(float intensity)
    {
        EnsureStrikeLight();
        var cam = Camera.main;
        if (strikePoint != null)
            strikeLight.transform.position = strikePoint.position;
        else if (cam != null)
            strikeLight.transform.position = cam.transform.position + strikeOffsetFromCamera
                + new Vector3(Random.Range(-strikeSpread, strikeSpread), 0f, 0f);

        strikes.Add(new Flash(intensity));
        if (strikeSkyFlash > 0f) SkyFlash(strikeSkyFlash);
    }

    // Whole skybox brightens (used by strikes).
    public void SkyFlash(float strength)
    {
        EnsureSkyInstance();
        skyFlashes.Add(new Flash(strength));
    }

    // One patch of cloud lights up from inside, somewhere in front of the camera.
    public void CloudFlash(float strength)
    {
        var cam = Camera.main;
        if (cam == null) return;
        EnsureCloudAssets();
        if (cloudMaterial == null)
        {
            Debug.LogWarning("[Lightning] Resources/SkyFlash material is missing.");
            return;
        }

        var g = glowPool.Count > 0 ? glowPool.Pop() : CreateGlow();

        Vector3 fwd = cam.transform.forward;
        fwd.y = 0f;
        if (fwd.sqrMagnitude < 1e-4f) fwd = Vector3.forward;
        fwd.Normalize();
        float azimuth = Random.Range(-cloudFlashSpread, cloudFlashSpread);
        float elevation = Random.Range(cloudFlashElevation.x, cloudFlashElevation.y);
        Vector3 right = Vector3.Cross(Vector3.up, fwd);
        g.direction = Quaternion.AngleAxis(azimuth, Vector3.up) * Quaternion.AngleAxis(-elevation, right) * fwd;

        g.angularSize = Random.Range(cloudFlashSize.x, cloudFlashSize.y);
        g.spin = Random.Range(0f, 360f);
        g.texture = glowTextures[Random.Range(0, glowTextures.Length)];
        g.flash = new Flash(strength);
        g.go.SetActive(true);
        glows.Add(g);

        if (cloudFlashSkyBleed > 0f) SkyFlash(strength * cloudFlashSkyBleed);
    }

    // ------------------------------------------------------------------ internals
    void Awake()
    {
        if (instance != null && instance != this) { Destroy(this); return; }
        instance = this;
    }

    void EnsureStrikeLight()
    {
        if (strikeLight != null) return;
        var go = new GameObject("Lightning Strike Light");
        go.transform.SetParent(transform, false);
        strikeLight = go.AddComponent<Light>();
        strikeLight.type = LightType.Point;
        strikeLight.color = strikeColor;
        strikeLight.range = strikeRange;
        strikeLight.shadows = strikeShadows;
        strikeLight.intensity = 0f;
        strikeLight.enabled = false;
    }

    void EnsureSkyInstance()
    {
        // work on a copy so the skybox material asset is never changed
        if (skyInstance != null && RenderSettings.skybox == skyInstance) return;
        skyOriginal = RenderSettings.skybox;
        if (skyOriginal == null) return;

        skyInstance = new Material(skyOriginal) { name = skyOriginal.name + " (lightning)" };
        skyHasExposure = skyInstance.HasProperty(ExposureId);
        skyHasTint = skyInstance.HasProperty(TintId);
        if (skyHasExposure) skyBaseExposure = skyInstance.GetFloat(ExposureId);
        if (skyHasTint) skyBaseTint = skyInstance.GetColor(TintId);
        RenderSettings.skybox = skyInstance;
    }

    void EnsureCloudAssets()
    {
        if (cloudMaterial == null) cloudMaterial = Resources.Load<Material>("SkyFlash");
        if (glowBlock == null) glowBlock = new MaterialPropertyBlock();
        if (glowTextures == null)
        {
            glowTextures = new Texture2D[3];
            for (int i = 0; i < glowTextures.Length; i++) glowTextures[i] = MakeGlowTexture(i);
        }
    }

    CloudGlow CreateGlow()
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = "Cloud Flash";
        Destroy(go.GetComponent<Collider>());
        var r = go.GetComponent<Renderer>();
        r.sharedMaterial = cloudMaterial;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        glowObjects.Add(go);
        return new CloudGlow { go = go, renderer = r };
    }

    // Soft radial glow broken up by cloud-like noise plus a hot core, so it reads as cloud lit from inside.
    static Texture2D MakeGlowTexture(int seed)
    {
        const int size = 128;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "CloudFlashGlow" };
        var px = new Color32[size * size];
        float ox = seed * 17.3f, oy = seed * 41.7f;
        var core = new Vector2(Random.Range(-0.25f, 0.25f), Random.Range(-0.25f, 0.25f));

        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = x / (size - 1f) * 2f - 1f, v = y / (size - 1f) * 2f - 1f;
                float r = Mathf.Sqrt(u * u + v * v);
                float radial = Mathf.Exp(-r * r * 3.2f) * Mathf.Clamp01(1f - r);

                float n = 0f, amp = 0.5f, freq = 2.5f;
                for (int o = 0; o < 4; o++)
                {
                    n += amp * Mathf.PerlinNoise(u * freq + ox, v * freq + oy);
                    amp *= 0.5f;
                    freq *= 2.1f;
                }
                float cloud = Mathf.Pow(Mathf.Clamp01(n / 0.9375f), 1.6f) * 1.6f;
                float d2 = (u - core.x) * (u - core.x) + (v - core.y) * (v - core.y);
                float hot = Mathf.Exp(-d2 * 18f);

                byte b = (byte)(Mathf.Clamp01(radial * (0.3f + 0.9f * cloud) + hot * 0.6f * radial) * 255f);
                px[y * size + x] = new Color32(b, b, b, 255);
            }
        tex.SetPixels32(px);
        tex.Apply();
        return tex;
    }

    void UpdateCloudGlows(float now)
    {
        var cam = Camera.main;
        for (int i = glows.Count - 1; i >= 0; i--)
        {
            var g = glows[i];
            if (now > g.flash.end || cam == null)
            {
                g.go.SetActive(false);
                glows.RemoveAt(i);
                glowPool.Push(g);
                continue;
            }

            float dist = cam.farClipPlane * 0.9f;
            var t = g.go.transform;
            t.position = cam.transform.position + g.direction * dist;
            t.rotation = Quaternion.LookRotation(g.direction) * Quaternion.Euler(0f, 0f, g.spin);
            float s = 2f * dist * Mathf.Tan(g.angularSize * 0.5f * Mathf.Deg2Rad);
            t.localScale = new Vector3(s, s, 1f);

            g.renderer.GetPropertyBlock(glowBlock);
            glowBlock.SetTexture(MainTexId, g.texture);
            glowBlock.SetColor(ColorId, cloudFlashColor * g.flash.Evaluate(now));
            g.renderer.SetPropertyBlock(glowBlock);
        }
    }

    void Update()
    {
        float now = Time.time;
        UpdateCloudGlows(now);

        float strike = Sample(strikes, now);
        if (strikeLight != null)
        {
            strikeLight.intensity = strike;
            strikeLight.enabled = strike > 0.01f;
        }

        if (skyInstance != null)
        {
            float sky = Sample(skyFlashes, now);
            if (skyHasExposure) skyInstance.SetFloat(ExposureId, skyBaseExposure * (1f + sky));
            if (skyHasTint) skyInstance.SetColor(TintId, Color.Lerp(skyBaseTint, skyFlashTint, Mathf.Clamp01(sky * 0.25f)));
        }
    }

    static float Sample(List<Flash> flashes, float now)
    {
        float v = 0f;
        for (int i = flashes.Count - 1; i >= 0; i--)
        {
            if (now > flashes[i].end) { flashes.RemoveAt(i); continue; }
            v = Mathf.Max(v, flashes[i].Evaluate(now));
        }
        return v;
    }

    void OnDestroy()
    {
        foreach (var go in glowObjects) if (go != null) Destroy(go);
        if (glowTextures != null) foreach (var tex in glowTextures) if (tex != null) Destroy(tex);

        if (skyInstance != null)
        {
            if (RenderSettings.skybox == skyInstance) RenderSettings.skybox = skyOriginal;
            Destroy(skyInstance);
        }
        if (instance == this) instance = null;
    }
}
