using System.Net.Http.Json;
using ProgramaPaso.Domain;

namespace ProgramaPaso.Services;

public class QuestionBank
{
    private readonly HttpClient _http;
    private readonly Dictionary<int, List<Question>> _cache = new();

    public QuestionBank(HttpClient http) => _http = http;

    public async Task<List<Question>> GetLevelAsync(int level)
    {
        if (_cache.TryGetValue(level, out var cached)) return cached;

        var questions = await _http.GetFromJsonAsync<List<Question>>($"questions/nivel{level}.json")
                        ?? new List<Question>();
        _cache[level] = questions;
        return questions;
    }
}