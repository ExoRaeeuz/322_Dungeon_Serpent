using Microsoft.Xna.Framework;

namespace DungeonSerpent;

// ── Game state machine ─────────────────────────────────────────────────────
public enum GameState
{
    Start,
    Playing,
    Paused,
    Settings,
    Dead
}

// ── Input scheme ───────────────────────────────────────────────────────────
public enum InputScheme
{
    Both,
    WASDOnly,
    ArrowOnly
}

// ── Persistent user settings ───────────────────────────────────────────────
public class GameSettings
{
    public bool        SoundEnabled { get; set; } = true;
    public InputScheme InputScheme  { get; set; } = InputScheme.Both;
}

// ── Direction ──────────────────────────────────────────────────────────────
public struct Direction
{
    public int Dx;
    public int Dy;

    public static readonly Direction Right = new() { Dx =  1, Dy =  0 };
    public static readonly Direction Left  = new() { Dx = -1, Dy =  0 };
    public static readonly Direction Up    = new() { Dx =  0, Dy = -1 };
    public static readonly Direction Down  = new() { Dx =  0, Dy =  1 };

    public bool IsOpposite(Direction other) =>
        Dx == -other.Dx && Dy == -other.Dy;
}

// ── Food types ─────────────────────────────────────────────────────────────
public enum FoodType { Coin, Ruby, Diamond, Shrinker, Slow }

public record FoodDefinition(
    FoodType Type, int Points,
    Color MainColor, Color GlowColor,
    float Radius, float Weight);

public class FoodItem
{
    public Point          Cell;
    public FoodDefinition Def   = null!;
    public float          Pulse;
}

// ── Knight (mini-boss) ────────────────────────────────────────────────────
/// <summary>
/// A knight that appears every 5 levels on a wall cell and fires one arrow
/// toward the snake head.  Despawns after firing or when the level changes.
/// </summary>
public class Knight
{
    public Point Cell;          // wall cell the knight stands on
    public int   WallSide;      // 0=top 1=bottom 2=left 3=right  (for drawing)
    public bool  HasFired;
    public float FireTimer;     // counts down before the first shot
}

/// <summary>Single arrow projectile moving one cell per snake step.</summary>
public class Arrow
{
    public float X, Y;          // pixel position (centre of cell)
    public int   Dx, Dy;        // unit direction
    public bool  Active = true;
}

// ── Score pop-up ───────────────────────────────────────────────────────────
public class Popup
{
    public float  X, Y;
    public string Text  = "";
    public float  Alpha;
    public float  DY;
}

// ── Level-up flash ─────────────────────────────────────────────────────────
public class LevelFlash
{
    public string Text  = "";
    public float  Alpha;
}
