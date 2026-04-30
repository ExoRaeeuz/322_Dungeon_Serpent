using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace DungeonSerpent;

/// <summary>
/// Minimal bitmap font renderer.  Each character is 8×8 pixels, packed into
/// a 128×8 texture (16 chars per row, ASCII 32–127).  Scale up via the
/// SpriteBatch destination rectangle.
/// </summary>
public class BitmapFont
{
    // Compact 8×8 font — only the characters we actually use are defined.
    // A "1" bit = opaque, "0" = transparent.  8 bytes per character (rows).
    // Characters stored starting at ASCII 32 (space).
    private static readonly byte[][] Glyphs = BuildGlyphs();

    private readonly Texture2D _tex;
    private readonly int       _charW = 8;
    private readonly int       _charH = 8;

    public BitmapFont(GraphicsDevice gd)
    {
        // Build a 128 × 8 texture: columns = chars (0–95), rows = glyph rows
        int chars = Glyphs.Length;
        var data  = new Color[chars * _charW * _charH];
        for (int ci = 0; ci < chars; ci++)
        {
            byte[] g = Glyphs[ci];
            for (int row = 0; row < _charH; row++)
            {
                byte rowBits = row < g.Length ? g[row] : (byte)0;
                for (int col = 0; col < _charW; col++)
                {
                    bool on = (rowBits & (1 << (7 - col))) != 0;
                    data[row * chars * _charW + ci * _charW + col] =
                        on ? Color.White : Color.Transparent;
                }
            }
        }
        _tex = new Texture2D(gd, chars * _charW, _charH);
        _tex.SetData(data);
    }

    /// <summary>Draws text at pixel position, scaled by <paramref name="scale"/>.</summary>
    public void Draw(SpriteBatch sb, string text, float x, float y,
                     Color color, float scale = 1f)
    {
        int glyphCount = Glyphs.Length;
        float cx = x;
        float advance = _charW * scale;
        foreach (char ch in text)
        {
            int idx = ch - 32;
            if (idx < 0 || idx >= glyphCount) { cx += advance; continue; }
            var src  = new Rectangle(idx * _charW, 0, _charW, _charH);
            var dest = new Rectangle((int)cx, (int)y,
                                     (int)(_charW * scale), (int)(_charH * scale));
            sb.Draw(_tex, dest, src, color);
            cx += advance;
        }
    }

    /// <summary>Width in pixels of <paramref name="text"/> at given <paramref name="scale"/>.</summary>
    public float MeasureWidth(string text, float scale = 1f) =>
        text.Length * _charW * scale;

    // ── Glyph table ───────────────────────────────────────────────────────
    // ASCII 32 (space) through 90 (Z) — lowercase mapped to upper inside Draw
    // Each byte is one row of 8 pixels, MSB = left pixel.

    private static byte[][] BuildGlyphs()
    {
        // 96 entries covering ASCII 32–127
        var g = new byte[96][];
        for (int i = 0; i < 96; i++) g[i] = new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 };

        // Space (0)
        g[0] = new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
        // ! (1)
        g[1] = new byte[] { 0x18, 0x18, 0x18, 0x18, 0x00, 0x00, 0x18, 0x00 };
        // + (11)
        g[11] = new byte[] { 0x00, 0x18, 0x18, 0x7E, 0x18, 0x18, 0x00, 0x00 };
        // - (13)
        g[13] = new byte[] { 0x00, 0x00, 0x00, 0x7E, 0x00, 0x00, 0x00, 0x00 };
        // . (14)
        g[14] = new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x18, 0x18, 0x00 };
        // : (26)
        g[26] = new byte[] { 0x00, 0x18, 0x18, 0x00, 0x18, 0x18, 0x00, 0x00 };

        // Digits  0–9  (ASCII 48 = index 16)
        g[16] = new byte[] { 0x3C, 0x66, 0x6E, 0x76, 0x66, 0x66, 0x3C, 0x00 }; // 0
        g[17] = new byte[] { 0x18, 0x38, 0x18, 0x18, 0x18, 0x18, 0x7E, 0x00 }; // 1
        g[18] = new byte[] { 0x3C, 0x66, 0x06, 0x1C, 0x30, 0x66, 0x7E, 0x00 }; // 2
        g[19] = new byte[] { 0x3C, 0x66, 0x06, 0x1C, 0x06, 0x66, 0x3C, 0x00 }; // 3
        g[20] = new byte[] { 0x0C, 0x1C, 0x3C, 0x6C, 0x7E, 0x0C, 0x0C, 0x00 }; // 4
        g[21] = new byte[] { 0x7E, 0x60, 0x7C, 0x06, 0x06, 0x66, 0x3C, 0x00 }; // 5
        g[22] = new byte[] { 0x1C, 0x30, 0x60, 0x7C, 0x66, 0x66, 0x3C, 0x00 }; // 6
        g[23] = new byte[] { 0x7E, 0x66, 0x0C, 0x18, 0x18, 0x18, 0x18, 0x00 }; // 7
        g[24] = new byte[] { 0x3C, 0x66, 0x66, 0x3C, 0x66, 0x66, 0x3C, 0x00 }; // 8
        g[25] = new byte[] { 0x3C, 0x66, 0x66, 0x3E, 0x06, 0x0C, 0x38, 0x00 }; // 9

        // Uppercase A–Z  (ASCII 65 = index 33)
        g[33] = new byte[] { 0x18, 0x3C, 0x66, 0x7E, 0x66, 0x66, 0x66, 0x00 }; // A
        g[34] = new byte[] { 0x7C, 0x66, 0x66, 0x7C, 0x66, 0x66, 0x7C, 0x00 }; // B
        g[35] = new byte[] { 0x3C, 0x66, 0x60, 0x60, 0x60, 0x66, 0x3C, 0x00 }; // C
        g[36] = new byte[] { 0x78, 0x6C, 0x66, 0x66, 0x66, 0x6C, 0x78, 0x00 }; // D
        g[37] = new byte[] { 0x7E, 0x60, 0x60, 0x78, 0x60, 0x60, 0x7E, 0x00 }; // E
        g[38] = new byte[] { 0x7E, 0x60, 0x60, 0x78, 0x60, 0x60, 0x60, 0x00 }; // F
        g[39] = new byte[] { 0x3C, 0x66, 0x60, 0x6E, 0x66, 0x66, 0x3C, 0x00 }; // G
        g[40] = new byte[] { 0x66, 0x66, 0x66, 0x7E, 0x66, 0x66, 0x66, 0x00 }; // H
        g[41] = new byte[] { 0x3C, 0x18, 0x18, 0x18, 0x18, 0x18, 0x3C, 0x00 }; // I
        g[42] = new byte[] { 0x1E, 0x0C, 0x0C, 0x0C, 0x0C, 0x6C, 0x38, 0x00 }; // J
        g[43] = new byte[] { 0x66, 0x6C, 0x78, 0x70, 0x78, 0x6C, 0x66, 0x00 }; // K
        g[44] = new byte[] { 0x60, 0x60, 0x60, 0x60, 0x60, 0x60, 0x7E, 0x00 }; // L
        g[45] = new byte[] { 0x63, 0x77, 0x7F, 0x6B, 0x63, 0x63, 0x63, 0x00 }; // M
        g[46] = new byte[] { 0x66, 0x76, 0x7E, 0x7E, 0x6E, 0x66, 0x66, 0x00 }; // N
        g[47] = new byte[] { 0x3C, 0x66, 0x66, 0x66, 0x66, 0x66, 0x3C, 0x00 }; // O
        g[48] = new byte[] { 0x7C, 0x66, 0x66, 0x7C, 0x60, 0x60, 0x60, 0x00 }; // P
        g[49] = new byte[] { 0x3C, 0x66, 0x66, 0x66, 0x66, 0x3C, 0x0E, 0x00 }; // Q
        g[50] = new byte[] { 0x7C, 0x66, 0x66, 0x7C, 0x78, 0x6C, 0x66, 0x00 }; // R
        g[51] = new byte[] { 0x3C, 0x66, 0x60, 0x3C, 0x06, 0x66, 0x3C, 0x00 }; // S
        g[52] = new byte[] { 0x7E, 0x18, 0x18, 0x18, 0x18, 0x18, 0x18, 0x00 }; // T
        g[53] = new byte[] { 0x66, 0x66, 0x66, 0x66, 0x66, 0x66, 0x3C, 0x00 }; // U
        g[54] = new byte[] { 0x66, 0x66, 0x66, 0x66, 0x66, 0x3C, 0x18, 0x00 }; // V
        g[55] = new byte[] { 0x63, 0x63, 0x63, 0x6B, 0x7F, 0x77, 0x63, 0x00 }; // W
        g[56] = new byte[] { 0x66, 0x66, 0x3C, 0x18, 0x3C, 0x66, 0x66, 0x00 }; // X
        g[57] = new byte[] { 0x66, 0x66, 0x66, 0x3C, 0x18, 0x18, 0x18, 0x00 }; // Y
        g[58] = new byte[] { 0x7E, 0x06, 0x0C, 0x18, 0x30, 0x60, 0x7E, 0x00 }; // Z

        // Map lowercase to uppercase in Draw by converting char
        // a=97 → 65 → index 33, etc.  Handled in the Draw method.

        return g;
    }

    /// <summary>
    /// Draw, auto-converting lowercase to uppercase (the font has no lowercase).
    /// </summary>
    public void DrawAuto(SpriteBatch sb, string text, float x, float y,
                         Color color, float scale = 1f)
        => Draw(sb, text.ToUpperInvariant(), x, y, color, scale);

    /// <summary>Center-X helper.</summary>
    public void DrawCentered(SpriteBatch sb, string text, float centerX, float y,
                             Color color, float scale = 1f)
    {
        float w = MeasureWidth(text.ToUpperInvariant(), scale);
        DrawAuto(sb, text, centerX - w / 2f, y, color, scale);
    }
}
