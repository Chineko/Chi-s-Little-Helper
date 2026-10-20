using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Animations;
using VRC.SDK3.Avatars.Components;

public partial class ChiLittleHelperWindow
{
    static Color Hex(string hex)
    {
        ColorUtility.TryParseHtmlString(hex, out var c);
        return c;
    }

    static Texture2D Make9SliceRoundedRect(int size, int radius, Color fill, Color border, float borderWidth = 1.2f)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave,
            filterMode = FilterMode.Bilinear
        };
        float half = size * 0.5f;
        float r = radius;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float qx = Mathf.Abs(x + 0.5f - half) - (half - r);
                float qy = Mathf.Abs(y + 0.5f - half) - (half - r);
                float dist = Mathf.Max(qx, 0) * Mathf.Max(qx, 0) + Mathf.Max(qy, 0) * Mathf.Max(qy, 0);
                dist = Mathf.Sqrt(dist) + Mathf.Min(Mathf.Max(qx, qy), 0) - r;

                float alpha = Mathf.Clamp01(0.5f - dist);
                float borderT = Mathf.Clamp01(dist + borderWidth);
                Color col = Color.Lerp(fill, border, borderT);
                col.a *= alpha;
                tex.SetPixel(x, y, col);
            }
        }
        tex.Apply();
        return tex;
    }

    static Texture2D Make9SliceGradientRect(int size, int radius, Color leftCol, Color rightCol, Color border, float borderWidth = 1.0f)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave,
            filterMode = FilterMode.Bilinear
        };
        float half = size * 0.5f;
        float r = radius;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float t = x / (float)(size - 1);
                Color fill = Color.Lerp(leftCol, rightCol, t);

                float qx = Mathf.Abs(x + 0.5f - half) - (half - r);
                float qy = Mathf.Abs(y + 0.5f - half) - (half - r);
                float dist = Mathf.Max(qx, 0) * Mathf.Max(qx, 0) + Mathf.Max(qy, 0) * Mathf.Max(qy, 0);
                dist = Mathf.Sqrt(dist) + Mathf.Min(Mathf.Max(qx, qy), 0) - r;

                float alpha = Mathf.Clamp01(0.5f - dist);
                float borderT = Mathf.Clamp01(dist + borderWidth);
                Color col = Color.Lerp(fill, border, borderT);
                col.a *= alpha;
                tex.SetPixel(x, y, col);
            }
        }
        tex.Apply();
        return tex;
    }

    static Texture2D MakeBannerBackground(int w = 512, int h = 120)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave,
            filterMode = FilterMode.Bilinear
        };

        Color cLeft   = Hex("#6658F4");
        Color cMidL   = Hex("#8B62F5");
        Color cMidR   = Hex("#D875E4");
        Color cRight  = Hex("#F49DDE");

        Color waveGlow = Hex("#FBE0FF");

        var glints = new[]
        {
            new Vector3(0.10f, 0.34f, 0.55f),
            new Vector3(0.22f, 0.66f, 0.40f),
            new Vector3(0.31f, 0.42f, 0.65f),
            new Vector3(0.69f, 0.70f, 0.45f),
            new Vector3(0.76f, 0.44f, 0.60f),
            new Vector3(0.89f, 0.60f, 0.40f),
        };

        for (int y = 0; y < h; y++)
        {
            float ty = y / (float)(h - 1);
            for (int x = 0; x < w; x++)
            {
                float tx = x / (float)(w - 1);

                Color baseCol;
                if (tx < 0.33f)
                    baseCol = Color.Lerp(cLeft,  cMidL, tx / 0.33f);
                else if (tx < 0.66f)
                    baseCol = Color.Lerp(cMidL,  cMidR, (tx - 0.33f) / 0.33f);
                else
                    baseCol = Color.Lerp(cMidR,  cRight, (tx - 0.66f) / 0.34f);

                float vShade = Mathf.Lerp(0.82f, 1.0f, ty);
                baseCol *= vShade;

                float waveY = 0.55f + 0.10f * Mathf.Sin((tx - 0.5f) * 2.2f * Mathf.PI);
                float dWave = Mathf.Abs(ty - waveY);

                float glow = Mathf.Exp(-dWave * dWave / 0.010f) * 0.30f;
                float core = Mathf.Exp(-dWave * dWave / 0.0009f) * 0.55f;

                baseCol = Color.Lerp(baseCol, waveGlow, Mathf.Clamp01(glow + core));

                foreach (var g in glints)
                {
                    float dx = (tx - g.x) * w;
                    float dy = (ty - g.y) * h;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    if (d < 7f)
                    {
                        float ax = Mathf.Pow(Mathf.Abs(dx) / (g.z * 8f), 0.5f);
                        float ay = Mathf.Pow(Mathf.Abs(dy) / (g.z * 8f), 0.5f);
                        float a = ax + ay;
                        if (a < 1f)
                            baseCol = Color.Lerp(baseCol, Color.white, (1f - a) * 0.55f);
                    }
                }

                float r = 12f;
                float qx = Mathf.Abs(x + 0.5f - w * 0.5f) - (w * 0.5f - r);
                float qy = Mathf.Abs(y + 0.5f - h * 0.5f) - (h * 0.5f - r);
                float dist = Mathf.Max(qx, 0) * Mathf.Max(qx, 0) + Mathf.Max(qy, 0) * Mathf.Max(qy, 0);
                dist = Mathf.Sqrt(dist) + Mathf.Min(Mathf.Max(qx, qy), 0) - r;
                float alpha = Mathf.Clamp01(0.5f - dist);

                baseCol.a = alpha;
                tex.SetPixel(x, y, baseCol);
            }
        }
        tex.Apply();
        return tex;
    }

    static Texture2D MakeBannerIcon(int size = 88)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave,
            filterMode = FilterMode.Bilinear
        };

        Color topFill    = Hex("#B05CE8");
        Color btmFill    = Hex("#9A44E0");
        Color glowRing   = Hex("#E5A8F5");
        float half = size * 0.5f;
        float r = 20f;

        Vector2 mainC = new Vector2(half, half + 1.0f);
        float mainArmX = size * 0.30f;
        float mainArmY = size * 0.32f;

        Vector2 subC = new Vector2(half + size * 0.17f, half - size * 0.16f);
        float subArmX = size * 0.10f;
        float subArmY = size * 0.11f;

        for (int y = 0; y < size; y++)
        {
            float ty = y / (float)(size - 1);
            Color tileFill = Color.Lerp(btmFill, topFill, ty);

            for (int x = 0; x < size; x++)
            {
                float qx = Mathf.Abs(x + 0.5f - half) - (half - r);
                float qy = Mathf.Abs(y + 0.5f - half) - (half - r);
                float dist = Mathf.Max(qx, 0) * Mathf.Max(qx, 0) + Mathf.Max(qy, 0) * Mathf.Max(qy, 0);
                dist = Mathf.Sqrt(dist) + Mathf.Min(Mathf.Max(qx, qy), 0) - r;

                float inside = Mathf.Clamp01(0.5f - dist);
                float glow = Mathf.Clamp01(1.0f - Mathf.Abs(dist) / 2.2f) * 0.45f;

                Color pixel = tileFill;
                pixel.a = inside;

                Vector2 pm = new Vector2(x, y) - mainC;
                float am = Mathf.Pow(Mathf.Abs(pm.x) / mainArmX, 0.5f) +
                           Mathf.Pow(Mathf.Abs(pm.y) / mainArmY, 0.5f);
                if (am <= 1.0f)
                {
                    float edge = Mathf.Clamp01((1.0f - am) * 6f);
                    pixel = Color.Lerp(pixel, Color.white, edge);
                }

                Vector2 ps = new Vector2(x, y) - subC;
                float asub = Mathf.Pow(Mathf.Abs(ps.x) / subArmX, 0.5f) +
                             Mathf.Pow(Mathf.Abs(ps.y) / subArmY, 0.5f);
                if (asub <= 1.0f)
                {
                    float edge = Mathf.Clamp01((1.0f - asub) * 5f);
                    pixel = Color.Lerp(pixel, Color.white, edge);
                }

                if (inside < 0.99f)
                {
                    pixel = Color.Lerp(pixel, glowRing, glow);
                    pixel.a = Mathf.Max(inside, glow);
                }

                tex.SetPixel(x, y, pixel);
            }
        }
        tex.Apply();
        return tex;
    }

    static Texture2D MakeBoxIllustration(int w = 150, int h = 95)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave,
            filterMode = FilterMode.Bilinear
        };

        Color clear = new Color(0, 0, 0, 0);
        Color purple = Hex("#A879FF");
        Color pink = Hex("#E879E8");
        Color soft = Hex("#C596FF");

        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                tex.SetPixel(x, y, clear);

        void BlendPixel(int x, int y, Color c, float a)
        {
            if (x < 0 || y < 0 || x >= w || y >= h) return;
            Color old = tex.GetPixel(x, y);
            float na = Mathf.Clamp01(a);
            Color outc = Color.Lerp(old, c, na);
            outc.a = Mathf.Max(old.a, na);
            tex.SetPixel(x, y, outc);
        }

        void Dot(float cx, float cy, float radius, Color c)
        {
            int minX = Mathf.Max(0, Mathf.FloorToInt(cx - radius - 1));
            int maxX = Mathf.Min(w - 1, Mathf.CeilToInt(cx + radius + 1));
            int minY = Mathf.Max(0, Mathf.FloorToInt(cy - radius - 1));
            int maxY = Mathf.Min(h - 1, Mathf.CeilToInt(cy + radius + 1));
            for (int y = minY; y <= maxY; y++)
                for (int x = minX; x <= maxX; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy));
                    if (d <= radius + 1f)
                        BlendPixel(x, y, c, Mathf.Clamp01(radius + 1f - d));
                }
        }

        float SegDist(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float l2 = Vector2.Dot(ab, ab);
            if (l2 < 0.0001f) return (p - a).magnitude;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / l2);
            return (p - (a + ab * t)).magnitude;
        }

        void Line(Vector2 a, Vector2 b, float width, Color c)
        {
            int minX = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.x, b.x) - width - 2));
            int maxX = Mathf.Min(w - 1, Mathf.CeilToInt(Mathf.Max(a.x, b.x) + width + 2));
            int minY = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.y, b.y) - width - 2));
            int maxY = Mathf.Min(h - 1, Mathf.CeilToInt(Mathf.Max(a.y, b.y) + width + 2));
            for (int y = minY; y <= maxY; y++)
                for (int x = minX; x <= maxX; x++)
                {
                    float d = SegDist(new Vector2(x, y), a, b);
                    if (d <= width + 1f)
                        BlendPixel(x, y, c, Mathf.Clamp01(width + 1f - d));
                }
        }

        // Cat head outline, matching the friendly neon mockup.
        Vector2 center = new Vector2(w * .50f, h * .47f);
        float rx = 31f, ry = 23f;
        Vector2 prev = Vector2.zero;
        bool havePrev = false;
        for (int i = 0; i <= 64; i++)
        {
            float t = Mathf.PI * 2f * i / 64f;
            Vector2 p = center + new Vector2(Mathf.Cos(t) * rx, Mathf.Sin(t) * ry);
            // Leave the upper arc open where the ears sit.
            if (p.y < center.y + 14f || Mathf.Abs(p.x - center.x) > 18f)
            {
                if (havePrev && Vector2.Distance(prev, p) < 8f) Line(prev, p, 1.35f, purple);
                prev = p; havePrev = true;
            }
            else havePrev = false;
        }

        // Ears.
        Line(center + new Vector2(-24, 13), center + new Vector2(-13, 28), 1.35f, purple);
        Line(center + new Vector2(-13, 28), center + new Vector2(-5, 17), 1.35f, purple);
        Line(center + new Vector2(  8, 18), center + new Vector2( 18, 31), 1.35f, purple);
        Line(center + new Vector2( 18, 31), center + new Vector2( 26, 12), 1.35f, purple);

        // Eyes.
        Dot(center.x - 13, center.y + 2, 2.1f, soft);
        Dot(center.x + 13, center.y + 2, 2.1f, soft);

        // Tiny smiling mouth.
        Line(center + new Vector2(-5, -5), center + new Vector2(0, -8), 1.1f, pink);
        Line(center + new Vector2(0, -8), center + new Vector2(5, -5), 1.1f, pink);
        Dot(center.x, center.y - 4.5f, 1.1f, soft);

        // Sparkles around the cat.
        void Sparkle(float x, float y, float r)
        {
            Line(new Vector2(x - r, y), new Vector2(x + r, y), .9f, purple);
            Line(new Vector2(x, y - r), new Vector2(x, y + r), .9f, purple);
            Line(new Vector2(x - r*.55f, y - r*.55f), new Vector2(x + r*.55f, y + r*.55f), .7f, pink);
            Line(new Vector2(x - r*.55f, y + r*.55f), new Vector2(x + r*.55f, y - r*.55f), .7f, pink);
        }
        Sparkle(center.x - 43, center.y + 26, 5);
        Sparkle(center.x - 52, center.y + 4, 4);
        Sparkle(center.x + 43, center.y + 10, 5);
        Sparkle(center.x + 35, center.y - 16, 3.5f);

        tex.Apply();
        return tex;
    }

    static float SdfSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 pa = p - a, ba = b - a;
        float l2 = Vector2.Dot(ba, ba);
        if (l2 < 0.0001f) return pa.magnitude;
        float t = Mathf.Clamp01(Vector2.Dot(pa, ba) / l2);
        return (pa - ba * t).magnitude;
    }

    static float SdfCircle(Vector2 p, Vector2 c, float r)
        => (p - c).magnitude - r;

    static float SdfBox(Vector2 p, Vector2 c, Vector2 halfSize)
    {
        Vector2 d = new Vector2(Mathf.Abs(p.x - c.x) - halfSize.x,
                                Mathf.Abs(p.y - c.y) - halfSize.y);
        Vector2 dPos = new Vector2(Mathf.Max(d.x, 0), Mathf.Max(d.y, 0));
        return dPos.magnitude + Mathf.Min(Mathf.Max(d.x, d.y), 0);
    }

    static Texture2D MakeIconFromSdf(int size, Func<Vector2, float> sdf, Color fill, Color outline)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave,
            filterMode = FilterMode.Bilinear
        };

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                float d = sdf(p);

                float fillA   = Mathf.Clamp01(0.5f - d);
                float borderA = Mathf.Clamp01(1.0f - Mathf.Abs(d + 1.0f));

                Color col = Color.Lerp(fill, outline, borderA);
                col.a = fillA;
                tex.SetPixel(x, y, col);
            }
        }
        tex.Apply();
        return tex;
    }

    static Texture2D MakeIconBone(int size = 32)
    {
        Color fill    = Hex("#DDBBFF");
        Color outline = Hex("#B79CE8");

        // Bone: a thin shaft with two knob-clusters at each end
        Vector2 a = new Vector2(size * 0.28f, size * 0.30f);
        Vector2 b = new Vector2(size * 0.72f, size * 0.70f);
        float shaftR = size * 0.055f;
        float knobR  = size * 0.105f;

        Vector2 dir  = (b - a).normalized;
        Vector2 perp = new Vector2(-dir.y, dir.x) * (knobR * 0.55f);

        Vector2[] knobs =
        {
            a + perp, a - perp,
            b + perp, b - perp
        };

        return MakeIconFromSdf(size, p =>
        {
            float d = SdfSegment(p, a, b) - shaftR;
            for (int i = 0; i < knobs.Length; i++)
                d = Mathf.Min(d, SdfCircle(p, knobs[i], knobR));
            return d;
        }, fill, outline);
    }

    static Texture2D MakeIconBubble(int size = 32)
    {
        Color fill    = Hex("#DDBBFF");
        Color outline = Hex("#B79CE8");

        // A rounded speech bubble with a small tail
        Vector2 bodyC = new Vector2(size * 0.5f, size * 0.55f);
        Vector2 bodyH = new Vector2(size * 0.30f, size * 0.22f);
        float radius  = size * 0.20f;

        Vector2 tailA = new Vector2(size * 0.30f, size * 0.30f);
        Vector2 tailB = new Vector2(size * 0.20f, size * 0.18f);
        Vector2 tailC = new Vector2(size * 0.38f, size * 0.28f);

        return MakeIconFromSdf(size, p =>
        {
            // Rounded rectangle body via box + circle subtraction
            float d = SdfBox(p, bodyC, bodyH) - radius * 0.35f;

            // Triangular tail (three edge distances -> max gives the shape)
            float e0 = SdfSegment(p, tailA, tailB);
            float e1 = SdfSegment(p, tailB, tailC);
            float e2 = SdfSegment(p, tailC, tailA);
            float dTail = Mathf.Max(e0, e1) - size * 0.02f;
            // Only keep the tail if it's on the correct side (below body)
            if (p.y > tailA.y) dTail = 1e5f;

            return Mathf.Min(d, dTail);
        }, fill, outline);
    }

    static Texture2D MakeIconCube(int size = 32)
    {
        Color fill    = Hex("#DDBBFF");
        Color outline = Hex("#B79CE8");

        // Simple isometric cube: union of three rhombi approximated via SDF
        Vector2 c = new Vector2(size * 0.5f, size * 0.5f);
        float s = size * 0.26f;

        // Top rhombus corners
        Vector2 top    = new Vector2(c.x, c.y + s);
        Vector2 right  = new Vector2(c.x + s, c.y + s * 0.5f);
        Vector2 bottom = new Vector2(c.x, c.y);
        Vector2 left   = new Vector2(c.x - s, c.y + s * 0.5f);

        // Bottom rhombus corners (offset down)
        Vector2 bRight  = new Vector2(c.x + s, c.y - s * 0.5f);
        Vector2 bBottom = new Vector2(c.x, c.y - s);
        Vector2 bLeft   = new Vector2(c.x - s, c.y - s * 0.5f);

        return MakeIconFromSdf(size, p =>
        {
            // Top face (diamond)
            float dTop = Mathf.Max(
                Mathf.Abs(p.y - (c.y + s * 0.5f)) / (s * 0.5f) +
                Mathf.Abs(p.x - c.x) / s - 1f, 0f) * (s * 0.5f);

            // Left face (parallelogram)
            float dLeft = Mathf.Max(
                Mathf.Abs(p.x - (c.x - s * 0.5f)) / (s * 0.5f) +
                Mathf.Abs(p.y - (c.y - s * 0.25f) - (p.x - (c.x - s * 0.5f)) * 0.5f) / (s * 0.5f) - 1f, 0f) * (s * 0.5f);

            // Right face
            float dRight = Mathf.Max(
                Mathf.Abs(p.x - (c.x + s * 0.5f)) / (s * 0.5f) +
                Mathf.Abs(p.y - (c.y - s * 0.25f) + (p.x - (c.x + s * 0.5f)) * 0.5f) / (s * 0.5f) - 1f, 0f) * (s * 0.5f);

            float d = Mathf.Min(dTop, Mathf.Min(dLeft, dRight));
            return d - 0.5f; // slight inset for a cleaner glyph
        }, fill, outline);
    }

    static Texture2D MakeIconSmile(int size = 32)
    {
        Color fill    = Hex("#DDBBFF");
        Color outline = Hex("#B79CE8");

        Vector2 c = new Vector2(size * 0.5f, size * 0.5f);
        float r = size * 0.32f;

        return MakeIconFromSdf(size, p =>
        {
            float d = SdfCircle(p, c, r);
            // Punch out eyes and mouth (negative areas)
            float eyeL = SdfCircle(p, new Vector2(c.x - r * 0.35f, c.y + r * 0.25f), size * 0.055f);
            float eyeR = SdfCircle(p, new Vector2(c.x + r * 0.35f, c.y + r * 0.25f), size * 0.055f);

            // Mouth arc: distance to a downward-pointing ring segment
            Vector2 mouthC = new Vector2(c.x, c.y + r * 0.10f);
            float mouthR = r * 0.55f;
            float mouthD = Mathf.Abs(SdfCircle(p, mouthC, mouthR)) - size * 0.04f;
            // Keep only the lower half of the ring
            if (p.y > c.y + r * 0.10f) mouthD = 1e5f;

            // Union of eyes and mouth as a "cut out" -> use max (i.e. we want to subtract)
            float cut = Mathf.Min(eyeL, eyeR);
            cut = Mathf.Min(cut, mouthD);

            // Shape = body minus cut
            return Mathf.Max(d, -cut);
        }, fill, outline);
    }

    static Texture2D MakeIconList(int size = 32)
    {
        Color fill    = Hex("#DDBBFF");
        Color outline = Hex("#B79CE8");

        float pad = size * 0.22f;
        float lineH = size * 0.05f;
        float gap = size * 0.12f;

        return MakeIconFromSdf(size, p =>
        {
            // Three horizontal bars
            float d0 = SdfBox(p, new Vector2(size * 0.5f, pad + gap * 2f), new Vector2(size * 0.28f, lineH));
            float d1 = SdfBox(p, new Vector2(size * 0.5f, pad + gap * 1f), new Vector2(size * 0.28f, lineH));
            float d2 = SdfBox(p, new Vector2(size * 0.5f, pad + gap * 0f), new Vector2(size * 0.28f, lineH));
            return Mathf.Min(d0, Mathf.Min(d1, d2));
        }, fill, outline);
    }

    static Texture2D MakeIconPerson(int size = 32)
    {
        Color fill    = Hex("#DDBBFF");
        Color outline = Hex("#B79CE8");

        Vector2 headC = new Vector2(size * 0.5f, size * 0.68f);
        float headR = size * 0.15f;
        float bodyR = size * 0.28f;
        Vector2 bodyC = new Vector2(size * 0.5f, size * 0.15f);

        return MakeIconFromSdf(size, p =>
        {
            float head = SdfCircle(p, headC, headR);
            // Shoulders: a big circle clipped to the bottom
            float body = SdfCircle(p, bodyC, bodyR);
            if (p.y > size * 0.42f) body = 1e5f;

            return Mathf.Min(head, body);
        }, fill, outline);
    }

    static void EnsureStyles()
    {
        if (texCardBg == null)          texCardBg         = Make9SliceRoundedRect(32, 10, ColCardBg, ColCardBorder, 1.2f);
        if (texBannerBg == null)        texBannerBg       = MakeBannerBackground(512, 120);
        if (texBannerIcon == null)      texBannerIcon     = MakeBannerIcon(88);
        if (texTabActive == null)       texTabActive      = Make9SliceGradientRect(32, 7, Hex("#8B5CFF"), Hex("#EF7FDB"), Hex("#FFB4F1"), 1.0f);
        if (texTabInactive == null)     texTabInactive    = Make9SliceRoundedRect(32, 7, Hex("#0E1C31"), Hex("#2A3D60"), 1.0f);
        if (texBtnGrad == null)         texBtnGrad        = Make9SliceGradientRect(32, 8, Hex("#8A5CFF"), Hex("#F07FDC"), Hex("#FFC0F2"), 1.0f);
        if (texBtnGradHover == null)    texBtnGradHover   = Make9SliceGradientRect(32, 8, Hex("#9E72FF"), Hex("#FF92E6"), Color.white, 1.2f);
        if (texBtnDark == null)         texBtnDark        = Make9SliceRoundedRect(32, 8, ColDarkBtnBg, ColDarkBtnBorder, 1.0f);
        if (texBtnDarkHover == null)    texBtnDarkHover   = Make9SliceRoundedRect(32, 8, ColDarkBtnHover, Lighten(ColDarkBtnBorder, 0.20f), 1.0f);
        if (texBtnRemove == null)       texBtnRemove      = Make9SliceRoundedRect(32, 6, ColDarkBtnBg, ColDarkBtnBorder, 1.0f);
        if (texBtnRemoveHover == null)  texBtnRemoveHover = Make9SliceRoundedRect(32, 6, DangerBgHover, DangerRed, 1.0f);
        if (texInfoBox == null)         texInfoBox        = Make9SliceRoundedRect(32, 8, Hex("#111F38"), Hex("#405A88"), 1.0f);
        if (texFieldBg == null)         texFieldBg        = Make9SliceRoundedRect(32, 6, ColCardInnerBg, ColFieldBorder, 1.0f);
        if (texRowBg == null)           texRowBg          = Make9SliceRoundedRect(32, 8, Hex("#0C1A2E"), Hex("#263E63"), 1.0f);
        if (texBoxIllustration == null) texBoxIllustration = MakeBoxIllustration(150, 95);

        // Card badge icons
        if (texIconBone == null)        texIconBone       = MakeIconBone(32);
        if (texIconBubble == null)      texIconBubble     = MakeIconBubble(32);
        if (texIconCube == null)        texIconCube       = MakeIconCube(32);
        if (texIconSmile == null)       texIconSmile      = MakeIconSmile(32);
        if (texIconList == null)        texIconList       = MakeIconList(32);
        if (texIconPerson == null)      texIconPerson     = MakeIconPerson(32);

        if (styleHeaderTitle == null)
            styleHeaderTitle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 20,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = ColTextPrimary }
            };

        if (styleHeaderSubtitle == null)
            styleHeaderSubtitle = new GUIStyle(EditorStyles.miniLabel)
            {
                fontSize = 11,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Hex("#F3B2EA") }
            };

        if (styleCardTitle == null)
            styleCardTitle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 13,
                margin = new RectOffset(0, 0, 0, 1),
                normal = { textColor = ColTextPrimary }
            };

        if (styleCardSubtitle == null)
            styleCardSubtitle = new GUIStyle(EditorStyles.miniLabel)
            {
                fontSize = 10,
                wordWrap = true,
                normal = { textColor = ColTextMuted }
            };

        if (styleInfoText == null)
            styleInfoText = new GUIStyle(EditorStyles.miniLabel)
            {
                fontSize = 11,
                wordWrap = true,
                normal = { textColor = Hex("#94A1B8") }
            };

        if (styleFieldLabel == null)
            styleFieldLabel = new GUIStyle(EditorStyles.label)
            {
                fontSize = 11,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = ColTextMuted }
            };

        if (styleInputBox == null)
            styleInputBox = new GUIStyle(EditorStyles.textField)
            {
                fontSize = 12,
                fixedHeight = 24,
                border = new RectOffset(6, 6, 6, 6),
                padding = new RectOffset(8, 8, 3, 3),
                alignment = TextAnchor.MiddleLeft,
                normal = { background = texFieldBg, textColor = ColTextPrimary },
                focused = { background = texFieldBg, textColor = ColTextPrimary }
            };

        if (stylePopup == null)
            stylePopup = new GUIStyle(EditorStyles.popup)
            {
                fontStyle = FontStyle.Bold,
                fontSize = 12,
                fixedHeight = 24,
                border = new RectOffset(6, 6, 6, 6),
                padding = new RectOffset(8, 24, 3, 3),
                alignment = TextAnchor.MiddleLeft,
                normal = { background = texFieldBg, textColor = ColLavenderText },
                hover = { background = texBtnDarkHover, textColor = Color.white },
                focused = { background = texFieldBg, textColor = Color.white },
                active = { background = texBtnDarkHover, textColor = Color.white }
            };

        if (styleNumberField == null)
            styleNumberField = new GUIStyle(EditorStyles.numberField)
            {
                fontSize = 12,
                fixedHeight = 24,
                border = new RectOffset(6, 6, 6, 6),
                padding = new RectOffset(8, 8, 3, 3),
                alignment = TextAnchor.MiddleLeft,
                normal = { background = texFieldBg, textColor = ColTextPrimary },
                focused = { background = texFieldBg, textColor = Color.white },
                hover = { background = texBtnDarkHover, textColor = Color.white }
            };

        if (styleObjectField == null)
            styleObjectField = new GUIStyle(EditorStyles.objectField)
            {
                fontSize = 12,
                fixedHeight = 24,
                border = new RectOffset(6, 6, 6, 6),
                padding = new RectOffset(8, 22, 3, 3),
                alignment = TextAnchor.MiddleLeft,
                normal = { background = texFieldBg, textColor = ColTextPrimary },
                hover = { background = texBtnDarkHover, textColor = Color.white },
                focused = { background = texFieldBg, textColor = Color.white },
                active = { background = texBtnDarkHover, textColor = Color.white }
            };

        if (styleToolbarSearch == null)
            styleToolbarSearch = new GUIStyle(EditorStyles.toolbarSearchField)
            {
                fontSize = 12,
                fixedHeight = 24,
                border = new RectOffset(6, 6, 6, 6),
                padding = new RectOffset(8, 8, 3, 3),
                normal = { background = texFieldBg, textColor = ColTextPrimary },
                focused = { background = texFieldBg, textColor = Color.white }
            };

        if (styleRowBg == null)
            styleRowBg = new GUIStyle
            {
                normal = { background = texRowBg },
                border = new RectOffset(8, 8, 8, 8),
                padding = new RectOffset(10, 10, 8, 8),
                margin = new RectOffset(0, 0, 0, 0)
            };

        if (styleBtnGrad == null)
            styleBtnGrad = new GUIStyle(GUI.skin.button)
            {
                fontStyle = FontStyle.Bold,
                fontSize = 12,
                border = new RectOffset(8, 8, 8, 8),
                padding = new RectOffset(10, 10, 4, 4),
                clipping = TextClipping.Clip,
                alignment = TextAnchor.MiddleCenter,
                normal = { background = texBtnGrad, textColor = Color.white },
                hover  = { background = texBtnGradHover, textColor = Color.white },
                active = { background = texBtnGradHover, textColor = Color.white }
            };

        if (styleBtnDark == null)
            styleBtnDark = new GUIStyle(GUI.skin.button)
            {
                fontStyle = FontStyle.Bold,
                fontSize = 12,
                border = new RectOffset(8, 8, 8, 8),
                padding = new RectOffset(10, 10, 4, 4),
                clipping = TextClipping.Clip,
                alignment = TextAnchor.MiddleCenter,
                normal = { background = texBtnDark, textColor = Hex("#D2DBE8") },
                hover  = { background = texBtnDarkHover, textColor = Color.white },
                active = { background = texBtnDarkHover, textColor = Color.white }
            };

        if (styleBtnRemove == null)
            styleBtnRemove = new GUIStyle(GUI.skin.button)
            {
                fontStyle = FontStyle.Bold,
                fontSize = 12,
                border = new RectOffset(6, 6, 6, 6),
                padding = new RectOffset(8, 8, 4, 4),
                clipping = TextClipping.Clip,
                alignment = TextAnchor.MiddleCenter,
                normal = { background = texBtnRemove, textColor = Hex("#CCD5E4") },
                hover  = { background = texBtnRemoveHover, textColor = DangerRed },
                active = { background = texBtnRemoveHover, textColor = DangerRed }
            };

        if (styleTabActive == null)
            styleTabActive = new GUIStyle(GUI.skin.button)
            {
                fontStyle = FontStyle.Bold,
                fontSize = 12,
                padding = new RectOffset(10, 10, 5, 5),
                border = new RectOffset(7, 7, 7, 7),
                alignment = TextAnchor.MiddleCenter,
                normal = { background = texTabActive, textColor = Color.white },
                hover  = { background = texTabActive, textColor = Color.white },
                active = { background = texTabActive, textColor = Color.white }
            };

        if (styleTabInactive == null)
            styleTabInactive = new GUIStyle(GUI.skin.button)
            {
                fontStyle = FontStyle.Bold,
                fontSize = 12,
                padding = new RectOffset(10, 10, 5, 5),
                border = new RectOffset(7, 7, 7, 7),
                alignment = TextAnchor.MiddleCenter,
                normal = { background = texTabInactive, textColor = ColTextMuted },
                hover  = { background = texBtnDarkHover, textColor = Color.white },
                active = { background = texBtnDarkHover, textColor = Color.white }
            };
    }

    UnityEngine.Object DrawThemedObjectField(
        UnityEngine.Object value,
        Type objectType,
        bool allowSceneObjects,
        params GUILayoutOption[] options)
    {
        Rect rect = GUILayoutUtility.GetRect(
            GUIContent.none,
            styleObjectField,
            options);

        // Unity's EditorGUI.ObjectField does not expose a GUIStyle overload
        // consistently across supported editor versions. Draw our themed field
        // background first, then let Unity draw the object picker/content using
        // the stable five-argument overload.
        if (Event.current.type == EventType.Repaint)
        {
            styleObjectField.Draw(
                rect,
                GUIContent.none,
                false,
                false,
                false,
                false);
        }

        return EditorGUI.ObjectField(
            rect,
            GUIContent.none,
            value,
            objectType,
            allowSceneObjects);
    }

    void DrawBanner()
    {
        EnsureStyles();
        const float BannerHeight = 132f;
        var r = GUILayoutUtility.GetRect(0f, BannerHeight, GUILayout.ExpandWidth(true));
        if (Event.current.type != EventType.Repaint) return;

        GUI.DrawTexture(r, texBannerBg, ScaleMode.StretchToFill);

        // Soft inner highlight to match the polished mockup.
        EditorGUI.DrawRect(new Rect(r.x + 10f, r.y + 1f, Mathf.Max(0f, r.width - 20f), 1f), new Color(1f, 1f, 1f, 0.18f));

        const float IconSize = 50f;
        float iconX = r.x + (r.width - IconSize) * 0.5f;
        float iconY = r.y + 15f;
        var iconRect = new Rect(iconX, iconY, IconSize, IconSize);

        GUI.DrawTexture(iconRect, texBannerIcon, ScaleMode.StretchToFill);

        var titleRect = new Rect(r.x, iconY + IconSize + 7f, r.width, 25f);
        GUI.Label(titleRect, "Chi's Little Helper", styleHeaderTitle);

        var subtitleRect = new Rect(r.x, titleRect.yMax + 1f, r.width, 18f);
        GUI.Label(subtitleRect, "Your friendly avatar toolkit", styleHeaderSubtitle);
    }

    static Texture2D GetIconTexture(CardIcon icon)
    {
        switch (icon)
        {
            case CardIcon.Avatar:       return texIconPerson;
            case CardIcon.Armature:     return texIconBone;
            case CardIcon.Dissolve:     return texIconBubble;
            case CardIcon.MultiToggles: return texIconCube;
            case CardIcon.Blendshapes:  return texIconSmile;
            case CardIcon.Overview:     return texIconList;
            case CardIcon.Warning:
            case CardIcon.Sparkle:
            default:                    return null; // fallback: symbol glyph
        }
    }

    static string GetIconFallbackGlyph(CardIcon icon)
    {
        switch (icon)
        {
            case CardIcon.Warning: return "!";
            case CardIcon.Sparkle: return "*";
            default:               return null;
        }
    }

    static void BeginCard(CardIcon icon, string title, string subtitle)
    {
        EnsureStyles();
        var style = new GUIStyle
        {
            normal = { background = texCardBg },
            border = new RectOffset(10, 10, 10, 10),
            padding = new RectOffset(16, 16, 15, 15),
            margin = new RectOffset(0, 0, 5, 12)
        };
        EditorGUILayout.BeginVertical(style);

        if (!string.IsNullOrEmpty(title))
        {
            EditorGUILayout.BeginHorizontal();

            // Badge: draw a procedural icon texture (reliable across platforms),
            // or fall back to an ASCII glyph for special cards.
            var badgeRect = GUILayoutUtility.GetRect(32, 32, GUILayout.Width(32), GUILayout.Height(32));
            if (Event.current.type == EventType.Repaint)
            {
                GUI.DrawTexture(badgeRect, texTabInactive, ScaleMode.StretchToFill);

                Texture2D iconTex = GetIconTexture(icon);
                if (iconTex != null)
                {
                    var drawRect = new Rect(
                        badgeRect.x + 6, badgeRect.y + 6,
                        badgeRect.width - 12, badgeRect.height - 12);
                    GUI.DrawTexture(drawRect, iconTex, ScaleMode.ScaleToFit);
                }
                else
                {
                    string glyph = GetIconFallbackGlyph(icon);
                    if (!string.IsNullOrEmpty(glyph))
                    {
                        var glyphStyle = new GUIStyle(EditorStyles.boldLabel)
                        {
                            alignment = TextAnchor.MiddleCenter,
                            fontSize = 16,
                            normal = { textColor = ColLavenderText }
                        };
                        GUI.Label(badgeRect, glyph, glyphStyle);
                    }
                }
            }

            GUILayout.Space(10);
            EditorGUILayout.BeginVertical();
            EditorGUILayout.LabelField(title, styleCardTitle);
            if (!string.IsNullOrEmpty(subtitle))
                EditorGUILayout.LabelField(subtitle, styleCardSubtitle);
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(12);
        }
    }

    static GUIContent HelpContent(string text, string tooltip)
    {
        return new GUIContent(text, tooltip ?? "");
    }

    static bool HelpButton(string text, string tooltip, GUIStyle style, params GUILayoutOption[] options)
    {
        return GUILayout.Button(new GUIContent(text, tooltip ?? ""), style, options);
    }

    void BeginResponsiveButtonRow()
    {
        if (IsCompactLayout)
            EditorGUILayout.BeginVertical();
        else
            EditorGUILayout.BeginHorizontal();
    }

    void EndResponsiveButtonRow()
    {
        if (IsCompactLayout)
            EditorGUILayout.EndVertical();
        else
            EditorGUILayout.EndHorizontal();
    }

    void ResponsiveButtonGap(float wideGap = 4f)
    {
        GUILayout.Space(IsCompactLayout ? 4f : wideGap);
    }

    GUILayoutOption ResponsiveButtonWidth(float wideWidth)
    {
        return IsCompactLayout
            ? GUILayout.ExpandWidth(true)
            : GUILayout.Width(wideWidth);
    }

    static void DrawHelpHint(string text)
    {
        EnsureStyles();

        var helpStyle = new GUIStyle(EditorStyles.miniLabel)
        {
            wordWrap = true,
            fontSize = 10,
            normal = { textColor = ColTextMuted }
        };

        EditorGUILayout.LabelField(new GUIContent("ⓘ " + text, text), helpStyle);
    }

    static void DrawInfoBox(string message)
    {
        EnsureStyles();
        var boxStyle = new GUIStyle
        {
            normal = { background = texInfoBox },
            border = new RectOffset(8, 8, 8, 8),
            padding = new RectOffset(12, 12, 10, 10),
            margin = new RectOffset(0, 0, 2, 12)
        };

        EditorGUILayout.BeginHorizontal(boxStyle);
        var iconRect = GUILayoutUtility.GetRect(24, 24, GUILayout.Width(24), GUILayout.Height(24));
        if (Event.current.type == EventType.Repaint)
        {
            GUI.DrawTexture(iconRect, texTabInactive, ScaleMode.StretchToFill);
            var symStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 13,
                normal = { textColor = ColLavenderText }
            };
            GUI.Label(iconRect, "!", symStyle);
        }
        GUILayout.Space(10);
        EditorGUILayout.LabelField(message, styleInfoText);
        EditorGUILayout.EndHorizontal();
    }

    void DrawFooter()
    {
        EnsureStyles();
        var footerRect = GUILayoutUtility.GetRect(0f, 26f, GUILayout.ExpandWidth(true));
        if (Event.current.type != EventType.Repaint) return;

        var pillRect = new Rect(footerRect.x + 8f, footerRect.y + 11f, 74f, 3f);
        GUI.DrawTexture(pillRect, texTabActive, ScaleMode.StretchToFill);

        var versionStyle = new GUIStyle(EditorStyles.miniLabel)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 11,
            normal = { textColor = Hex("#637188") }
        };
        GUI.Label(footerRect, "v " + TOOL_VERSION, versionStyle);

        var heartStyle = new GUIStyle(EditorStyles.label)
        {
            alignment = TextAnchor.MiddleRight,
            fontSize = 12,
            normal = { textColor = Hex("#E875B0") }
        };
        GUI.Label(new Rect(footerRect.xMax - 30f, footerRect.y, 20f, footerRect.height), "♥", heartStyle);
    }
}
