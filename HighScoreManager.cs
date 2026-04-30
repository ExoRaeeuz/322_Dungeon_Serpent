namespace DungeonSerpent;

/// <summary>
/// Persists the all-time high score to a plain-text file next to the executable.
/// </summary>
public static class HighScoreManager
{
    private static readonly string _path = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory, C.HiScoreFile);

    public static int Load()
    {
        try
        {
            if (File.Exists(_path) && int.TryParse(File.ReadAllText(_path).Trim(), out int v))
                return v;
        }
        catch { /* ignore IO errors */ }
        return 0;
    }

    public static void Save(int score)
    {
        try { File.WriteAllText(_path, score.ToString()); }
        catch { /* ignore IO errors */ }
    }
}
