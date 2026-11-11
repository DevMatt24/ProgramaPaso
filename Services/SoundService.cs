using Microsoft.JSInterop;

namespace ProgramaPaso.Services;

public class SoundService
{
    private readonly IJSRuntime _js;
    public SoundService(IJSRuntime js) => _js = js;

    public async Task PlayAsync(string name)
    {
        try { await _js.InvokeVoidAsync("pp.play", name); } catch { }
    }

    public async Task<bool> IsMutedAsync()
    {
        try { return await _js.InvokeAsync<bool>("pp.isMuted"); } catch { return false; }
    }

    public async Task SetMutedAsync(bool muted)
    {
        try { await _js.InvokeVoidAsync("pp.setMuted", muted); } catch { }
    }
}
