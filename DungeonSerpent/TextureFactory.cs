using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace DungeonSerpent;

/// <summary>
/// Creates all Texture2D assets at runtime from raw pixel data.
/// No external image files are required.
/// </summary>
public static class TextureFactory
{
    /// <summary>Solid 1×1 white pixel — tinted via SpriteBatch colour parameter.</summary>
    public static Texture2D CreatePixel(GraphicsDevice gd)
    {
        var t = new Texture2D(gd, 1, 1);
        t.SetData(new[] { Color.White });
        return t;
    }

    /// <summary>
    /// Round food pellet sprite (32×32) with a highlight dot — matches the
    /// JS prototype's glow-and-shine look as closely as possible in 2D.
    /// </summary>
    public static Texture2D CreateFoodSprite(GraphicsDevice gd, FoodDefinition def)
    {
        const int Size = 32;
        var data = new Color[Size * Size];
        float cx = Size / 2f;
        float cy = Size / 2f;
        float r  = def.Radius * (Size / (float)(C.CellSize));  // scale to sprite

        for (int py = 0; py < Size; py++)
        {
            for (int px = 0; px < Size; px++)
            {
                float dx   = px - cx;
                float dy   = py - cy;
                float dist = MathF.Sqrt(dx * dx + dy * dy);

                Color c;
                if (dist <= r)
                {
                    // Lerp from white at centre to main colour at edge → glossy look
                    float t = dist / r;
                    c = LerpColor(Color.White, def.MainColor, t * 0.85f);
                    c.A = 255;
                }
                else if (dist <= r * 2.4f)
                {
                    // Outer glow halo
                    float t = (dist - r) / (r * 1.4f);
                    t = MathF.Pow(1f - t, 2.5f);
                    c = def.GlowColor;
                    c.A = (byte)(def.GlowColor.A * t);
                }
                else
                {
                    c = Color.Transparent;
                }

                // Highlight dot (upper-left of sphere)
                float hdx  = px - (cx - r * 0.3f);
                float hdy  = py - (cy - r * 0.35f);
                float hdist = MathF.Sqrt(hdx * hdx + hdy * hdy);
                if (dist <= r && hdist <= r * 0.38f)
                {
                    float ht = 1f - hdist / (r * 0.38f);
                    c = LerpColor(c, Color.White, ht * 0.6f);
                    c.A = 255;
                }

                data[py * Size + px] = c;
            }
        }

        var tex = new Texture2D(gd, Size, Size);
        tex.SetData(data);
        return tex;
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private static Color LerpColor(Color a, Color b, float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        return new Color(
            (byte)(a.R + (b.R - a.R) * t),
            (byte)(a.G + (b.G - a.G) * t),
            (byte)(a.B + (b.B - a.B) * t),
            (byte)(a.A + (b.A - a.A) * t));
    }
}
