using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace DungeonSerpent;

public class DungeonSerpentGame : Game
{
    // ── Infrastructure ─────────────────────────────────────────────────────
    private readonly GraphicsDeviceManager _graphics;
    private SpriteBatch _sb    = null!;
    private BitmapFont  _font  = null!;
    private Texture2D   _pixel = null!;
    private Texture2D[] _foodSprites = null!;

    // ── Settings ───────────────────────────────────────────────────────────
    private readonly GameSettings _settings = new();

    // ── Game state ─────────────────────────────────────────────────────────
    private GameState _state = GameState.Start;

    private readonly List<Point> _snake = new();
    private Direction  _dir;
    private Direction  _nextDir;
    private FoodItem   _food = new();

    private int  _score;
    private int  _level;
    private int  _highScore;
    private bool _newRecord;

    // Timing
    private double _stepAccum;
    private double _totalMs;

    // Special pellet
    private bool  _slowActive;
    private float _slowRemaining;

    // Visuals
    private readonly List<Popup> _popups      = new();
    private readonly List<Color> _popupColors = new();
    private LevelFlash?          _levelFlash;
    private float                _foodPulse;

    // ── Knight / arrow ─────────────────────────────────────────────────────
    private Knight?            _knight;
    private readonly List<Arrow> _arrows    = new();
    private double             _arrowAccum;    // ms since last arrow step
    private int                _lastKnightLv;  // level at which knight last spawned

    // ── Pause / settings menu ──────────────────────────────────────────────
    private int _pauseSelected    = 0;
    private int _settingsSelected = 0;
    private const int PauseItemCount    = 3;
    private const int SettingsItemCount = 3;

    // Input
    private KeyboardState _prevKeys;
    private readonly Random _rng = new();

    // ── Ctor ───────────────────────────────────────────────────────────────
    public DungeonSerpentGame()
    {
        _graphics = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth  = C.WindowW,
            PreferredBackBufferHeight = C.WindowH,
            SynchronizeWithVerticalRetrace = true
        };
        Content.RootDirectory    = "Content";
        IsMouseVisible           = true;
        Window.Title             = "Dungeon Serpent";
        Window.AllowUserResizing = false;
    }

    // ── Init ───────────────────────────────────────────────────────────────
    protected override void Initialize()
    {
        _highScore = HighScoreManager.Load();
        base.Initialize();
    }

    protected override void LoadContent()
    {
        _sb    = new SpriteBatch(GraphicsDevice);
        _font  = new BitmapFont(GraphicsDevice);
        _pixel = TextureFactory.CreatePixel(GraphicsDevice);

        _foodSprites = new Texture2D[C.Foods.Length];
        for (int i = 0; i < C.Foods.Length; i++)
            _foodSprites[i] = TextureFactory.CreateFoodSprite(GraphicsDevice, C.Foods[i]);

        SoundManager.Initialize();
        InitState();
    }

    // ── Game logic init ────────────────────────────────────────────────────
    private void InitState()
    {
        _snake.Clear();
        _popups.Clear();
        _popupColors.Clear();
        _arrows.Clear();
        _knight        = null;
        _lastKnightLv  = 0;
        _levelFlash    = null;
        _stepAccum     = 0;
        _arrowAccum    = 0;
        _totalMs       = 0;
        _slowActive    = false;
        _slowRemaining = 0;

        int hx = C.MinCol + (C.MaxCol - C.MinCol) / 2 + 1;
        int hy = C.MinRow + (C.MaxRow - C.MinRow) / 2;
        _snake.Add(new Point(hx,     hy));
        _snake.Add(new Point(hx - 1, hy));
        _snake.Add(new Point(hx - 2, hy));

        _dir       = Direction.Right;
        _nextDir   = Direction.Right;
        _score     = 0;
        _level     = 1;
        _newRecord = false;

        SpawnFood();
    }

    private void SetState(GameState s)
    {
        _state = s;
        if (s == GameState.Paused)   _pauseSelected    = 0;
        if (s == GameState.Settings) _settingsSelected = 0;
        if (s == GameState.Dead)
        {
            if (_settings.SoundEnabled) SoundManager.PlayDie();
            if (_score > _highScore)
            {
                _highScore = _score;
                _newRecord = true;
                HighScoreManager.Save(_highScore);
            }
        }
    }

    // ── Food spawning ──────────────────────────────────────────────────────
    private void SpawnFood()
    {
        var occ = new HashSet<string>(_snake.Select(p => $"{p.X},{p.Y}"));
        int x, y, att = 0;
        do
        {
            x = C.MinCol + _rng.Next(C.MaxCol - C.MinCol + 1);
            y = C.MinRow + _rng.Next(C.MaxRow - C.MinRow + 1);
        } while (occ.Contains($"{x},{y}") && ++att < 500);

        FoodDefinition def;
        int sc = _rng.Next(100);
        if      (sc < 8  && _level >= 3) def = C.Foods[(int)FoodType.Shrinker];
        else if (sc < 13 && _level >= 2) def = C.Foods[(int)FoodType.Slow];
        else
        {
            float r = _rng.NextSingle() * 100f;
            int fi = 0;
            for (int i = 0; i < C.Foods.Length; i++)
                if (C.Foods[i].Weight > 0f && r < C.Foods[i].Weight) { fi = i; break; }
            def = C.Foods[fi];
        }
        _food = new FoodItem { Cell = new Point(x, y), Def = def };
    }

    // ── Knight spawning ────────────────────────────────────────────────────
    private void TrySpawnKnight()
    {
        // Spawn once per qualifying level milestone (multiples of KnightEveryNLevels)
        if (_level % C.KnightEveryNLevels != 0) return;
        if (_level == _lastKnightLv) return;
        _lastKnightLv = _level;
        _knight       = null;   // clear old one
        _arrows.Clear();

        // Pick a random wall side then a random cell along that wall
        int side = _rng.Next(4);
        Point cell;
        int wallSide = side;
        switch (side)
        {
            case 0: // top wall
                cell = new Point(C.MinCol + _rng.Next(C.MaxCol - C.MinCol + 1), 0);
                break;
            case 1: // bottom wall
                cell = new Point(C.MinCol + _rng.Next(C.MaxCol - C.MinCol + 1), C.Rows - 1);
                break;
            case 2: // left wall
                cell = new Point(0, C.MinRow + _rng.Next(C.MaxRow - C.MinRow + 1));
                break;
            default: // right wall
                cell = new Point(C.Cols - 1, C.MinRow + _rng.Next(C.MaxRow - C.MinRow + 1));
                break;
        }

        _knight = new Knight
        {
            Cell      = cell,
            WallSide  = wallSide,
            HasFired  = false,
            FireTimer = C.KnightFireDelay
        };
    }

    // ── Update ─────────────────────────────────────────────────────────────
    protected override void Update(GameTime gt)
    {
        var keys  = Keyboard.GetState();
        double elMs = gt.ElapsedGameTime.TotalMilliseconds;
        _totalMs   += elMs;

        switch (_state)
        {
            case GameState.Start:    UpdateStart(keys);              break;
            case GameState.Playing:  UpdatePlaying(keys, gt, elMs);  break;
            case GameState.Paused:   UpdatePauseMenu(keys);          break;
            case GameState.Settings: UpdateSettings(keys);           break;
            case GameState.Dead:     UpdateDead(keys);               break;
        }

        // Animate popups regardless of state
        for (int i = _popups.Count - 1; i >= 0; i--)
        {
            var p = _popups[i];
            p.DY    -= 0.6f  * (float)(elMs / 16.67);
            p.Alpha -= 0.022f * (float)(elMs / 16.67);
            if (p.Alpha <= 0) { _popups.RemoveAt(i); _popupColors.RemoveAt(i); }
        }

        if (_levelFlash != null)
        {
            _levelFlash.Alpha -= 0.012f * (float)(elMs / 16.67);
            if (_levelFlash.Alpha <= 0) _levelFlash = null;
        }

        _prevKeys = keys;
        base.Update(gt);
    }

    private void UpdateStart(KeyboardState keys)
    {
        if (WasPressed(keys, _prevKeys, Keys.Enter) ||
            WasPressed(keys, _prevKeys, Keys.Space))
        { StartGame(); return; }
        if (TryGetDirection(keys, out _)) StartGame();
    }

    private void UpdatePlaying(KeyboardState keys, GameTime gt, double elMs)
    {
        // Pause
        if (WasPressed(keys, _prevKeys, Keys.P)      ||
            WasPressed(keys, _prevKeys, Keys.Space)  ||
            WasPressed(keys, _prevKeys, Keys.Escape))
        { SetState(GameState.Paused); return; }

        // Direction
        if (TryGetDirection(keys, out Direction nd) && !nd.IsOpposite(_dir))
            _nextDir = nd;

        // Slow timer
        if (_slowActive)
        {
            _slowRemaining -= (float)gt.ElapsedGameTime.TotalSeconds;
            if (_slowRemaining <= 0) _slowActive = false;
        }

        // Snake step
        int    base_ = C.Speeds[Math.Min(_level - 1, C.Speeds.Length - 1)];
        double eff   = _slowActive ? base_ * C.SlowMultiplier : base_;
        _stepAccum  += elMs;
        if (_stepAccum >= eff) { _stepAccum -= eff; StepSnake(); }

        // Knight fire timer
        if (_knight != null && !_knight.HasFired)
        {
            _knight.FireTimer -= (float)gt.ElapsedGameTime.TotalSeconds;
            if (_knight.FireTimer <= 0) FireKnightArrow();
        }

        // Move arrows
        if (_arrows.Count > 0)
        {
            _arrowAccum += elMs;
            while (_arrowAccum >= C.ArrowStepMs)
            {
                _arrowAccum -= C.ArrowStepMs;
                StepArrows();
            }
        }
    }

    private void UpdatePauseMenu(KeyboardState keys)
    {
        if (WasPressed(keys, _prevKeys, Keys.Up)   || WasPressed(keys, _prevKeys, Keys.W))
            _pauseSelected = (_pauseSelected - 1 + PauseItemCount) % PauseItemCount;
        if (WasPressed(keys, _prevKeys, Keys.Down) || WasPressed(keys, _prevKeys, Keys.S))
            _pauseSelected = (_pauseSelected + 1) % PauseItemCount;

        if (WasPressed(keys, _prevKeys, Keys.Enter) || WasPressed(keys, _prevKeys, Keys.Space))
        {
            switch (_pauseSelected)
            {
                case 0: ResumeGame();                    break;
                case 1: SetState(GameState.Settings);   break;
                case 2: SetState(GameState.Start);      break;
            }
        }
        if (WasPressed(keys, _prevKeys, Keys.P) || WasPressed(keys, _prevKeys, Keys.Escape))
            ResumeGame();
    }

    private void UpdateSettings(KeyboardState keys)
    {
        if (WasPressed(keys, _prevKeys, Keys.Up)   || WasPressed(keys, _prevKeys, Keys.W))
            _settingsSelected = (_settingsSelected - 1 + SettingsItemCount) % SettingsItemCount;
        if (WasPressed(keys, _prevKeys, Keys.Down) || WasPressed(keys, _prevKeys, Keys.S))
            _settingsSelected = (_settingsSelected + 1) % SettingsItemCount;

        if (WasPressed(keys, _prevKeys, Keys.Enter) || WasPressed(keys, _prevKeys, Keys.Space))
        {
            switch (_settingsSelected)
            {
                case 0: _settings.SoundEnabled = !_settings.SoundEnabled; break;
                case 1: CycleInputScheme(1);  break;
                case 2: SetState(GameState.Paused); break;
            }
        }

        if (_settingsSelected == 1)
        {
            if (WasPressed(keys, _prevKeys, Keys.Left))  CycleInputScheme(-1);
            if (WasPressed(keys, _prevKeys, Keys.Right)) CycleInputScheme(1);
        }

        if (WasPressed(keys, _prevKeys, Keys.Escape))
            SetState(GameState.Paused);
    }

    private void CycleInputScheme(int dir)
    {
        int v = ((int)_settings.InputScheme + dir + 3) % 3;
        _settings.InputScheme = (InputScheme)v;
    }

    private void UpdateDead(KeyboardState keys)
    {
        if (WasPressed(keys, _prevKeys, Keys.Enter) || WasPressed(keys, _prevKeys, Keys.Space))
            StartGame();
        if (WasPressed(keys, _prevKeys, Keys.Escape))
            SetState(GameState.Start);
    }

    private void StartGame()  { InitState(); SetState(GameState.Playing); }
    private void ResumeGame() { _stepAccum = 0; SetState(GameState.Playing); }

    private bool TryGetDirection(KeyboardState keys, out Direction d)
    {
        bool wasd   = _settings.InputScheme != InputScheme.ArrowOnly;
        bool arrows = _settings.InputScheme != InputScheme.WASDOnly;

        if ((arrows && keys.IsKeyDown(Keys.Up))    || (wasd && keys.IsKeyDown(Keys.W)))
            { d = Direction.Up;    return true; }
        if ((arrows && keys.IsKeyDown(Keys.Down))  || (wasd && keys.IsKeyDown(Keys.S)))
            { d = Direction.Down;  return true; }
        if ((arrows && keys.IsKeyDown(Keys.Left))  || (wasd && keys.IsKeyDown(Keys.A)))
            { d = Direction.Left;  return true; }
        if ((arrows && keys.IsKeyDown(Keys.Right)) || (wasd && keys.IsKeyDown(Keys.D)))
            { d = Direction.Right; return true; }

        d = default; return false;
    }

    // ── Snake step ─────────────────────────────────────────────────────────
    private void StepSnake()
    {
        _dir = _nextDir;
        int nx = _snake[0].X + _dir.Dx;
        int ny = _snake[0].Y + _dir.Dy;

        if (nx < C.MinCol || nx > C.MaxCol || ny < C.MinRow || ny > C.MaxRow)
        { SetState(GameState.Dead); return; }

        _snake.Insert(0, new Point(nx, ny));
        bool ate = nx == _food.Cell.X && ny == _food.Cell.Y;
        if (!ate) _snake.RemoveAt(_snake.Count - 1);

        for (int i = 1; i < _snake.Count; i++)
            if (_snake[i].X == nx && _snake[i].Y == ny)
            { SetState(GameState.Dead); return; }

        if (ate)
        {
            if (_settings.SoundEnabled) SoundManager.PlayEat(_food.Def.Type);

            switch (_food.Def.Type)
            {
                case FoodType.Shrinker:
                    int rem = Math.Min(C.ShrinkSegments, _snake.Count - 1);
                    for (int i = 0; i < rem; i++) _snake.RemoveAt(_snake.Count - 1);
                    AddPopup(nx, ny, "SHRINK!", Color.MediumPurple);
                    break;
                case FoodType.Slow:
                    _slowActive    = true;
                    _slowRemaining = C.SlowDuration;
                    AddPopup(nx, ny, "SLOW!", Color.MediumAquamarine);
                    break;
                default:
                    _score += _food.Def.Points;
                    AddPopup(nx, ny, $"+{_food.Def.Points}", C.GoldColor);
                    break;
            }

            int prev = _level;
            _level = Math.Min(10, 1 + _score / C.PtsPerLevel);
            if (_level > prev)
            {
                _levelFlash = new LevelFlash { Text = $"LEVEL {_level}", Alpha = 1.5f };
                if (_settings.SoundEnabled) SoundManager.PlayLevelUp();
                TrySpawnKnight();
            }

            SpawnFood();
        }
    }

    // ── Knight / arrow logic ───────────────────────────────────────────────
    private void FireKnightArrow()
    {
        if (_knight == null || _snake.Count == 0) return;
        _knight.HasFired = true;

        // Aim directly at snake head cell
        var head = _snake[0];
        int dx = Math.Sign(head.X - _knight.Cell.X);
        int dy = Math.Sign(head.Y - _knight.Cell.Y);

        // Prefer the dominant axis so the arrow travels in one direction
        if (Math.Abs(head.X - _knight.Cell.X) >= Math.Abs(head.Y - _knight.Cell.Y))
            dy = 0;
        else
            dx = 0;

        if (dx == 0 && dy == 0) dx = 1; // fallback

        float startX = _knight.Cell.X * C.CellSize + C.CellSize / 2f;
        float startY = C.GridOffsetY + _knight.Cell.Y * C.CellSize + C.CellSize / 2f;

        _arrows.Add(new Arrow { X = startX, Y = startY, Dx = dx, Dy = dy });
    }

    private void StepArrows()
    {
        for (int i = _arrows.Count - 1; i >= 0; i--)
        {
            var a = _arrows[i];
            if (!a.Active) { _arrows.RemoveAt(i); continue; }

            a.X += a.Dx * C.CellSize;
            a.Y += a.Dy * C.CellSize;

            // Out of bounds → remove
            if (a.X < 0 || a.X > C.WindowW || a.Y < C.GridOffsetY ||
                a.Y > C.GridOffsetY + C.Rows * C.CellSize)
            { _arrows.RemoveAt(i); continue; }

            // Hit any snake segment → kill
            int cellX = (int)((a.X) / C.CellSize);
            int cellY = (int)((a.Y - C.GridOffsetY) / C.CellSize);
            foreach (var seg in _snake)
            {
                if (seg.X == cellX && seg.Y == cellY)
                { SetState(GameState.Dead); return; }
            }
        }
    }

    private void AddPopup(int cellX, int cellY, string text, Color color)
    {
        _popups.Add(new Popup
        {
            X     = cellX * C.CellSize + C.CellSize / 2f,
            Y     = cellY * C.CellSize + C.GridOffsetY + 4f,
            Text  = text,
            Alpha = 1f,
            DY    = 0f
        });
        _popupColors.Add(color);
    }

    // ══════════════════════════════════════════════════════════════════════
    // DRAW
    // ══════════════════════════════════════════════════════════════════════
    protected override void Draw(GameTime gt)
    {
        _foodPulse = (MathF.Sin((float)_totalMs * 0.004f) + 1f) * 0.5f;
        GraphicsDevice.Clear(C.BgDark);

        _sb.Begin(samplerState: SamplerState.PointClamp,
                  blendState:   BlendState.NonPremultiplied);

        DrawHUD();
        DrawFloor();
        DrawWalls();
        DrawFood();
        DrawKnight();
        DrawArrows();
        DrawSnake();
        DrawPopups();
        DrawLevelFlash();
        DrawFooter();

        switch (_state)
        {
            case GameState.Start:    DrawOverlayStart();    break;
            case GameState.Paused:   DrawOverlayPause();    break;
            case GameState.Settings: DrawOverlaySettings(); break;
            case GameState.Dead:     DrawOverlayDead();     break;
        }

        _sb.End();
        base.Draw(gt);
    }

    // ── HUD ────────────────────────────────────────────────────────────────
    private void DrawHUD()
    {
        FillRect(0, 0, C.WindowW, C.HudHeight, C.BgSurface);
        FillRect(0, C.HudHeight - 2, C.WindowW, 2, C.BorderColor);
        _font.DrawCentered(_sb, "DUNGEON SERPENT", C.WindowW / 2f, 8, C.GoldColor, 2f);
        DrawHudStat("SCORE", _score.ToString(), 20);
        DrawHudStatRight("LEVEL", _level.ToString(), C.WindowW - 20);

        if (_slowActive)
            _font.DrawCentered(_sb, $"SLOW {_slowRemaining:0.0}S",
                               C.WindowW / 2f, C.HudHeight - 14, Color.MediumAquamarine, 1f);

        // Knight warning
        if (_knight != null && !_knight.HasFired)
        {
            float pulse = (MathF.Sin((float)_totalMs * 0.01f) + 1f) * 0.5f;
            byte  pa    = (byte)(160 + pulse * 95);
            _font.DrawCentered(_sb, "! KNIGHT INCOMING !",
                               C.WindowW / 2f, C.HudHeight - 14,
                               new Color((byte)255,(byte)80,(byte)80,pa), 1f);
        }
    }

    private void DrawHudStat(string label, string value, float x)
    {
        _font.DrawAuto(_sb, label, x, 6,  C.DimColor,  1f);
        _font.DrawAuto(_sb, value, x, 20, C.TextColor, 2.5f);
    }

    private void DrawHudStatRight(string label, string value, float rightX)
    {
        _font.DrawAuto(_sb, label, rightX - _font.MeasureWidth(label, 1f),   6,  C.DimColor,  1f);
        _font.DrawAuto(_sb, value, rightX - _font.MeasureWidth(value, 2.5f), 20, C.TextColor, 2.5f);
    }

    // ── Floor ──────────────────────────────────────────────────────────────
    private void DrawFloor()
    {
        for (int c = C.MinCol; c <= C.MaxCol; c++)
        for (int r = C.MinRow; r <= C.MaxRow; r++)
        {
            int n = (c * 7 + r * 11) % 6;
            int v = 15 + n;
            FillRect(c * C.CellSize, C.GridOffsetY + r * C.CellSize, C.CellSize, C.CellSize,
                     new Color((byte)v, (byte)(v+2), (byte)(v+12)));
        }
    }

    // ── Walls ──────────────────────────────────────────────────────────────
    private void DrawWalls()
    {
        for (int c = 0; c < C.Cols; c++)
        for (int r = 0; r < C.Rows; r++)
        {
            if (c != 0 && c != C.Cols-1 && r != 0 && r != C.Rows-1) continue;
            DrawWallTile(c * C.CellSize, C.GridOffsetY + r * C.CellSize);
        }
    }

    private void DrawWallTile(int x, int y)
    {
        int cs = C.CellSize;
        FillRect(x, y, cs, cs, C.WallDark);
        FillRect(x+2, y+2, cs-4, cs-4, C.WallMid);
        FillRect(x+2, y+cs/2-1, cs-4, 2, C.WallDark);
        FillRect(x+2, y+2, cs-4, 2, new Color((byte)255,(byte)255,(byte)255,(byte)15));
        FillRect(x+2, y+2, 2, cs-4, new Color((byte)255,(byte)255,(byte)255,(byte)15));
        FillRect(x+2, y+cs-4, cs-4, 2, new Color((byte)0,(byte)0,(byte)0,(byte)64));
        FillRect(x+cs-4, y+2, 2, cs-4, new Color((byte)0,(byte)0,(byte)0,(byte)64));
    }

    // ── Food ───────────────────────────────────────────────────────────────
    private void DrawFood()
    {
        int fi   = (int)_food.Def.Type;
        float p  = 1f + _foodPulse * 0.15f;
        int sz   = (int)((C.CellSize - 4) * p);
        int cx   = _food.Cell.X * C.CellSize + C.CellSize / 2;
        int cy   = C.GridOffsetY + _food.Cell.Y * C.CellSize + C.CellSize / 2;
        var gc   = _food.Def.GlowColor;
        int halo = sz + 8;
        FillRect(cx-halo/2, cy-halo/2, halo, halo,
                 new Color(gc.R, gc.G, gc.B, (byte)(80 + _foodPulse * 60)));
        _sb.Draw(_foodSprites[fi],
                 new Rectangle(cx-sz/2, cy-sz/2, sz, sz), Color.White);
    }

    // ── Dragon snake ───────────────────────────────────────────────────────
    private void DrawSnake()
    {
        int n = _snake.Count;
        for (int i = n - 1; i >= 0; i--)
            DrawDragonSegment(i, n);
        if (n > 0)
            DrawDragonHead(_snake[0]);
    }

    private void DrawDragonSegment(int i, int n)
    {
        var cell = _snake[i];
        float t  = 1f - (float)i / n;   // 1=head 0=tail
        int px   = cell.X * C.CellSize + 2;
        int py   = C.GridOffsetY + cell.Y * C.CellSize + 2;
        int s    = C.CellSize - 4;

        if (i == 0) return; // head drawn separately

        // Colour: interpolate deep red → orange toward head
        byte r = (byte)(180 + t * 40);
        byte g = (byte)(30  + t * 50);
        byte b = (byte)(20);
        Color body = new Color(r, g, b);
        FillRoundRect(px, py, s, s, 5, body);

        // Belly stripe down the centre (gold tint)
        int bw = s / 3;
        int bx = px + (s - bw) / 2;
        Color belly = new Color(
            (byte)(200),
            (byte)(int)(160 * t + 60 * (1f - t)),
            (byte)60);
        FillRect(bx, py + 2, bw, s - 4, belly);

        // Diamond scale pattern on alternating segments
        if (i % 2 == 0)
        {
            // Dark diamond outline
            int mx = px + s / 2, my = py + s / 2;
            int ds = s / 4;
            FillRect(mx - 1, my - ds, 2, ds * 2, new Color((byte)80,(byte)10,(byte)10,(byte)160));
            FillRect(mx - ds, my - 1, ds * 2, 2,  new Color((byte)80,(byte)10,(byte)10,(byte)160));
        }

        // Outline
        int oc = (int)(40 + t * 60);
        DrawRoundRectOutline(px, py, s, s, 5, new Color((byte)oc,(byte)0,(byte)0,(byte)180));
    }

    private void DrawDragonHead(Point seg)
    {
        int px = seg.X * C.CellSize + 2;
        int py = C.GridOffsetY + seg.Y * C.CellSize + 2;
        int s  = C.CellSize - 4;

        // Head base — bright orange-red
        Color headCol = new Color((byte)220, (byte)60, (byte)20);
        FillRoundRect(px, py, s, s, 5, headCol);

        // Belly patch on head
        int bw = s / 3;
        FillRect(px + (s - bw) / 2, py + 2, bw, s - 4,
                 new Color((byte)220, (byte)170, (byte)60));

        // Outline
        DrawRoundRectOutline(px, py, s, s, 5, new Color((byte)140,(byte)20,(byte)0,(byte)200));

        // Horns — two small triangles pointing away from movement direction
        DrawHorns(seg, s);

        // Eyes
        DrawDragonEyes(seg, s);

        // Tongue
        DrawTongue(seg, s);
    }

    private void DrawHorns(Point seg, int s)
    {
        int px = seg.X * C.CellSize + 2;
        int py = C.GridOffsetY + seg.Y * C.CellSize + 2;
        Color hc = C.DragonHorn;

        // Horns protrude perpendicular to travel direction, at the back of the head
        if (_dir.Dx != 0) // moving left or right — horns point up
        {
            int hornX1 = px + s / 4;
            int hornX2 = px + 3 * s / 4;
            // Left horn
            FillRect(hornX1 - 1, py - 4, 3, 5, hc);
            FillRect(hornX1,     py - 6, 1, 3, hc);
            // Right horn
            FillRect(hornX2 - 1, py - 4, 3, 5, hc);
            FillRect(hornX2,     py - 6, 1, 3, hc);
        }
        else // moving up or down — horns point left
        {
            int hornY1 = py + s / 4;
            int hornY2 = py + 3 * s / 4;
            FillRect(px - 4, hornY1 - 1, 5, 3, hc);
            FillRect(px - 6, hornY1,     3, 1, hc);
            FillRect(px - 4, hornY2 - 1, 5, 3, hc);
            FillRect(px - 6, hornY2,     3, 1, hc);
        }
    }

    private void DrawDragonEyes(Point seg, int s)
    {
        int x  = seg.X * C.CellSize;
        int y  = C.GridOffsetY + seg.Y * C.CellSize;
        int cs = C.CellSize;

        (int ex1, int ey1, int ex2, int ey2) eyes;
        if      (_dir.Dx ==  1) eyes = (x+cs-8, y+7,    x+cs-8, y+cs-7);
        else if (_dir.Dx == -1) eyes = (x+8,    y+7,    x+8,    y+cs-7);
        else if (_dir.Dy == -1) eyes = (x+7,    y+8,    x+cs-7, y+8);
        else                    eyes = (x+7,    y+cs-8, x+cs-7, y+cs-8);

        foreach ((int ex, int ey) in new[]{ (eyes.ex1,eyes.ey1),(eyes.ex2,eyes.ey2) })
        {
            // Slit pupil eye (dragon-like: vertical slit)
            FillCircle(ex, ey, 4, new Color((byte)255,(byte)200,(byte)0));  // yellow iris
            FillRect(ex-1, ey-3, 2, 6, Color.Black);                        // vertical slit
            FillCircle(ex-1, ey-2, 1,
                       new Color((byte)255,(byte)255,(byte)255,(byte)180));  // glint
        }
    }

    private void DrawTongue(Point seg, int s)
    {
        int px = seg.X * C.CellSize + 2;
        int py = C.GridOffsetY + seg.Y * C.CellSize + 2;
        Color tc = new Color((byte)220, (byte)30, (byte)60);

        // Tongue extends from the front face of the head
        int mid = s / 2;
        int len = 5;
        if      (_dir.Dx ==  1)
        {
            FillRect(px + s,     py + mid - 1, len,   2, tc);
            FillRect(px + s + len - 1, py + mid - 2, 2, 2, tc);
            FillRect(px + s + len - 1, py + mid + 1, 2, 2, tc);
        }
        else if (_dir.Dx == -1)
        {
            FillRect(px - len,  py + mid - 1, len,   2, tc);
            FillRect(px - len,  py + mid - 2, 2, 2, tc);
            FillRect(px - len,  py + mid + 1, 2, 2, tc);
        }
        else if (_dir.Dy == -1)
        {
            FillRect(px + mid - 1, py - len,  2, len, tc);
            FillRect(px + mid - 2, py - len,  2, 2, tc);
            FillRect(px + mid + 1, py - len,  2, 2, tc);
        }
        else
        {
            FillRect(px + mid - 1, py + s,    2, len, tc);
            FillRect(px + mid - 2, py + s + len - 1, 2, 2, tc);
            FillRect(px + mid + 1, py + s + len - 1, 2, 2, tc);
        }
    }

    // ── Knight drawing ─────────────────────────────────────────────────────
    private void DrawKnight()
    {
        if (_knight == null) return;

        int x  = _knight.Cell.X * C.CellSize;
        int y  = C.GridOffsetY + _knight.Cell.Y * C.CellSize;
        int cs = C.CellSize;

        // Pulsing warning outline before firing
        if (!_knight.HasFired)
        {
            float pulse = (MathF.Sin((float)_totalMs * 0.008f) + 1f) * 0.5f;
            byte  pa    = (byte)(60 + pulse * 120);
            FillRect(x-2, y-2, cs+4, cs+4,
                     new Color((byte)255,(byte)80,(byte)80,pa));
        }

        // Body (armour plate)
        FillRect(x+4,  y+10, cs-8, cs-12, C.KnightArmor);

        // Helmet
        FillRect(x+5,  y+2,  cs-10, 10, C.KnightArmor);

        // Visor (dark slit)
        FillRect(x+7,  y+5,  cs-14, 4,  C.KnightVisor);

        // Plume on top of helmet
        FillRect(x+cs/2-1, y,   2, 4, new Color((byte)200,(byte)30,(byte)30));

        // Legs
        FillRect(x+5,  y+cs-10, 6, 8, C.KnightArmor);
        FillRect(x+cs-11, y+cs-10, 6, 8, C.KnightArmor);

        // Sword — points inward toward the playfield
        switch (_knight.WallSide)
        {
            case 0: // top wall — sword points down
                FillRect(x+cs-6, y+cs-2, 2, 10, C.ArrowColor);
                FillRect(x+cs-8, y+cs-2, 6, 2,  C.ArrowColor); // guard
                break;
            case 1: // bottom — sword points up
                FillRect(x+cs-6, y-8,    2, 10, C.ArrowColor);
                FillRect(x+cs-8, y,      6, 2,  C.ArrowColor);
                break;
            case 2: // left — sword points right
                FillRect(x+cs-2, y+cs-6, 10, 2, C.ArrowColor);
                FillRect(x+cs-2, y+cs-8, 2,  6, C.ArrowColor);
                break;
            default: // right — sword points left
                FillRect(x-8,   y+cs-6, 10, 2, C.ArrowColor);
                FillRect(x,     y+cs-8, 2,  6, C.ArrowColor);
                break;
        }
    }

    // ── Arrow drawing ──────────────────────────────────────────────────────
    private void DrawArrows()
    {
        foreach (var a in _arrows)
        {
            if (!a.Active) continue;
            int ax = (int)a.X;
            int ay = (int)a.Y;

            // Shaft
            if (a.Dx != 0) // horizontal
            {
                FillRect(ax - 8, ay - 1, 16, 3, C.ArrowColor);
                // Tip
                int tipX = a.Dx > 0 ? ax + 8 : ax - 10;
                FillRect(tipX, ay - 2, 2, 5, C.ArrowTip);
                // Fletching
                int fletchX = a.Dx > 0 ? ax - 10 : ax + 8;
                FillRect(fletchX, ay - 3, 3, 2, C.ArrowColor);
                FillRect(fletchX, ay + 2, 3, 2, C.ArrowColor);
            }
            else // vertical
            {
                FillRect(ax - 1, ay - 8, 3, 16, C.ArrowColor);
                int tipY = a.Dy > 0 ? ay + 8 : ay - 10;
                FillRect(ax - 2, tipY, 5, 2, C.ArrowTip);
                int fletchY = a.Dy > 0 ? ay - 10 : ay + 8;
                FillRect(ax - 3, fletchY, 2, 3, C.ArrowColor);
                FillRect(ax + 2, fletchY, 2, 3, C.ArrowColor);
            }
        }
    }

    // ── Popups ─────────────────────────────────────────────────────────────
    private void DrawPopups()
    {
        for (int i = 0; i < _popups.Count; i++)
        {
            var p = _popups[i];
            byte a = (byte)(Math.Clamp(p.Alpha, 0f, 1f) * 255);
            Color c = i < _popupColors.Count ? _popupColors[i] : C.GoldColor;
            c.A = a;
            float w = _font.MeasureWidth(p.Text.ToUpperInvariant(), 1.5f);
            _font.DrawAuto(_sb, p.Text, p.X - w / 2f, p.Y + p.DY, c, 1.5f);
        }
    }

    // ── Level flash ────────────────────────────────────────────────────────
    private void DrawLevelFlash()
    {
        if (_levelFlash == null) return;
        byte a = (byte)(Math.Clamp(_levelFlash.Alpha, 0f, 1f) * 217);
        _font.DrawCentered(_sb, _levelFlash.Text,
                           C.WindowW / 2f,
                           C.GridOffsetY + C.Rows * C.CellSize / 2f - 14f,
                           new Color((byte)170,(byte)136,(byte)255,a), 3.5f);
    }

    // ── Footer ─────────────────────────────────────────────────────────────
    private void DrawFooter()
    {
        int fy = C.GridOffsetY + C.Rows * C.CellSize + 6;
        FillRect(0, C.GridOffsetY + C.Rows * C.CellSize, C.WindowW, C.FooterHeight, C.BgSurface);
        // Two short lines instead of one long overflow line
        _font.DrawCentered(_sb, "COLLECT TREASURES  AVOID WALLS",
                           C.WindowW / 2f, fy - 4, C.DimColor, 1f);
        _font.DrawCentered(_sb, "DON'T BITE YOURSELF",
                           C.WindowW / 2f, fy + 10, C.DimColor, 1f);
    }

    // ══════════════════════════════════════════════════════════════════════
    // OVERLAY SCREENS
    // ══════════════════════════════════════════════════════════════════════

    // ── Start ──────────────────────────────────────────────────────────────
    private void DrawOverlayStart()
    {
        DrawOverlayBg();
        // Panel: 340 wide so all text fits with padding
        int pw = 340, ph = 450;
        int panelX = C.WindowW / 2 - pw / 2;
        int panelY = C.GridOffsetY + 16;
        DrawPanel(panelX, panelY, pw, ph);

        int cx = C.WindowW / 2;
        int y  = panelY + 22;

        _font.DrawCentered(_sb, "DUNGEON", cx, y,       C.GoldColor, 3f);
        _font.DrawCentered(_sb, "SERPENT", cx, y + 28,  C.GoldColor, 3f);
        y += 64;

        _font.DrawCentered(_sb, "AN ANCIENT SERPENT STIRS", cx, y, C.DimColor, 1f);
        y += 16;

        string scheme = _settings.InputScheme switch
        {
            InputScheme.WASDOnly  => "WASD TO MOVE",
            InputScheme.ArrowOnly => "ARROWS TO MOVE",
            _                     => "WASD / ARROWS TO MOVE"
        };
        _font.DrawCentered(_sb, scheme,           cx, y,      C.DimColor, 1f);
        _font.DrawCentered(_sb, "P / SPACE: PAUSE", cx, y+14, C.DimColor, 1f);
        y += 34;

        DrawLegendItem(cx, y,      C.GoldColor,              "COIN     +10 PTS");
        DrawLegendItem(cx, y + 18, new Color(255, 68, 102),  "RUBY     +25 PTS");
        DrawLegendItem(cx, y + 36, new Color(68,  221, 255), "DIAMOND  +50 PTS");
        DrawLegendItem(cx, y + 54, new Color(160, 60,  220), "SHRINKER  SHRINKS");
        DrawLegendItem(cx, y + 72, new Color(60,  200, 140), "SLOW     5S SLOW");
        DrawLegendItem(cx, y + 90, C.KnightArmor,            "KNIGHT   FIRES ARROW");
        y += 116;

        _font.DrawCentered(_sb, $"HIGH SCORE: {_highScore}", cx, y, C.DimColor, 1f);
        y += 22;

        // Button wide enough for its text (ENTER DUNGEON = 104px + padding)
        DrawButton(cx - 80, y, 160, 26, "ENTER DUNGEON");
    }

    // ── Pause ──────────────────────────────────────────────────────────────
    private void DrawOverlayPause()
    {
        DrawOverlayBg();
        int cx = C.WindowW / 2;
        int cy = C.GridOffsetY + C.Rows * C.CellSize / 2;

        // Panel: 260 wide, 220 tall
        DrawPanel(cx - 130, cy - 100, 260, 222);

        _font.DrawCentered(_sb, "PAUSED", cx, cy - 88, C.TextColor, 3f);
        _font.DrawCentered(_sb, "SERPENT LURKS IN SHADOW", cx, cy - 56, C.DimColor, 1f);

        string[] items = { "RESUME GAME", "SETTINGS", "MAIN MENU" };
        int itemY = cy - 34;
        for (int i = 0; i < items.Length; i++)
        {
            bool sel = i == _pauseSelected;
            if (sel) DrawButtonSelected(cx - 90, itemY + i * 34, 180, 26, items[i]);
            else     DrawButton        (cx - 90, itemY + i * 34, 180, 26, items[i]);
        }

        // Two short hint lines that fit inside the panel
        _font.DrawCentered(_sb, "UP/DOWN: SELECT",    cx, cy + 76, C.DimColor, 1f);
        _font.DrawCentered(_sb, "ENTER: CONFIRM",     cx, cy + 90, C.DimColor, 1f);
    }

    // ── Settings ───────────────────────────────────────────────────────────
    private void DrawOverlaySettings()
    {
        DrawOverlayBg();
        int cx = C.WindowW / 2;
        int cy = C.GridOffsetY + C.Rows * C.CellSize / 2;

        DrawPanel(cx - 150, cy - 120, 300, 258);

        _font.DrawCentered(_sb, "SETTINGS", cx, cy - 108, C.TextColor, 3f);

        int rh = 28, rowY = cy - 62;

        // Row 0: Sound
        DrawSettingsRow(cx, rowY, 0, "SOUND",
                        _settings.SoundEnabled ? "ON" : "OFF",
                        _settings.SoundEnabled ? C.GoldColor : C.DimColor);

        // Row 1: Controls
        string schemeStr = _settings.InputScheme switch
        {
            InputScheme.WASDOnly  => "WASD",
            InputScheme.ArrowOnly => "ARROWS",
            _                     => "BOTH"
        };
        DrawSettingsRow(cx, rowY + rh + 6, 1, "CONTROLS", schemeStr, C.TextColor);

        if (_settingsSelected == 1)
            _font.DrawCentered(_sb, "LEFT/RIGHT TO CYCLE",
                               cx, rowY + rh + 6 + 24, C.DimColor, 1f);

        // Row 2: Back
        int backY = rowY + (rh + 6) * 2 + 14;
        if (_settingsSelected == 2) DrawButtonSelected(cx - 50, backY, 100, 26, "BACK");
        else                        DrawButton        (cx - 50, backY, 100, 26, "BACK");

        _font.DrawCentered(_sb, "UP/DOWN: SELECT", cx, cy + 110, C.DimColor, 1f);
        _font.DrawCentered(_sb, "ENTER: CONFIRM",  cx, cy + 124, C.DimColor, 1f);
    }

    private void DrawSettingsRow(int cx, int y, int rowIndex,
                                 string label, string value, Color valueColor)
    {
        bool sel  = rowIndex == _settingsSelected;
        int  pw   = 280;
        int  ph   = 26;
        int  x    = cx - pw / 2;

        if (sel)
        {
            FillRect(x, y, pw, ph, new Color((byte)40,(byte)30,(byte)90,(byte)200));
            DrawRectOutline(x, y, pw, ph, C.AccentColor);
            _font.DrawAuto(_sb, ">", x + 4, y + (ph - 8) / 2f, C.AccentColor, 1f);
        }
        else
        {
            DrawRectOutline(x, y, pw, ph, C.BorderColor);
        }

        _font.DrawAuto(_sb, label,
                       x + 18, y + (ph - 8) / 2f,
                       sel ? C.TextColor : C.DimColor, 1f);

        float vw = _font.MeasureWidth(value, 1f);
        _font.DrawAuto(_sb, value,
                       x + pw - vw - 12, y + (ph - 8) / 2f,
                       valueColor, 1f);
    }

    // ── Dead ───────────────────────────────────────────────────────────────
    private void DrawOverlayDead()
    {
        DrawOverlayBg();
        int cx = C.WindowW / 2;
        int cy = C.GridOffsetY + C.Rows * C.CellSize / 2;
        DrawPanel(cx - 140, cy - 96, 280, 220);

        _font.DrawCentered(_sb, "YOU DIED", cx, cy - 82, C.RedColor, 3f);

        _font.DrawCentered(_sb, "SCORE", cx, cy - 40, C.DimColor, 1f);
        _font.DrawCentered(_sb, _score.ToString(),     cx, cy - 26, C.GoldColor, 2.5f);

        _font.DrawCentered(_sb, "BEST",  cx, cy + 2,  C.DimColor, 1f);
        _font.DrawCentered(_sb, _highScore.ToString(), cx, cy + 16, C.GoldColor, 2.5f);

        if (_newRecord)
        {
            float pulse = (MathF.Sin((float)_totalMs * 0.006f) + 1f) * 0.5f;
            byte  pa    = (byte)(128 + pulse * 127);
            _font.DrawCentered(_sb, "* NEW RECORD *", cx, cy + 50,
                               new Color((byte)255,(byte)215,(byte)0,pa), 1.5f);
        }

        DrawButton(cx - 88, cy + 76, 176, 26, "DESCEND AGAIN");
        _font.DrawCentered(_sb, "ESC: MAIN MENU", cx, cy + 112, C.DimColor, 1f);
    }

    // ── Overlay helpers ────────────────────────────────────────────────────
    private void DrawOverlayBg()
    {
        FillRect(0, C.GridOffsetY, C.WindowW, C.Rows * C.CellSize,
                 new Color((byte)4,(byte)4,(byte)12,(byte)224));
    }

    private void DrawPanel(int x, int y, int w, int h)
    {
        FillRect(x, y, w, h, C.BgSurface);
        DrawRectOutline(x, y, w, h, C.BorderColor);
        FillRect(x + 18, y + 8,     w - 36, 1, C.BorderColor);
        FillRect(x + 18, y + h - 8, w - 36, 1, C.BorderColor);
    }

    private void DrawButton(int x, int y, int w, int h, string label)
    {
        DrawRectOutline(x, y, w, h, C.AccentColor);
        _font.DrawCentered(_sb, label, x + w / 2f, y + (h - 8) / 2f,
                           new Color((byte)153,(byte)136,(byte)255), 1f);
    }

    private void DrawButtonSelected(int x, int y, int w, int h, string label)
    {
        FillRect(x, y, w, h, new Color((byte)40,(byte)30,(byte)90,(byte)220));
        DrawRectOutline(x, y, w, h, C.AccentColor);
        _font.DrawAuto(_sb, ">", x + 6, y + (h - 8) / 2f, C.AccentColor, 1f);
        _font.DrawCentered(_sb, label, x + w / 2f, y + (h - 8) / 2f, Color.White, 1f);
    }

    private void DrawLegendItem(int cx, int y, Color dotColor, string text)
    {
        int dotX = cx - 90;
        FillCircle(dotX, y + 4, 4, dotColor);
        _font.DrawAuto(_sb, text, dotX + 12, y, C.DimColor, 1f);
    }

    // ── Primitives ─────────────────────────────────────────────────────────
    private void FillRect(int x, int y, int w, int h, Color c)
    {
        if (w <= 0 || h <= 0) return;
        _sb.Draw(_pixel, new Rectangle(x, y, w, h), c);
    }

    private void DrawRectOutline(int x, int y, int w, int h, Color c)
    {
        FillRect(x,     y,     w, 1, c);
        FillRect(x,     y+h-1, w, 1, c);
        FillRect(x,     y,     1, h, c);
        FillRect(x+w-1, y,     1, h, c);
    }

    private void FillRoundRect(int x, int y, int w, int h, int r, Color c)
    {
        FillRect(x+r, y,   w-2*r, h,     c);
        FillRect(x,   y+r, r,     h-2*r, c);
        FillRect(x+w-r, y+r, r,   h-2*r, c);
        FillCircleQuad(x+r,   y+r,   r, 0, c);
        FillCircleQuad(x+w-r, y+r,   r, 1, c);
        FillCircleQuad(x+r,   y+h-r, r, 2, c);
        FillCircleQuad(x+w-r, y+h-r, r, 3, c);
    }

    private void DrawRoundRectOutline(int x, int y, int w, int h, int r, Color c)
    {
        FillRect(x+r,   y,     w-2*r, 1, c);
        FillRect(x+r,   y+h-1, w-2*r, 1, c);
        FillRect(x,     y+r,   1, h-2*r, c);
        FillRect(x+w-1, y+r,   1, h-2*r, c);
    }

    private void FillCircleQuad(int cx, int cy, int r, int quad, Color c)
    {
        for (int dy = 0; dy < r; dy++)
        for (int dx = 0; dx < r; dx++)
        {
            if (dx*dx + dy*dy <= r*r)
            {
                int qx = (quad==0||quad==2) ? cx-r+dx : cx+dx-r;
                int qy = (quad==0||quad==1) ? cy-r+dy : cy+dy-r;
                FillRect(qx, qy, 1, 1, c);
            }
        }
    }

    private void FillCircle(int cx, int cy, int r, Color c)
    {
        for (int dy = -r; dy <= r; dy++)
        for (int dx = -r; dx <= r; dx++)
            if (dx*dx + dy*dy <= r*r)
                FillRect(cx+dx, cy+dy, 1, 1, c);
    }

    private void FillEllipse(int cx, int cy, int rx, int ry, Color c)
    {
        for (int dy = -ry; dy <= ry; dy++)
        for (int dx = -rx; dx <= rx; dx++)
        {
            float nx2 = (float)dx/rx, ny2 = (float)dy/ry;
            if (nx2*nx2 + ny2*ny2 <= 1f)
                FillRect(cx+dx, cy+dy, 1, 1, c);
        }
    }

    private static bool WasPressed(KeyboardState cur, KeyboardState prev, Keys k)
        => cur.IsKeyDown(k) && prev.IsKeyUp(k);
}
