'use strict';
// ── Config ────────────────────────────────────────────────────────────────────
const COLS    = 20;
const ROWS    = 20;
const CELL    = 30;
const W       = COLS * CELL;   // 600px
const H       = ROWS * CELL;   // 600px
const MIN_COL = 1;
const MAX_COL = COLS - 2;      // 18  (column 0 and 19 are walls)
const MIN_ROW = 1;
const MAX_ROW = ROWS - 2;      // 18
// Milliseconds between steps, indexed by level-1
const SPEEDS = [160, 140, 120, 105, 90, 76, 64, 55, 47, 40];
const PTS_PER_LEVEL = 100;
const FOOD_DEFS = [
    { id: 'coin',    pts: 10, color: '#ffd700', glow: 'rgba(255,200,0,0.45)',  r: 8 },
    { id: 'ruby',    pts: 25, color: '#ff4466', glow: 'rgba(255,60,100,0.45)', r: 7 },
    { id: 'diamond', pts: 50, color: '#44ddff', glow: 'rgba(50,220,255,0.5)',  r: 7 },
];
// Cumulative weights: 60% coin, 30% ruby, 10% diamond
const FOOD_CUM = [60, 90, 100];
const LS_KEY = 'dungeonSerpent_hi';
// ── Canvas setup ──────────────────────────────────────────────────────────────
const canvas = document.getElementById('canvas');
const ctx    = canvas.getContext('2d');
canvas.width  = W;
canvas.height = H;
// ── DOM references ────────────────────────────────────────────────────────────
const elScore      = document.getElementById('score');
const elLevel      = document.getElementById('level');
const elBestStart  = document.getElementById('best-start');
const elFinalScore = document.getElementById('final-score');
const elFinalBest  = document.getElementById('final-best');
const elNewRecord  = document.getElementById('new-record');
document.getElementById('btn-start').onclick   = startGame;
document.getElementById('btn-resume').onclick  = resumeGame;
document.getElementById('btn-restart').onclick = startGame;
// ── Game state ────────────────────────────────────────────────────────────────
let snake, dir, nextDir, food;
let score, level, highScore;
let state;       // 'start' | 'playing' | 'paused' | 'dead'
let lastStep;
let popups       = [];    // score pop-ups: { x, y, text, alpha, dy }
let levelFlash   = null;  // { text, alpha }
let foodPulse    = 0;     // 0..1 oscillator driven by time
highScore = parseInt(localStorage.getItem(LS_KEY) || '0', 10);
// ── Input ─────────────────────────────────────────────────────────────────────
const DIR_MAP = {
    ArrowUp:    { dx: 0, dy: -1 }, ArrowDown:  { dx: 0, dy:  1 },
    ArrowLeft:  { dx: -1, dy: 0 }, ArrowRight: { dx:  1, dy: 0 },
    w: { dx: 0, dy: -1 }, W: { dx: 0, dy: -1 },
    s: { dx: 0, dy:  1 }, S: { dx: 0, dy:  1 },
    a: { dx: -1, dy: 0 }, A: { dx: -1, dy: 0 },
    d: { dx:  1, dy: 0 }, D: { dx:  1, dy: 0 },
};
document.addEventListener('keydown', e => {
    if (e.key === ' ' || e.key === 'p' || e.key === 'P') {
        e.preventDefault();
        if (state === 'playing') pauseGame();
        else if (state === 'paused') resumeGame();
        return;
    }
    const d = DIR_MAP[e.key];
    if (!d) return;
    e.preventDefault();
    if (d.dx !== 0 && d.dx === -dir.dx) return; // block 180° reversal
    if (d.dy !== 0 && d.dy === -dir.dy) return;
    nextDir = d;
    if (state === 'start') startGame();
});
// Swipe support for mobile
let swipeStart = null;
canvas.addEventListener('touchstart', e => {
    swipeStart = { x: e.touches[0].clientX, y: e.touches[0].clientY };
}, { passive: true });
canvas.addEventListener('touchend', e => {
    if (!swipeStart) return;
    const dx = e.changedTouches[0].clientX - swipeStart.x;
    const dy = e.changedTouches[0].clientY - swipeStart.y;
    swipeStart = null;
    if (Math.abs(dx) < 20 && Math.abs(dy) < 20) return;
    let d;
    if (Math.abs(dx) > Math.abs(dy)) d = dx > 0 ? { dx: 1, dy: 0 } : { dx: -1, dy: 0 };
    else                              d = dy > 0 ? { dx: 0, dy: 1 } : { dx: 0, dy: -1 };
    if (d.dx !== 0 && d.dx === -dir.dx) return;
    if (d.dy !== 0 && d.dy === -dir.dy) return;
    nextDir = d;
}, { passive: true });
// ── Game logic ────────────────────────────────────────────────────────────────
function initState() {
    const hx = MIN_COL + Math.floor((MAX_COL - MIN_COL) / 2) + 1;
    const hy = MIN_ROW + Math.floor((MAX_ROW - MIN_ROW) / 2);
    snake      = [{ x: hx, y: hy }, { x: hx - 1, y: hy }, { x: hx - 2, y: hy }];
    dir        = { dx: 1, dy: 0 };
    nextDir    = { dx: 1, dy: 0 };
    score      = 0;
    level      = 1;
    popups     = [];
    levelFlash = null;
    spawnFood();
    updateHUD();
}
function startGame() {
    initState();
    lastStep = performance.now();
    setState('playing');
}
function pauseGame() {
    setState('paused');
}
function resumeGame() {
    lastStep = performance.now();
    setState('playing');
}
function setState(s) {
    state = s;
    document.getElementById('overlay-start').classList.toggle('hidden', s !== 'start');
    document.getElementById('overlay-pause').classList.toggle('hidden', s !== 'paused');
    document.getElementById('overlay-dead').classList.toggle('hidden',  s !== 'dead');
    if (s === 'start') {
        elBestStart.textContent = highScore;
    }
    if (s === 'dead') {
        const newRecord = score > highScore;
        if (newRecord) {
            highScore = score;
            localStorage.setItem(LS_KEY, highScore);
        }
        elFinalScore.textContent = score;
        elFinalBest.textContent  = highScore;
        elNewRecord.classList.toggle('hidden', !newRecord);
    }
}
function spawnFood() {
    const occupied = new Set(snake.map(s => `${s.x},${s.y}`));
    let x, y, attempts = 0;
    do {
        x = MIN_COL + Math.floor(Math.random() * (MAX_COL - MIN_COL + 1));
        y = MIN_ROW + Math.floor(Math.random() * (MAX_ROW - MIN_ROW + 1));
        attempts++;
    } while (occupied.has(`${x},${y}`) && attempts < 500);
    const r  = Math.random() * 100;
    const fi = FOOD_CUM.findIndex(c => r < c);
    food = { x, y, def: FOOD_DEFS[fi < 0 ? 0 : fi] };
}
function step(now) {
    if (state !== 'playing') return;
    const speed = SPEEDS[Math.min(level - 1, SPEEDS.length - 1)];
    if (now - lastStep < speed) return;
    lastStep = now;
    dir = nextDir;
    const nx = snake[0].x + dir.dx;
    const ny = snake[0].y + dir.dy;
    // Wall collision
    if (nx < MIN_COL || nx > MAX_COL || ny < MIN_ROW || ny > MAX_ROW) {
        setState('dead'); return;
    }
    // Move: prepend new head
    snake.unshift({ x: nx, y: ny });
    // Check food before removing tail (snake grows on eat)
    const ate = nx === food.x && ny === food.y;
    if (!ate) snake.pop();
    // Self-collision (new head against the rest of the body)
    for (let i = 1; i < snake.length; i++) {
        if (snake[i].x === nx && snake[i].y === ny) {
            setState('dead'); return;
        }
    }
    if (ate) {
        const pts = food.def.pts;
        score += pts;
        popups.push({ x: nx * CELL + CELL / 2, y: ny * CELL + 4, text: `+${pts}`, alpha: 1, dy: 0 });
        const prevLv = level;
        level = Math.min(10, 1 + Math.floor(score / PTS_PER_LEVEL));
        if (level > prevLv) levelFlash = { text: `LEVEL ${level}`, alpha: 1.5 };
        updateHUD();
        spawnFood();
    }
}
function updateHUD() {
    elScore.textContent = score;
    elLevel.textContent = level;
}
// ── Rendering ─────────────────────────────────────────────────────────────────
function draw(now) {
    foodPulse = (Math.sin(now * 0.004) + 1) * 0.5;
    ctx.fillStyle = '#080814';
    ctx.fillRect(0, 0, W, H);
    drawFloor();
    drawWalls();
    drawFood();
    drawSnake();
    drawPopups();
    drawLevelFlash();
}
function drawFloor() {
    for (let c = MIN_COL; c <= MAX_COL; c++) {
        for (let r = MIN_ROW; r <= MAX_ROW; r++) {
            const n = (c * 7 + r * 11) % 6;
            const v = 15 + n;
            ctx.fillStyle = `rgb(${v},${v + 2},${v + 12})`;
            ctx.fillRect(c * CELL, r * CELL, CELL, CELL);
            ctx.strokeStyle = 'rgba(255,255,255,0.025)';
            ctx.lineWidth = 0.5;
            ctx.strokeRect(c * CELL, r * CELL, CELL, CELL);
        }
    }
}
function drawWalls() {
    for (let c = 0; c < COLS; c++) {
        for (let r = 0; r < ROWS; r++) {
            if (c !== 0 && c !== COLS - 1 && r !== 0 && r !== ROWS - 1) continue;
            drawWallTile(c * CELL, r * CELL);
        }
    }
}
function drawWallTile(x, y) {
    ctx.fillStyle = '#161e38';
    ctx.fillRect(x, y, CELL, CELL);
    ctx.fillStyle = '#1e2a4a';
    ctx.fillRect(x + 2, y + 2, CELL - 4, CELL - 4);
    // Mortar line
    ctx.fillStyle = '#131d35';
    ctx.fillRect(x + 2, y + Math.floor(CELL / 2) - 1, CELL - 4, 2);
    // Highlight edges
    ctx.fillStyle = 'rgba(255,255,255,0.06)';
    ctx.fillRect(x + 2, y + 2, CELL - 4, 2);
    ctx.fillRect(x + 2, y + 2, 2, CELL - 4);
    ctx.fillStyle = 'rgba(0,0,0,0.25)';
    ctx.fillRect(x + 2, y + CELL - 4, CELL - 4, 2);
    ctx.fillRect(x + CELL - 4, y + 2, 2, CELL - 4);
}
function drawFood() {
    const { x, y, def } = food;
    const cx = x * CELL + CELL / 2;
    const cy = y * CELL + CELL / 2;
    const r  = def.r + foodPulse * 2.5;
    const g = ctx.createRadialGradient(cx, cy, 0, cx, cy, r * 2.8);
    g.addColorStop(0, def.glow);
    g.addColorStop(1, 'transparent');
    ctx.beginPath();
    ctx.arc(cx, cy, r * 2.8, 0, Math.PI * 2);
    ctx.fillStyle = g;
    ctx.fill();
    ctx.beginPath();
    ctx.arc(cx, cy, r, 0, Math.PI * 2);
    ctx.fillStyle = def.color;
    ctx.fill();
    ctx.beginPath();
    ctx.arc(cx - r * 0.3, cy - r * 0.35, r * 0.38, 0, Math.PI * 2);
    ctx.fillStyle = 'rgba(255,255,255,0.55)';
    ctx.fill();
}
function drawSnake() {
    const n = snake.length;
    for (let i = n - 1; i >= 0; i--) drawSegment(i, n);
    if (n > 0) drawEyes(snake[0]);
}
function drawSegment(i, n) {
    const { x, y } = snake[i];
    const t  = 1 - i / n;
    const px = x * CELL + 2;
    const py = y * CELL + 2;
    const s  = CELL - 4;
    const gr = Math.floor(80  + t * 100);
    const gb = Math.floor(20  + t * 40);
    ctx.fillStyle = `rgb(20,${gr},${gb})`;
    rrect(px, py, s, s, 6);
    ctx.fill();
    if (i % 2 === 0 && i > 0) {
        ctx.fillStyle = 'rgba(0,0,0,0.18)';
        ctx.beginPath();
        ctx.ellipse(px + s / 2, py + s / 2, s * 0.33, s * 0.22, 0, 0, Math.PI * 2);
        ctx.fill();
    }
    ctx.strokeStyle = `rgba(0,${Math.floor(50 + t * 60)},10,0.6)`;
    ctx.lineWidth = 1;
    rrect(px, py, s, s, 6);
    ctx.stroke();
}
function drawEyes(seg) {
    const x = seg.x * CELL;
    const y = seg.y * CELL;
    let eyes;
    if      (dir.dx ===  1) eyes = [{ x: x + CELL - 9, y: y + 7        }, { x: x + CELL - 9, y: y + CELL - 7  }];
    else if (dir.dx === -1) eyes = [{ x: x + 9,         y: y + 7        }, { x: x + 9,         y: y + CELL - 7  }];
    else if (dir.dy === -1) eyes = [{ x: x + 7,         y: y + 9        }, { x: x + CELL - 7,  y: y + 9         }];
    else                    eyes = [{ x: x + 7,         y: y + CELL - 9 }, { x: x + CELL - 7,  y: y + CELL - 9  }];
    for (const e of eyes) {
        ctx.beginPath(); ctx.arc(e.x, e.y, 4,   0, Math.PI * 2);
        ctx.fillStyle = '#ffffff'; ctx.fill();
        ctx.beginPath(); ctx.arc(e.x, e.y, 2.2, 0, Math.PI * 2);
        ctx.fillStyle = '#000000'; ctx.fill();
        ctx.beginPath(); ctx.arc(e.x - 1, e.y - 1, 0.9, 0, Math.PI * 2);
        ctx.fillStyle = 'rgba(255,255,255,0.7)'; ctx.fill();
    }
}
function drawPopups() {
    ctx.font = 'bold 13px "Courier New",monospace';
    ctx.textAlign = 'center';
    for (let i = popups.length - 1; i >= 0; i--) {
        const p = popups[i];
        ctx.globalAlpha = Math.min(1, p.alpha);
        ctx.fillStyle = '#ffd700';
        ctx.fillText(p.text, p.x, p.y + p.dy);
        p.dy    -= 0.6;
        p.alpha -= 0.022;
        if (p.alpha <= 0) popups.splice(i, 1);
    }
    ctx.globalAlpha = 1;
}
function drawLevelFlash() {
    if (!levelFlash) return;
    ctx.globalAlpha = Math.min(1, levelFlash.alpha) * 0.85;
    ctx.fillStyle = '#aa88ff';
    ctx.font = 'bold 28px "Courier New",monospace';
    ctx.textAlign = 'center';
    ctx.fillText(levelFlash.text, W / 2, H / 2);
    ctx.globalAlpha = 1;
    levelFlash.alpha -= 0.012;
    if (levelFlash.alpha <= 0) levelFlash = null;
}
// ── Helpers ───────────────────────────────────────────────────────────────────
function rrect(x, y, w, h, r) {
    ctx.beginPath();
    ctx.moveTo(x + r, y);
    ctx.lineTo(x + w - r, y);
    ctx.arcTo(x + w, y,     x + w, y + r,     r);
    ctx.lineTo(x + w, y + h - r);
    ctx.arcTo(x + w, y + h, x + w - r, y + h, r);
    ctx.lineTo(x + r, y + h);
    ctx.arcTo(x,     y + h, x,     y + h - r, r);
    ctx.lineTo(x,     y + r);
    ctx.arcTo(x,     y,     x + r, y,         r);
    ctx.closePath();
}
// ── Main loop ─────────────────────────────────────────────────────────────────
function loop(now) {
    step(now);
    draw(now);
    requestAnimationFrame(loop);
}
// ── Boot ──────────────────────────────────────────────────────────────────────
initState();
setState('start');
lastStep = performance.now();
requestAnimationFrame(loop);
