using System.Globalization;

namespace ProgramaPaso.Domain;

// ---------- Rangos ----------
public record Rank(string Name, int MinXp, string Icon);

public static class Ranks
{
    public static readonly Rank[] All =
    {
        new("Novato", 0, "🌱"),
        new("Aprendiz", 120, "📘"),
        new("Programador", 320, "💻"),
        new("Depurador", 650, "🐞"),
        new("Arquitecto", 1100, "🏗️"),
        new("Maestro C#", 1800, "👑"),
    };

    public static Rank For(int xp) => All.Last(r => xp >= r.MinXp);
    public static Rank? Next(int xp) => All.FirstOrDefault(r => r.MinXp > xp);

    // 0 a 100: cuánto falta para el próximo rango
    public static int Progress(int xp)
    {
        var current = For(xp);
        var next = Next(xp);
        if (next is null) return 100;
        return (int)Math.Round((xp - current.MinXp) * 100.0 / (next.MinXp - current.MinXp));
    }
}

// ---------- Racha de días (con un día de gracia que se gana cada 7 días) ----------
public static class Streaks
{
    public const string DateFormat = "yyyy-MM-dd";

    // Días entre la última práctica y hoy. int.MaxValue si nunca practicó.
    public static int DayGap(string last, string today)
    {
        if (!DateOnly.TryParseExact(last, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var l) ||
            !DateOnly.TryParseExact(today, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var t))
            return int.MaxValue;
        return t.DayNumber - l.DayNumber;
    }

    public static (int Days, bool UsedFreeze, int Freezes) Advance(int days, int freezes, int gap)
    {
        if (gap <= 0) return (days, false, freezes);                       // hoy ya había practicado
        if (gap == 1) return Finish(days + 1, false, freezes);             // practicó ayer
        if (gap == 2 && freezes > 0) return Finish(days + 1, true, freezes - 1);   // faltó un día: usa la gracia
        return Finish(1, false, freezes);                                  // se cortó: arranca de nuevo
    }

    private static (int, bool, int) Finish(int days, bool used, int freezes)
        => (days, used, days % 7 == 0 ? Math.Min(1, freezes + 1) : freezes);

    // Racha que hay que mostrar hoy (0 si ya se cortó)
    public static int Current(int days, int freezes, int gap)
        => gap <= 1 || (gap == 2 && freezes > 0) ? days : 0;
}

// ---------- Estadísticas del jugador (se guardan en el navegador) ----------
public class PlayerStats
{
    public int Xp { get; set; }
    public int StreakDays { get; set; }
    public int BestStreakDays { get; set; }
    public string LastActive { get; set; } = "";
    public int Freezes { get; set; }

    public int Runs { get; set; }
    public int PracticeRuns { get; set; }
    public int Answered { get; set; }
    public int Correct { get; set; }
    public int BugsFound { get; set; }
    public int CodeSolved { get; set; }
    public int ReviewCorrect { get; set; }

    // logro -> fecha en que se desbloqueó
    public Dictionary<string, string> Unlocked { get; set; } = new();
}

// ---------- Logros ----------
public record RunContext(int Level, int Score, int BestStreak, int PreviousBest, int PassedLevels, bool CodeNoHints, bool IsPractice);

public record Achievement(string Id, string Name, string Description, string Icon, Func<PlayerStats, RunContext, bool> IsEarned);

public static class Achievements
{
    public static readonly Achievement[] All =
    {
        new("first-run", "Hola, mundo", "Terminá tu primer quiz.", "👋", (s, c) => s.Runs >= 1),
        new("perfect", "Impecable", "Sacá 100 en un nivel.", "💯", (s, c) => !c.IsPractice && c.Score == 100),
        new("streak5", "En llamas", "Acertá 5 seguidas en un mismo quiz.", "🔥", (s, c) => !c.IsPractice && c.BestStreak >= 5),
        new("comeback", "Remontada", "Aprobá un nivel que antes no habías aprobado.", "🚀", (s, c) => !c.IsPractice && c.PreviousBest >= 0 && c.PreviousBest < 70 && c.Score >= 70),
        new("practice", "Aprender del error", "Completá una práctica con lo que fallaste.", "🔁", (s, c) => s.PracticeRuns >= 1),
        new("memory", "Memoria de elefante", "Acertá 5 preguntas de repaso de niveles anteriores.", "🐘", (s, c) => s.ReviewCorrect >= 5),
        new("bug-hunter", "Cazabugs", "Encontrá los errores en 3 ejercicios de depurar.", "🐞", (s, c) => s.BugsFound >= 3),
        new("code-first", "Primer reto", "Resolvé tu primer reto de código.", "⌨️", (s, c) => s.CodeSolved >= 1),
        new("no-hints", "Sin pistas", "Resolvé un reto de código sin pedir ninguna pista.", "🧠", (s, c) => !c.IsPractice && c.CodeNoHints),
        new("days3", "Constancia", "Practicá 3 días seguidos.", "📅", (s, c) => s.StreakDays >= 3),
        new("days7", "Semana completa", "Practicá 7 días seguidos.", "🗓️", (s, c) => s.StreakDays >= 7),
        new("answers50", "Cincuenta", "Llegá a 50 respuestas correctas.", "✅", (s, c) => s.Correct >= 50),
        new("answers200", "Doscientas", "Llegá a 200 respuestas correctas.", "🎯", (s, c) => s.Correct >= 200),
        new("rank3", "Subí de rango", "Llegá al rango Programador.", "💻", (s, c) => Ranks.For(s.Xp).MinXp >= 320),
        new("half-way", "A mitad de camino", "Aprobá 5 niveles.", "🧭", (s, c) => !c.IsPractice && c.PassedLevels >= 5),
        new("all-levels", "Camino completo", "Aprobá todos los niveles.", "🏆", (s, c) => !c.IsPractice && c.PassedLevels >= Curriculum.AvailableLevels),
    };
}

// ---------- Resultado de un intento ----------
public class RunSummary
{
    public int XpGained { get; set; }
    public int TotalXp { get; set; }
    public List<string> Bonuses { get; set; } = new();
    public Rank RankBefore { get; set; } = Ranks.All[0];
    public Rank RankAfter { get; set; } = Ranks.All[0];
    public bool LeveledUp => RankAfter != RankBefore;
    public int StreakDays { get; set; }
    public bool UsedFreeze { get; set; }
    public List<Achievement> NewAchievements { get; set; } = new();
}
