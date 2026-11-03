using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

// A ceiling light panel that hangs from its cables: it sways like a pendulum (wind + gusts, and Nudge() from
// other scripts), and every so often it shorts out, flickers and drops a shower of yellow sparks that
// bounce off the floor, each lit with its own little yellow light.
// Put it on the panel root; the lights and meshes under it swing together.
public class HangingLight : MonoBehaviour
{
    static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

    [Header("Swing")]
    [Tooltip("The cable mesh this hangs from. It swings along; its top is the pivot and sparks come from its bottom.")]
    public Renderer cable;
    [Tooltip("Only used without a cable: how far above the panel the pivot sits")]
    public float cableLength = 1f;
    [Range(0f, 15f)] public float maxAngle = 7f;
    [Tooltip("Higher = the sway dies out faster")]
    [Range(0.05f, 2f)] public float damping = 0.25f;
    [Tooltip("Constant draught (degrees/sec^2)")]
    public float windStrength = 8f;
    public float windSpeed = 0.35f;
    public Vector2 gustInterval = new Vector2(3f, 8f);
    [Tooltip("Degrees/sec of push from a gust")]
    public float gustStrength = 12f;

    [Header("Sparks")]
    public bool sparks = true;
    [Tooltip("Where on the cable the sparks start: 0 = where it meets the panel, 1 = the ceiling")]
    [Range(0f, 1f)] public float sparkHeightOnCable = 0.4f;
    [Tooltip("Adds a MeshCollider to the panel (if it has none) so sparks bounce off it on the way down")]
    public bool panelCollider = true;
    public Vector2 sparkInterval = new Vector2(4f, 11f);
    public Vector2Int sparkCount = new Vector2Int(5, 12);
    [ColorUsage(false, true)] public Color sparkColor = new Color(1f, 0.72f, 0.22f);
    public float sparkSize = 0.05f;
    public float sparkGravity = 1f;
    [Range(0f, 1f)] public float sparkBounce = 0.45f;
    public LayerMask sparkCollidesWith = ~0;
    [Tooltip("URP Particles/Unlit, additive")]
    public Material sparkMaterial;

    [Header("Spark light")]
    public float sparkLightIntensity = 4f;
    public float sparkLightRange = 3f;
    [Range(0f, 1f)] public float sparkLightRatio = 0.5f;
    [Tooltip("Brief yellow flash where the short happens")]
    public float flashIntensity = 6f;
    public float flashRange = 6f;

    Vector3 basePos, pivot, sparkOrigin;
    Quaternion baseRot;
    Vector3 cableBasePos;
    Quaternion cableBaseRot;
    Bounds baseBounds, cableBounds;
    Vector2 angle, velocity;   // x: tilt around world X, y: tilt around world Z (degrees)
    float freq2, seed, nextGust;

    ParticleSystem ps;
    GameObject sparkRoot;
    Light flash;
    float flashTime, flashDuration = 0.25f;

    Light[] panelLights;
    float[] lightBase;
    Renderer[] glow;
    Color[] glowBase;
    MaterialPropertyBlock block;
    float flickerEnd;
    bool wasFlickering;

    void Awake()
    {
        seed = Random.value * 100f;
        basePos = transform.position;
        baseRot = transform.rotation;

        bool has = false;
        foreach (var r in GetComponentsInChildren<Renderer>())
        {
            if (!has) { baseBounds = r.bounds; has = true; } else baseBounds.Encapsulate(r.bounds);
        }
        if (!has) baseBounds = new Bounds(basePos, Vector3.one);

        if (cable != null)
        {
            cableBounds = cable.bounds;
            cableBasePos = cable.transform.position;
            cableBaseRot = cable.transform.rotation;
            pivot = new Vector3(cableBounds.center.x, cableBounds.max.y, cableBounds.center.z);
            sparkOrigin = new Vector3(cableBounds.center.x,
                Mathf.Lerp(cableBounds.min.y, cableBounds.max.y, sparkHeightOnCable), cableBounds.center.z);
        }
        else
        {
            pivot = new Vector3(baseBounds.center.x, baseBounds.max.y + cableLength, baseBounds.center.z);
        }
        float length = Mathf.Max(0.3f, pivot.y - baseBounds.center.y);
        freq2 = Physics.gravity.magnitude / length;     // real pendulum: w^2 = g / L

        panelLights = GetComponentsInChildren<Light>();
        lightBase = new float[panelLights.Length];
        for (int i = 0; i < panelLights.Length; i++) lightBase[i] = panelLights[i].intensity;

        var glowList = new System.Collections.Generic.List<Renderer>();
        var glowColors = new System.Collections.Generic.List<Color>();
        foreach (var r in GetComponentsInChildren<Renderer>())
        {
            var m = r.sharedMaterial;
            if (m != null && m.HasProperty(EmissionId) && m.IsKeywordEnabled("_EMISSION") && m.GetColor(EmissionId).maxColorComponent > 0.5f)
            {
                glowList.Add(r);
                glowColors.Add(m.GetColor(EmissionId));
            }
        }
        glow = glowList.ToArray();
        glowBase = glowColors.ToArray();
        block = new MaterialPropertyBlock();

        if (panelCollider && GetComponent<Collider>() == null && GetComponent<MeshFilter>() != null)
            gameObject.AddComponent<MeshCollider>();

        nextGust = Time.time + Random.Range(gustInterval.x, gustInterval.y) * 0.5f;
        BuildSparks();
    }

    void OnDestroy()
    {
        if (sparkRoot != null) Destroy(sparkRoot);
    }

    void OnEnable()
    {
        if (sparkRoutine == null) sparkRoutine = StartCoroutine(SparkLoop());
    }

    void OnDisable()
    {
        if (sparkRoutine != null) { StopCoroutine(sparkRoutine); sparkRoutine = null; }
    }

    Coroutine sparkRoutine;

    // ------------------------------------------------------------------ swing
    // Push the panel. worldDirection is where it should swing towards (height is ignored).
    public void Nudge(Vector3 worldDirection, float degreesPerSecond)
    {
        worldDirection.y = 0f;
        if (worldDirection.sqrMagnitude < 1e-4f) return;
        worldDirection.Normalize();
        velocity.y += worldDirection.x * degreesPerSecond;   // tilt around Z moves it along +X
        velocity.x -= worldDirection.z * degreesPerSecond;   // tilt around X moves it along -Z
    }

    Vector3 Swing(Vector3 baseWorldPoint)
    {
        return pivot + SwingRotation() * (baseWorldPoint - pivot);
    }

    Quaternion SwingRotation()
    {
        return Quaternion.AngleAxis(angle.x, Vector3.right) * Quaternion.AngleAxis(angle.y, Vector3.forward);
    }

    void Update()
    {
        float dt = Mathf.Min(Time.deltaTime, 0.05f);
        float t = Time.time;

        Vector2 wind = new Vector2(
            Mathf.PerlinNoise(t * windSpeed, seed) - 0.5f,
            Mathf.PerlinNoise(seed + 7.3f, t * windSpeed) - 0.5f) * 2f * windStrength;

        if (t >= nextGust)
        {
            var dir = Random.insideUnitCircle.normalized;
            Nudge(new Vector3(dir.x, 0f, dir.y), gustStrength * Random.Range(0.5f, 1.2f));
            nextGust = t + Random.Range(gustInterval.x, gustInterval.y);
        }

        Vector2 accel = -freq2 * angle - damping * velocity + wind;
        velocity += accel * dt;
        angle += velocity * dt;
        if (angle.magnitude > maxAngle)
        {
            angle = Vector2.ClampMagnitude(angle, maxAngle);
            velocity *= 0.5f;
        }

        Quaternion dq = SwingRotation();
        transform.SetPositionAndRotation(pivot + dq * (basePos - pivot), dq * baseRot);
        if (cable != null)
            cable.transform.SetPositionAndRotation(pivot + dq * (cableBasePos - pivot), dq * cableBaseRot);

        UpdateFlicker(t);
        UpdateFlash(dt);
    }

    // ------------------------------------------------------------------ flicker + flash
    void UpdateFlicker(float t)
    {
        bool flickering = t < flickerEnd;
        if (!flickering && !wasFlickering) return;

        float k = flickering ? (Random.value < 0.5f ? Random.Range(0.05f, 0.25f) : 1f) : 1f;
        for (int i = 0; i < panelLights.Length; i++)
            if (panelLights[i] != null) panelLights[i].intensity = lightBase[i] * k;

        for (int i = 0; i < glow.Length; i++)
        {
            if (glow[i] == null) continue;
            glow[i].GetPropertyBlock(block);
            block.SetColor(EmissionId, glowBase[i] * k);
            glow[i].SetPropertyBlock(block);
        }
        wasFlickering = flickering;
    }

    void UpdateFlash(float dt)
    {
        if (flash == null) return;
        if (flashTime > 0f)
        {
            flashTime -= dt;
            float f = Mathf.Clamp01(flashTime / flashDuration);
            flash.intensity = flashIntensity * f * f;
            flash.enabled = flashTime > 0f;
        }
    }

    // ------------------------------------------------------------------ sparks
    void BuildSparks()
    {
        // lives at the scene root so the panel's scale never distorts the particles
        sparkRoot = new GameObject(name + " Sparks");
        ps = sparkRoot.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.loop = true;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Local;
        main.gravityModifier = sparkGravity;
        main.maxParticles = 160;
        main.startLifetime = 2f;
        main.startSpeed = 0f;

        var emission = ps.emission; emission.enabled = false;
        var shape = ps.shape; shape.enabled = false;

        var collision = ps.collision;
        collision.enabled = true;
        collision.type = ParticleSystemCollisionType.World;
        collision.mode = ParticleSystemCollisionMode.Collision3D;
        collision.bounce = sparkBounce;
        collision.dampen = 0.15f;
        collision.lifetimeLoss = 0.08f;
        collision.collidesWith = sparkCollidesWith;
        collision.quality = ParticleSystemCollisionQuality.High;
        collision.enableDynamicColliders = true;

        var colorOverLife = ps.colorOverLifetime;
        colorOverLife.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[] {
                new GradientColorKey(new Color(1f, 0.95f, 0.65f), 0f),
                new GradientColorKey(sparkColor, 0.35f),
                new GradientColorKey(new Color(0.9f, 0.25f, 0.05f), 1f) },
            new[] {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(1f, 0.6f),
                new GradientAlphaKey(0f, 1f) });
        colorOverLife.color = grad;

        var sizeOverLife = ps.sizeOverLifetime;
        sizeOverLife.enabled = true;
        sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 1f), new Keyframe(1f, 0.25f)));

        var rend = sparkRoot.GetComponent<ParticleSystemRenderer>();
        rend.renderMode = ParticleSystemRenderMode.Stretch;
        rend.velocityScale = 0.04f;
        rend.lengthScale = 1.5f;
        rend.cameraVelocityScale = 0f;
        rend.shadowCastingMode = ShadowCastingMode.Off;
        rend.receiveShadows = false;
        if (sparkMaterial != null) rend.sharedMaterial = sparkMaterial;

        // every spark carries a little yellow light, so the floor lights up where it bounces
        var templateGo = new GameObject("Spark Light Template");
        templateGo.transform.SetParent(sparkRoot.transform, false);
        var template = templateGo.AddComponent<Light>();
        template.type = LightType.Point;
        template.color = sparkColor;
        template.intensity = 1f;
        template.range = 1f;
        template.shadows = LightShadows.None;
        templateGo.SetActive(false);

        var lights = ps.lights;
        lights.enabled = true;
        lights.light = template;
        lights.ratio = sparkLightRatio;
        lights.useRandomDistribution = true;
        lights.useParticleColor = true;
        lights.sizeAffectsRange = false;
        lights.alphaAffectsIntensity = true;
        lights.range = sparkLightRange;
        lights.intensity = sparkLightIntensity;
        lights.maxLights = 6;

        // flash where the short happens
        var flashGo = new GameObject("Spark Flash");
        flashGo.transform.SetParent(sparkRoot.transform, false);
        flash = flashGo.AddComponent<Light>();
        flash.type = LightType.Point;
        flash.color = sparkColor;
        flash.range = flashRange;
        flash.intensity = 0f;
        flash.shadows = LightShadows.None;
        flash.enabled = false;

        ps.Play();
    }

    IEnumerator SparkLoop()
    {
        yield return new WaitForSeconds(Random.Range(sparkInterval.x, sparkInterval.y) * 0.5f);
        while (true)
        {
            if (sparks && ps != null) yield return Burst();
            yield return new WaitForSeconds(Random.Range(sparkInterval.x, sparkInterval.y));
        }
    }

    IEnumerator Burst()
    {
        // where the cable joins the panel, or a spot along the underside if there's no cable (rest position)
        Vector3 origin;
        if (cable != null)
        {
            Vector3 ce = cableBounds.extents;
            origin = sparkOrigin + new Vector3(Random.Range(-ce.x, ce.x), 0f, Random.Range(-ce.z, ce.z));
        }
        else
        {
            Vector3 e = baseBounds.extents;
            origin = new Vector3(
                baseBounds.center.x + Random.Range(-e.x, e.x) * 0.8f,
                baseBounds.min.y,
                baseBounds.center.z + Random.Range(-e.z, e.z) * 0.8f);
        }

        flickerEnd = Time.time + Random.Range(0.3f, 0.6f);
        Nudge(Random.onUnitSphere, 1.5f);

        int count = Random.Range(sparkCount.x, sparkCount.y + 1);
        flash.transform.position = Swing(origin);
        flashDuration = 0.25f;
        flashTime = flashDuration;

        for (int i = 0; i < count; i++)
        {
            var ep = new ParticleSystem.EmitParams
            {
                position = Swing(origin) + Random.insideUnitSphere * 0.05f,
                velocity = new Vector3(Random.Range(-1.4f, 1.4f), Random.Range(-0.3f, 1.2f), Random.Range(-1.4f, 1.4f)),
                startLifetime = Random.Range(1.5f, 2.6f),
                startSize = sparkSize * Random.Range(0.6f, 1.2f),
                startColor = Color.Lerp(sparkColor, Color.white, Random.value * 0.5f)
            };
            ps.Emit(ep, 1);
            if (i % 2 == 1) yield return null;
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        if (cable != null)
        {
            var cb = cable.bounds;
            Gizmos.DrawWireSphere(new Vector3(cb.center.x, cb.max.y, cb.center.z), 0.15f);   // pivot
            Gizmos.DrawWireSphere(new Vector3(cb.center.x, Mathf.Lerp(cb.min.y, cb.max.y, sparkHeightOnCable), cb.center.z), 0.08f);   // sparks
            return;
        }

        bool has = false;
        Bounds b = default;
        foreach (var r in GetComponentsInChildren<Renderer>())
        {
            if (!has) { b = r.bounds; has = true; } else b.Encapsulate(r.bounds);
        }
        if (!has) return;
        var top = new Vector3(b.center.x, b.max.y, b.center.z);
        var p = top + Vector3.up * cableLength;
        Gizmos.DrawLine(top, p);
        Gizmos.DrawWireSphere(p, 0.15f);
    }
}
