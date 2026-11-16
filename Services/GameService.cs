using System.Text.Json;
using Microsoft.JSInterop;
using ProgramaPaso.Domain;

namespace ProgramaPaso.Services;

// XP, rangos, racha de días y logros. Se guardan en el navegador (localStorage).
public class GameService
{
    private const string Key = "pp_stats";
    private readonly IJSRuntime _js;
    private PlayerStats? _stats;

    public GameService(IJSRuntime js) => _js = js;

    public async Task<PlayerStats> GetStatsAsync()
    {
        if (_stats is not null) return _stats;
        try
        {
            var raw = await _js.InvokeAsync<string?>("pp.get", Key);
            if (!string.IsNullOrWhiteSpace(raw)) _stats = JsonSerializer.Deserialize<PlayerStats>(raw);
        }
        catch { }
        return _stats ??= new PlayerStats();
    }

    private async Task SaveAsync()
    {
        try { await _js.InvokeVoidAsync("pp.set", Key, JsonSerializer.Serialize(_stats)); } catch { }
    }

    private async Task<string> TodayAsync()
    {
        try { return await _js.InvokeAsync<string>("pp.today"); }
        catch { return DateTime.UtcNow.ToString(Streaks.DateFormat); }
    }

    // Racha que hay que mostrar ahora y si ya practicó hoy
    public async Task<(int Days, bool PracticedToday)> GetStreakAsync()
    {
        var s = await GetStatsAsync();
        if (string.IsNullOrEmpty(s.LastActive)) return (0, false);
        var gap = Streaks.DayGap(s.LastActive, await TodayAsync());
        return (Streaks.Current(s.StreakDays, s.Freezes, gap), gap == 0);
    }

    // Se llama al terminar un quiz (o una práctica). Suma XP, actualiza la racha y desbloquea logros.
    public async Task<RunSummary> RecordRunAsync(int level, QuizResult result, int bestStreak,
        int previousBest, int passedLevels, bool practice)
    {
        var s = await GetStatsAsync();
        var summary = new RunSummary { RankBefore = Ranks.For(s.Xp) };

        // XP por respuesta: los ejercicios más difíciles valen más, y las pistas descuentan
        var xp = 0;
        foreach (var a in result.Answers)
        {
            var points = a.Question.Type switch { "code" => 40, "fill" or "order" or "bug" => 15, _ => 10 };
            xp += (int)Math.Round(points * a.Earned);
            s.Answered++;
            if (!a.IsCorrect) continue;

            s.Correct++;
            if (a.Question.Type == "bug") s.BugsFound++;
            if (a.Question.Type == "code") s.CodeSolved++;
            if (!practice && a.Question.Level != level) s.ReviewCorrect++;
        }

        if (practice)
        {
            xp = (int)Math.Round(xp * 0.5);   // la práctica da la mitad: lo importante es aprender
            s.PracticeRuns++;
        }
        else
        {
            s.Runs++;
            if (previousBest < ProgressService.PassMark && result.Score >= ProgressService.PassMark)
            {
                xp += 30;
                summary.Bonuses.Add("Nivel superado · +30 XP");
            }
            if (result.Score == 100)
            {
                xp += 20;
                summary.Bonuses.Add("Sin errores · +20 XP");
            }
        }

        // Racha de días
        var today = await TodayAsync();
        if (s.LastActive != today)
        {
            var gap = s.LastActive == "" ? int.MaxValue : Streaks.DayGap(s.LastActive, today);
            var next = Streaks.Advance(s.StreakDays, s.Freezes, gap);
            s.StreakDays = next.Days;
            s.Freezes = next.Freezes;
            summary.UsedFreeze = next.UsedFreeze;
            s.BestStreakDays = Math.Max(s.BestStreakDays, s.StreakDays);
            s.LastActive = today;
        }
        summary.StreakDays = s.StreakDays;

        s.Xp += xp;

        // Logros
        var noHints = result.Answers.Any(a => a.Question.Type == "code" && a.IsCorrect && a.HintsUsed == 0);
        var ctx = new RunContext(level, result.Score, bestStreak, previousBest, passedLevels, noHints, practice);
        foreach (var ach in Achievements.All)
        {
            if (s.Unlocked.ContainsKey(ach.Id) || !ach.IsEarned(s, ctx)) continue;
            s.Unlocked[ach.Id] = today;
            summary.NewAchievements.Add(ach);
        }

        var achievementXp = summary.NewAchievements.Count * 25;
        if (achievementXp > 0)
        {
            xp += achievementXp;
            s.Xp += achievementXp;
            summary.Bonuses.Add($"{(summary.NewAchievements.Count == 1 ? "1 logro" : $"{summary.NewAchievements.Count} logros")} · +{achievementXp} XP");
        }

        summary.XpGained = xp;
        summary.TotalXp = s.Xp;
        summary.RankAfter = Ranks.For(s.Xp);

        await SaveAsync();
        return summary;
    }
}
