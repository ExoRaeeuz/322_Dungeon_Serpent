using Microsoft.Xna.Framework.Audio;

namespace DungeonSerpent;

/// <summary>
/// Generates and plays simple synthesised sound effects at runtime.
/// No WAV/XNB files are required — all audio is built from raw PCM.
/// </summary>
public static class SoundManager
{
    private const int SampleRate = 22050;

    private static SoundEffect? _eatCoin;
    private static SoundEffect? _eatRuby;
    private static SoundEffect? _eatDiamond;
    private static SoundEffect? _eatShrinker;
    private static SoundEffect? _eatSlow;
    private static SoundEffect? _die;
    private static SoundEffect? _levelUp;
    private static SoundEffect? _move;

    public static void Initialize()
    {
        _eatCoin     = BuildTone(880f,  0.06f, Envelope.Ping);
        _eatRuby     = BuildTone(1046f, 0.09f, Envelope.Ping);
        _eatDiamond  = BuildChord(new[]{ 1046f, 1318f, 1568f }, 0.14f);
        _eatShrinker = BuildTone(330f,  0.10f, Envelope.Descend);
        _eatSlow     = BuildTone(494f,  0.12f, Envelope.Descend);
        _die         = BuildNoise(0.25f);
        _levelUp     = BuildArpeggio(new[]{ 523f, 659f, 784f, 1047f }, 0.08f);
        _move        = BuildTone(220f,  0.01f, Envelope.Ping);
    }

    public static void PlayEat(FoodType type)
    {
        switch (type)
        {
            case FoodType.Coin:     _eatCoin?.Play();     break;
            case FoodType.Ruby:     _eatRuby?.Play();     break;
            case FoodType.Diamond:  _eatDiamond?.Play();  break;
            case FoodType.Shrinker: _eatShrinker?.Play(); break;
            case FoodType.Slow:     _eatSlow?.Play();     break;
        }
    }

    public static void PlayDie()     => _die?.Play();
    public static void PlayLevelUp() => _levelUp?.Play();
    public static void PlayMove()    => _move?.Play();

    // ── PCM builders ───────────────────────────────────────────────────────

    private enum Envelope { Ping, Descend, Flat }

    private static SoundEffect BuildTone(float freq, float duration, Envelope env)
    {
        int samples = (int)(SampleRate * duration);
        var data    = new byte[samples * 2];
        for (int i = 0; i < samples; i++)
        {
            float t   = i / (float)SampleRate;
            float amp = Amp(i, samples, env);
            float s   = amp * MathF.Sin(2f * MathF.PI * freq * t);
            short v   = (short)(s * 28000);
            data[i * 2]     = (byte)(v & 0xFF);
            data[i * 2 + 1] = (byte)((v >> 8) & 0xFF);
        }
        return new SoundEffect(data, SampleRate, AudioChannels.Mono);
    }

    private static SoundEffect BuildChord(float[] freqs, float duration)
    {
        int samples = (int)(SampleRate * duration);
        var data    = new byte[samples * 2];
        for (int i = 0; i < samples; i++)
        {
            float t = i / (float)SampleRate;
            float s = 0f;
            foreach (var f in freqs)
                s += MathF.Sin(2f * MathF.PI * f * t);
            s /= freqs.Length;
            float amp = Amp(i, samples, Envelope.Ping);
            short v   = (short)(amp * s * 28000);
            data[i * 2]     = (byte)(v & 0xFF);
            data[i * 2 + 1] = (byte)((v >> 8) & 0xFF);
        }
        return new SoundEffect(data, SampleRate, AudioChannels.Mono);
    }

    private static SoundEffect BuildArpeggio(float[] freqs, float noteDuration)
    {
        int noteLen  = (int)(SampleRate * noteDuration);
        int total    = noteLen * freqs.Length;
        var data     = new byte[total * 2];
        for (int n = 0; n < freqs.Length; n++)
        {
            float freq = freqs[n];
            int offset = n * noteLen;
            for (int i = 0; i < noteLen; i++)
            {
                float t   = i / (float)SampleRate;
                float amp = Amp(i, noteLen, Envelope.Ping);
                float s   = amp * MathF.Sin(2f * MathF.PI * freq * t);
                short v   = (short)(s * 28000);
                int idx   = (offset + i) * 2;
                data[idx]     = (byte)(v & 0xFF);
                data[idx + 1] = (byte)((v >> 8) & 0xFF);
            }
        }
        return new SoundEffect(data, SampleRate, AudioChannels.Mono);
    }

    private static SoundEffect BuildNoise(float duration)
    {
        int samples = (int)(SampleRate * duration);
        var data    = new byte[samples * 2];
        var rng     = new Random(42);
        for (int i = 0; i < samples; i++)
        {
            float amp = 1f - (float)i / samples;
            short v   = (short)((rng.NextSingle() * 2f - 1f) * amp * 20000);
            data[i * 2]     = (byte)(v & 0xFF);
            data[i * 2 + 1] = (byte)((v >> 8) & 0xFF);
        }
        return new SoundEffect(data, SampleRate, AudioChannels.Mono);
    }

    private static float Amp(int i, int total, Envelope env) => env switch
    {
        Envelope.Ping    => 1f - (float)i / total,
        Envelope.Descend => MathF.Pow(1f - (float)i / total, 2f),
        _                => 1f
    };
}
