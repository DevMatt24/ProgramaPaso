using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.JSInterop;

namespace ProgramaPaso.Services;

public class SupabaseOptions
{
    public string Url { get; set; } = "";
    public string AnonKey { get; set; } = "";
    public bool IsConfigured => !string.IsNullOrWhiteSpace(Url) && !string.IsNullOrWhiteSpace(AnonKey);
}

public class UserProfile
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("ability")] public double Ability { get; set; } = 50;
}

internal class StoredSession
{
    [JsonPropertyName("access_token")] public string AccessToken { get; set; } = "";
    [JsonPropertyName("refresh_token")] public string RefreshToken { get; set; } = "";
    [JsonPropertyName("expires_at")] public long ExpiresAt { get; set; }
}

// Login con Google a través de Supabase Auth, usando su API REST directamente.
// La sesión se guarda en localStorage (la deja ahí wwwroot/js/app.js al volver de Google).
public class AuthService
{
    private const string SessionKey = "pp_session";

    private readonly HttpClient _http;
    private readonly IJSRuntime _js;
    private readonly SupabaseOptions _opt;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private Task? _init;
    private StoredSession? _session;

    public AuthService(HttpClient http, IJSRuntime js, SupabaseOptions opt)
    {
        _http = http;
        _js = js;
        _opt = opt;
    }

    // Interruptor del login: false = la app funciona sin cuentas (progreso guardado en el navegador).
    // Para activar las cuentas más adelante, cambialo a true y completá wwwroot/appsettings.json.
    public const bool LoginEnabled = false;

    public bool IsConfigured => LoginEnabled && _opt.IsConfigured;

    // Para diagnóstico: muestra qué leyó de appsettings.json, sin exponer la clave
    public string ConfigSummary
    {
        get
        {
            var url = string.IsNullOrWhiteSpace(_opt.Url) ? "vacía" : _opt.Url;
            var key = string.IsNullOrWhiteSpace(_opt.AnonKey) ? "vacía" : $"{_opt.AnonKey.Length} caracteres";
            return $"Url: {url} · Clave: {key}";
        }
    }
    public bool IsLoggedIn => _session is not null && UserId != "";
    public string UserId { get; private set; } = "";
    public string Email { get; private set; } = "";
    public string GoogleName { get; private set; } = "";
    public UserProfile? Profile { get; private set; }

    public bool NeedsOnboarding => IsLoggedIn && Profile is null;
    public string DisplayName => !string.IsNullOrWhiteSpace(Profile?.Name) ? Profile!.Name : GoogleName;

    public Task InitAsync() => _init ??= LoadAsync();

    public string GoogleLoginUrl(string redirectTo)
        => $"{_opt.Url}/auth/v1/authorize?provider=google&redirect_to={Uri.EscapeDataString(redirectTo)}";

    // ---------- Inicio de sesión ----------

    private async Task LoadAsync()
    {
        if (!IsConfigured) return;

        try
        {
            var raw = await _js.InvokeAsync<string?>("pp.get", SessionKey);
            if (string.IsNullOrWhiteSpace(raw)) return;
            _session = JsonSerializer.Deserialize<StoredSession>(raw);
        }
        catch { _session = null; }
        if (_session is null) return;

        var token = await GetTokenAsync();
        if (token is null || !await LoadUserAsync(token))
        {
            await ClearAsync();
            return;
        }

        Profile = await LoadProfileAsync();
    }

    private async Task<bool> LoadUserAsync(string token)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, $"{_opt.Url}/auth/v1/user");
            req.Headers.Add("apikey", _opt.AnonKey);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var res = await _http.SendAsync(req);
            if (!res.IsSuccessStatusCode) return false;

            using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
            var root = doc.RootElement;
            UserId = root.TryGetProperty("id", out var id) ? id.GetString() ?? "" : "";
            Email = root.TryGetProperty("email", out var mail) ? mail.GetString() ?? "" : "";

            GoogleName = "";
            if (root.TryGetProperty("user_metadata", out var meta) && meta.ValueKind == JsonValueKind.Object)
            {
                if (meta.TryGetProperty("full_name", out var full)) GoogleName = full.GetString() ?? "";
                if (string.IsNullOrWhiteSpace(GoogleName) && meta.TryGetProperty("name", out var nm)) GoogleName = nm.GetString() ?? "";
            }
            return UserId != "";
        }
        catch { return false; }
    }

    // ---------- Token (se renueva solo cuando está por vencer) ----------

    public async Task<string?> GetTokenAsync()
    {
        if (_session is null) return null;
        if (_session.ExpiresAt - DateTimeOffset.UtcNow.ToUnixTimeSeconds() > 60) return _session.AccessToken;
        return await RefreshAsync() ? _session?.AccessToken : null;
    }

    private async Task<bool> RefreshAsync()
    {
        await _refreshLock.WaitAsync();
        try
        {
            if (_session is null) return false;
            // Otro llamado pudo haberlo renovado mientras esperábamos
            if (_session.ExpiresAt - DateTimeOffset.UtcNow.ToUnixTimeSeconds() > 60) return true;

            using var req = new HttpRequestMessage(HttpMethod.Post, $"{_opt.Url}/auth/v1/token?grant_type=refresh_token");
            req.Headers.Add("apikey", _opt.AnonKey);
            req.Content = JsonContent.Create(new Dictionary<string, string> { ["refresh_token"] = _session.RefreshToken });
            using var res = await _http.SendAsync(req);
            if (!res.IsSuccessStatusCode) return false;

            using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
            var root = doc.RootElement;
            var expiresIn = root.TryGetProperty("expires_in", out var e) ? e.GetInt64() : 3600;

            _session = new StoredSession
            {
                AccessToken = root.GetProperty("access_token").GetString() ?? "",
                RefreshToken = root.GetProperty("refresh_token").GetString() ?? "",
                ExpiresAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + expiresIn
            };
            await _js.InvokeVoidAsync("pp.set", SessionKey, JsonSerializer.Serialize(_session));
            return true;
        }
        catch { return false; }
        finally { _refreshLock.Release(); }
    }

    // ---------- Perfil ----------

    private async Task<UserProfile?> LoadProfileAsync()
    {
        try
        {
            using var res = await RestAsync(HttpMethod.Get, $"profiles?id=eq.{UserId}&select=id,name,ability");
            if (!res.IsSuccessStatusCode) return null;
            var rows = await res.Content.ReadFromJsonAsync<List<UserProfile>>();
            return rows?.FirstOrDefault();
        }
        catch { return null; }
    }

    // Registro: guarda el nombre elegido (crea el perfil la primera vez)
    public async Task<bool> SaveNameAsync(string name)
    {
        name = name.Trim();
        if (!IsLoggedIn || name.Length < 2 || name.Length > 40) return false;
        try
        {
            var row = new UserProfile { Id = UserId, Name = name, Ability = Profile?.Ability ?? 50 };
            using var res = await RestAsync(HttpMethod.Post, "profiles?on_conflict=id", new[] { row },
                "resolution=merge-duplicates,return=minimal");
            if (!res.IsSuccessStatusCode) return false;
            Profile = row;
            return true;
        }
        catch { return false; }
    }

    public async Task UpdateAbilityAsync(double ability)
    {
        if (!IsLoggedIn || Profile is null) return;
        ability = Math.Clamp(ability, 0, 100);
        try
        {
            using var res = await RestAsync(HttpMethod.Patch, $"profiles?id=eq.{UserId}",
                new Dictionary<string, object> { ["ability"] = ability, ["updated_at"] = DateTime.UtcNow.ToString("o") },
                "return=minimal");
            if (res.IsSuccessStatusCode) Profile.Ability = ability;
        }
        catch { }
    }

    // ---------- Llamadas a la base de datos (PostgREST) con la sesión del usuario ----------

    public async Task<HttpResponseMessage> RestAsync(HttpMethod method, string pathAndQuery, object? body = null, string? prefer = null)
    {
        var token = await GetTokenAsync();
        var req = new HttpRequestMessage(method, $"{_opt.Url}/rest/v1/{pathAndQuery}");
        req.Headers.Add("apikey", _opt.AnonKey);
        if (token is not null) req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (prefer is not null) req.Headers.Add("Prefer", prefer);
        if (body is not null) req.Content = JsonContent.Create(body);
        return await _http.SendAsync(req);
    }

    // ---------- Cerrar sesión ----------

    public async Task SignOutAsync()
    {
        try
        {
            var token = _session?.AccessToken;
            if (token is not null)
            {
                using var req = new HttpRequestMessage(HttpMethod.Post, $"{_opt.Url}/auth/v1/logout");
                req.Headers.Add("apikey", _opt.AnonKey);
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                using var logoutRes = await _http.SendAsync(req);
            }
        }
        catch { }
        await ClearAsync();
    }

    private async Task ClearAsync()
    {
        _session = null;
        UserId = Email = GoogleName = "";
        Profile = null;
        try { await _js.InvokeVoidAsync("pp.remove", SessionKey); } catch { }
    }
}
