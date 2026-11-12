using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.JSInterop;
using ProgramaPaso.Domain;

namespace ProgramaPaso.Services;

public class LevelRow
{
    [JsonPropertyName("user_id")] public string UserId { get; set; } = "";
    [JsonPropertyName("level")] public int Level { get; set; }
    [JsonPropertyName("best_score")] public int BestScore { get; set; }
    [JsonPropertyName("last_score")] public int LastScore { get; set; }
    [JsonPropertyName("attempts")] public int Attempts { get; set; }
    [JsonPropertyName("updated_at")] public string? UpdatedAt { get; set; }
}

public class MasteryRow
{
    [JsonPropertyName("user_id")] public string UserId { get; set; } = "";
    [JsonPropertyName("topic")] public string Topic { get; set; } = "";
    [JsonPropertyName("mastery")] public double Mastery { get; set; } = 50;
    [JsonPropertyName("seen")] public int Seen { get; set; }
    [JsonPropertyName("updated_at")] public string? UpdatedAt { get; set; }
}

// Guarda y lee el progreso del alumno:
//  - con sesión iniciada: en Supabase (tablas level_progress, topic_mastery y profiles)
//  - sin Supabase configurado: en el navegador (modo local, útil mientras desarrollás)
public class ProgressService
{
    public const int PassMark = 70;
    public const double DefaultMastery = 50;

    private readonly IJSRuntime _js;
    private readonly AuthService _auth;
    private readonly Dictionary<int, LevelRow> _levels = new();
    private readonly Dictionary<string, MasteryRow> _mastery = new();
    private double _ability = DefaultMastery;
    private bool _loaded;

    public ProgressService(IJSRuntime js, AuthService auth)
    {
        _js = js;
        _auth = auth;
    }

    private bool Cloud => _auth.IsConfigured && _auth.IsLoggedIn;

    // ---------- Lectura ----------

    // -1 = todavía no intentó el nivel
    public async Task<int> GetBestAsync(int level)
    {
        await LoadAsync();
        return _levels.TryGetValue(level, out var row) ? row.BestScore : -1;
    }

    public async Task<bool> IsUnlockedAsync(int level)
        => level <= 1 || await GetBestAsync(level - 1) >= PassMark;

    public async Task<double> GetAbilityAsync()
    {
        await LoadAsync();
        return _ability;
    }

    // tema -> dominio (0 a 100). Los temas que nunca practicó no aparecen.
    public async Task<IReadOnlyDictionary<string, double>> GetMasteryAsync()
    {
        await LoadAsync();
        return _mastery.ToDictionary(kv => kv.Key, kv => kv.Value.Mastery);
    }

    private async Task LoadAsync()
    {
        if (_loaded) return;
        _loaded = true;

        if (Cloud)
        {
            foreach (var r in await GetRowsAsync<LevelRow>($"level_progress?select=level,best_score,last_score,attempts&user_id=eq.{_auth.UserId}"))
                _levels[r.Level] = r;
            foreach (var m in await GetRowsAsync<MasteryRow>($"topic_mastery?select=topic,mastery,seen&user_id=eq.{_auth.UserId}"))
                _mastery[m.Topic] = m;
            _ability = _auth.Profile?.Ability ?? DefaultMastery;
            return;
        }

        // Modo local
        for (int n = 1; n <= Curriculum.AvailableLevels; n++)
        {
            var s = await LocalGetAsync($"pp_best_{n}");
            if (int.TryParse(s, out var best)) _levels[n] = new LevelRow { Level = n, BestScore = best, LastScore = best };
        }
        try
        {
            var raw = await LocalGetAsync("pp_mastery");
            var dict = string.IsNullOrWhiteSpace(raw) ? null : JsonSerializer.Deserialize<Dictionary<string, double>>(raw);
            if (dict is not null)
                foreach (var kv in dict) _mastery[kv.Key] = new MasteryRow { Topic = kv.Key, Mastery = kv.Value };
        }
        catch { }
        if (double.TryParse(await LocalGetAsync("pp_ability"), System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var ab)) _ability = ab;
    }

    private async Task<List<T>> GetRowsAsync<T>(string query)
    {
        try
        {
            using var res = await _auth.RestAsync(HttpMethod.Get, query);
            if (!res.IsSuccessStatusCode) return new List<T>();
            return await res.Content.ReadFromJsonAsync<List<T>>() ?? new List<T>();
        }
        catch { return new List<T>(); }
    }

    // ---------- Escritura ----------

    // Al terminar un intento: actualiza puntaje del nivel, dominio por tema y capacidad general
    public async Task RecordAsync(int level, QuizResult result)
    {
        await LoadAsync();
        var score = result.Score;
        var now = DateTime.UtcNow.ToString("o");

        // Nivel
        _levels.TryGetValue(level, out var row);
        row ??= new LevelRow { Level = level };
        row.UserId = _auth.UserId;
        row.Attempts++;
        row.LastScore = score;
        row.BestScore = Math.Max(row.BestScore, score);
        row.UpdatedAt = now;
        _levels[level] = row;

        // Dominio por tema: promedio móvil (lo reciente pesa 30 %)
        var touched = new Dictionary<string, MasteryRow>();
        foreach (var a in result.Answers)
        {
            var topic = a.Question.Topic;
            if (!_mastery.TryGetValue(topic, out var m))
                m = _mastery[topic] = new MasteryRow { Topic = topic, Mastery = DefaultMastery };

            m.UserId = _auth.UserId;
            m.Mastery = Math.Clamp(m.Mastery * 0.7 + a.Earned * 100 * 0.3, 0, 100);
            m.Seen++;
            m.UpdatedAt = now;
            touched[topic] = m;
        }

        // Capacidad general
        _ability = Math.Clamp(_ability * 0.6 + score * 0.4, 0, 100);

        if (Cloud)
        {
            await UpsertAsync("level_progress?on_conflict=user_id,level", new[] { row });
            if (touched.Count > 0) await UpsertAsync("topic_mastery?on_conflict=user_id,topic", touched.Values.ToList());
            await _auth.UpdateAbilityAsync(_ability);
        }
        else
        {
            await LocalSetAsync($"pp_best_{level}", row.BestScore.ToString());
            await LocalSetAsync("pp_mastery", JsonSerializer.Serialize(_mastery.ToDictionary(kv => kv.Key, kv => kv.Value.Mastery)));
            await LocalSetAsync("pp_ability", _ability.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
    }

    private async Task UpsertAsync<T>(string query, IEnumerable<T> rows)
    {
        try
        {
            using var res = await _auth.RestAsync(HttpMethod.Post, query, rows, "resolution=merge-duplicates,return=minimal");
            _ = res.IsSuccessStatusCode;
        }
        catch { }
    }

    // ---------- Almacenamiento local ----------

    private async Task<string?> LocalGetAsync(string key)
    {
        try { return await _js.InvokeAsync<string?>("pp.get", key); } catch { return null; }
    }

    private async Task LocalSetAsync(string key, string value)
    {
        try { await _js.InvokeVoidAsync("pp.set", key, value); } catch { }
    }
}
