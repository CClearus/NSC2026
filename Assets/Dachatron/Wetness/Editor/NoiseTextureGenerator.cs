using System.IO;
using UnityEditor;
using UnityEngine;

// Writes a few tileable noise textures (R channel) to Assets/Dachatron/Wetness/Noise.
public static class NoiseTextureGenerator
{
    const string Folder = "Assets/Dachatron/Wetness/Noise";
    const int Size = 512;

    static float Hash(int x, int y, int seed)
    {
        unchecked
        {
            uint h = (uint)(x * 374761393 + y * 668265263 + seed * 2147483647);
            h = (h ^ (h >> 13)) * 1274126177u;
            return ((h ^ (h >> 16)) & 0xFFFFFF) / (float)0xFFFFFF;
        }
    }

    static float Fade(float t) => t * t * t * (t * (t * 6f - 15f) + 10f);

    // Value noise on a lattice that wraps every `period` cells, so the texture tiles seamlessly.
    static float Tile(float x, float y, int period, int seed)
    {
        int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y);
        float fx = Fade(x - xi), fy = Fade(y - yi);
        int x0 = ((xi % period) + period) % period, x1 = (x0 + 1) % period;
        int y0 = ((yi % period) + period) % period, y1 = (y0 + 1) % period;
        float a = Hash(x0, y0, seed), b = Hash(x1, y0, seed), c = Hash(x0, y1, seed), d = Hash(x1, y1, seed);
        return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
    }

    static float Fbm(float u, float v, int basePeriod, int octaves, float gain, int seed)
    {
        float sum = 0f, amp = 1f, total = 0f;
        int period = basePeriod;
        for (int o = 0; o < octaves; o++)
        {
            sum += amp * Tile(u * period, v * period, period, seed + o * 31);
            total += amp;
            amp *= gain;
            period *= 2;
        }
        return sum / total;
    }

    public static void Generate()
    {
        if (!AssetDatabase.IsValidFolder(Folder))
            AssetDatabase.CreateFolder("Assets/Dachatron/Wetness", "Noise");

        Write("WetNoise_Soft", (u, v) => Fbm(u, v, 3, 5, 0.5f, 1));                              // big soft patches
        Write("WetNoise_Puddles", (u, v) => Mathf.SmoothStep(0f, 1f, Fbm(u, v, 4, 4, 0.55f, 7)   // harder, pooled shapes
                                                           * 1.4f - 0.2f));
        Write("WetNoise_Streaks", (u, v) => Fbm(u * 1f, v, 2, 4, 0.5f, 13) * 0.6f                 // stretched, rain-run look
                                          + Tile(u * 24f, v * 3f, 24, 99) * 0.4f);

        AssetDatabase.Refresh();
        Debug.Log("[Wet World] Noise textures written to " + Folder);
    }

    static void Write(string name, System.Func<float, float, float> f)
    {
        var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false, true);
        var px = new Color32[Size * Size];
        for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                float n = Mathf.Clamp01(f(x / (float)Size, y / (float)Size));
                byte b = (byte)Mathf.RoundToInt(n * 255f);
                px[y * Size + x] = new Color32(b, b, b, 255);
            }
        tex.SetPixels32(px);

        string path = $"{Folder}/{name}.png";
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path);

        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        imp.sRGBTexture = false;
        imp.wrapMode = TextureWrapMode.Repeat;
        imp.mipmapEnabled = true;
        imp.filterMode = FilterMode.Bilinear;
        imp.anisoLevel = 8;
        imp.SaveAndReimport();
    }
}
