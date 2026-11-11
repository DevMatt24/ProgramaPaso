using Microsoft.JSInterop;

namespace ProgramaPaso.Services;

// Guarda el mejor puntaje por nivel en el navegador (localStorage).
// Más adelante se reemplaza por Supabase sin tocar las páginas.
public class ProgressService
{
    public const int PassMark = 70;
    private readonly IJSRuntime _js;
    public ProgressService(IJSRuntime js) => _js = js;

    // -1 = todavía no intentó el nivel
    public async Task<int> GetBestAsync(int level)
    {
        try
        {
            var s = await _js.InvokeAsync<string?>("pp.get", $"pp_best_{level}");
            return int.TryParse(s, out var v) ? v : -1;
        }
        catch { return -1; }
    }

    public async Task SaveBestAsync(int level, int score)
    {
        try
        {
            if (score > await GetBestAsync(level))
                await _js.InvokeVoidAsync("pp.set", $"pp_best_{level}", score.ToString());
        }
        catch { }
    }

    public async Task<bool> IsUnlockedAsync(int level)
        => level <= 1 || await GetBestAsync(level - 1) >= PassMark;
}
