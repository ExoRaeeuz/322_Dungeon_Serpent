using Microsoft.Xna.Framework;

namespace DungeonSerpent;

public static class C
{
    // ── Grid ──────────────────────────────────────────────────────────────
    public const int Cols     = 20;
    public const int Rows     = 20;
    public const int CellSize = 30;
    public const int WindowW  = Cols * CellSize;        // 600
    public const int WindowH  = Rows * CellSize + HudHeight + FooterHeight;

    public const int HudHeight    = 48;
    public const int FooterHeight = 24;

    public const int MinCol = 1;
    public const int MaxCol = Cols - 2;   // 18
    public const int MinRow = 1;
    public const int MaxRow = Rows - 2;   // 18

    public const int GridOffsetY = HudHeight;

    // ── Speeds (ms / step), index = level-1 ──────────────────────────────
    public static readonly int[] Speeds =
        { 160, 140, 120, 105, 90, 76, 64, 55, 47, 40 };

    // ── Scoring ───────────────────────────────────────────────────────────
    public const int PtsPerLevel = 100;

    // ── Special pellets ───────────────────────────────────────────────────
    public const float SlowDuration   = 5f;
    public const int   ShrinkSegments = 3;
    public const float SlowMultiplier = 2.2f;

    // ── Knight / arrow ────────────────────────────────────────────────────
    /// <summary>Knight spawns whenever level is a multiple of this.</summary>
    public const int KnightEveryNLevels = 5;
    /// <summary>Seconds after knight appears before it fires.</summary>
    public const float KnightFireDelay  = 2.0f;
    /// <summary>Arrow travels one cell per this many ms.</summary>
    public const float ArrowStepMs      = 120f;

    // ── Colours ───────────────────────────────────────────────────────────
    public static readonly Color BgDark      = new( 6,  6, 16);
    public static readonly Color BgSurface   = new(13, 13, 30);
    public static readonly Color BorderColor = new(37, 53, 88);
    public static readonly Color TextColor   = new(200,200,232);
    public static readonly Color DimColor    = new( 88, 88,160);
    public static readonly Color GoldColor   = new(255,215,  0);
    public static readonly Color RedColor    = new(238, 51, 85);
    public static readonly Color AccentColor = new( 85, 68,238);
    public static readonly Color WallDark    = new( 22, 30, 56);
    public static readonly Color WallMid     = new( 30, 42, 74);

    // Dragon colours
    public static readonly Color DragonScale1 = new(180,  30,  20);  // deep red
    public static readonly Color DragonScale2 = new(220,  80,  10);  // orange
    public static readonly Color DragonBelly  = new(200, 160,  60);  // gold belly
    public static readonly Color DragonHorn   = new(220, 200, 100);  // horn/claw

    // Knight colours
    public static readonly Color KnightArmor  = new(140,150,170);
    public static readonly Color KnightVisor  = new( 60, 70, 90);
    public static readonly Color ArrowColor   = new(200,160, 80);
    public static readonly Color ArrowTip     = new(180,180,200);

    // ── Food definitions ──────────────────────────────────────────────────
    public static readonly FoodDefinition[] Foods = new[]
    {
        new FoodDefinition(FoodType.Coin,    10,
            new Color((byte)255,(byte)215,(byte)  0),
            new Color((byte)255,(byte)180,(byte)  0,(byte)120), 8f, 60f),
        new FoodDefinition(FoodType.Ruby,    25,
            new Color((byte)255,(byte) 68,(byte)102),
            new Color((byte)255,(byte) 60,(byte)100,(byte)120), 7f, 90f),
        new FoodDefinition(FoodType.Diamond, 50,
            new Color((byte) 68,(byte)221,(byte)255),
            new Color((byte) 50,(byte)220,(byte)255,(byte)128), 7f,100f),
        new FoodDefinition(FoodType.Shrinker, 0,
            new Color((byte)160,(byte) 60,(byte)220),
            new Color((byte)140,(byte) 40,(byte)200,(byte)120), 7f,  0f),
        new FoodDefinition(FoodType.Slow,     0,
            new Color((byte) 60,(byte)200,(byte)140),
            new Color((byte) 40,(byte)180,(byte)120,(byte)120), 7f,  0f),
    };

    public const string HiScoreFile = "dungeon_serpent_hi.txt";
}
