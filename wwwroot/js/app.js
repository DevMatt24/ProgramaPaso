// Sonidos (Web Audio, sin archivos) y almacenamiento local
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
        set(key, value) { try { localStorage.setItem(key, value); } catch { } }
    };
})();
