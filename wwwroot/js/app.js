// Sonidos (Web Audio, sin archivos), almacenamiento local y retorno del login con Google
window.pp = (() => {
    let ctx = null;
    const muted = () => localStorage.getItem('pp_muted') === '1';
    const ac = () => {
        ctx = ctx || new (window.AudioContext || window.webkitAudioContext)();
        if (ctx.state === 'suspended') ctx.resume();
        return ctx;
    };
    const tone = (freq, start, dur, type = 'sine', vol = 0.12) => {
        const c = ac(), o = c.createOscillator(), g = c.createGain(), t = c.currentTime + start;
        o.type = type;
        o.frequency.setValueAtTime(freq, t);
        g.gain.setValueAtTime(0.0001, t);
        g.gain.exponentialRampToValueAtTime(vol, t + 0.02);
        g.gain.exponentialRampToValueAtTime(0.0001, t + dur);
        o.connect(g);
        g.connect(c.destination);
        o.start(t);
        o.stop(t + dur + 0.05);
    };
    const sounds = {
        tap:    () => tone(600, 0, 0.05, 'sine', 0.06),
        right:  () => { tone(659.25, 0, 0.12); tone(880, 0.09, 0.22); },
        streak: () => [523.25, 659.25, 783.99, 1046.5].forEach((f, i) => tone(f, i * 0.07, 0.2, 'triangle')),
        wrong:  () => { tone(220, 0, 0.18, 'sawtooth', 0.05); tone(165, 0.12, 0.3, 'sawtooth', 0.05); },
        win:    () => [523.25, 659.25, 783.99, 1046.5, 783.99, 1046.5].forEach((f, i) => tone(f, i * 0.12, 0.28, 'triangle', 0.13)),
        lose:   () => [392, 349.23, 329.63].forEach((f, i) => tone(f, i * 0.16, 0.32, 'sine', 0.1))
    };
    return {
        play(name) { try { if (!muted() && sounds[name]) sounds[name](); } catch { } },
        isMuted() { try { return muted(); } catch { return false; } },
        setMuted(m) { try { localStorage.setItem('pp_muted', m ? '1' : '0'); } catch { } },
        get(key) { try { return localStorage.getItem(key); } catch { return null; } },
        set(key, value) { try { localStorage.setItem(key, value); } catch { } },
        remove(key) { try { localStorage.removeItem(key); } catch { } },
        // Tema claro/oscuro: lo guardado por el usuario, o si no, el del sistema
        effectiveTheme() {
            try {
                const saved = localStorage.getItem('pp_theme');
                if (saved === 'dark' || saved === 'light') return saved;
                return window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';
            } catch { return 'light'; }
        },
        setTheme(theme) {
            try {
                if (theme !== 'dark' && theme !== 'light') return;
                localStorage.setItem('pp_theme', theme);
                document.documentElement.setAttribute('data-theme', theme);
            } catch { }
        },
        // Mensaje de error del login con Google (se lee una sola vez)
        takeAuthError() {
            try {
                const e = sessionStorage.getItem('pp_auth_error');
                sessionStorage.removeItem('pp_auth_error');
                return e;
            } catch { return null; }
        }
    };
})();

// Aplica el tema elegido antes de que arranque Blazor
(function applySavedTheme() {
    try {
        const t = localStorage.getItem('pp_theme');
        if (t === 'dark' || t === 'light') document.documentElement.setAttribute('data-theme', t);
    } catch { }
})();

// Login con Google (Supabase): al volver de Google, los tokens llegan en el
// fragmento de la URL (#access_token=...). Se guardan y se limpia la dirección.
// Se ejecuta apenas carga este archivo, antes de que arranque Blazor.
(function captureAuthRedirect() {
    try {
        const clean = () => history.replaceState(null, '', location.pathname);
        const hash = new URLSearchParams((location.hash || '').replace(/^#/, ''));
        const query = new URLSearchParams(location.search || '');

        if (hash.get('access_token') && hash.get('refresh_token')) {
            const expiresIn = parseInt(hash.get('expires_in') || '3600', 10);
            const expiresAt = parseInt(hash.get('expires_at') || '0', 10) || Math.floor(Date.now() / 1000) + expiresIn;
            localStorage.setItem('pp_session', JSON.stringify({
                access_token: hash.get('access_token'),
                refresh_token: hash.get('refresh_token'),
                expires_at: expiresAt
            }));
            clean();
            return;
        }

        const err = hash.get('error_description') || hash.get('error') || query.get('error_description');
        if (err) {
            sessionStorage.setItem('pp_auth_error', err.replace(/\+/g, ' '));
            clean();
        }
    } catch { }
})();
