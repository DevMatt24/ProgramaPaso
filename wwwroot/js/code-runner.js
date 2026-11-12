// Evaluador de código C# para ProgramaPaso.
// Traduce el subconjunto de C# que se ve en el curso a JavaScript y lo ejecuta
// en un Web Worker con tiempo límite. No es un compilador real: sirve para
// comprobar métodos sencillos (variables, if, for, while, vectores, strings).
(function (root) {
    'use strict';

    // ------------------------------------------------------------------
    // 1) Enmascarar comentarios y textos para analizar solo el código
    // ------------------------------------------------------------------
    function mask(code) {
        let out = '';
        const n = code.length;
        let i = 0;
        while (i < n) {
            const c = code[i], d = code[i + 1];
            if (c === '/' && d === '/') {
                while (i < n && code[i] !== '\n') { out += ' '; i++; }
                continue;
            }
            if (c === '/' && d === '*') {
                out += '  '; i += 2;
                while (i < n && !(code[i] === '*' && code[i + 1] === '/')) { out += code[i] === '\n' ? '\n' : ' '; i++; }
                out += '  '; i += 2;
                continue;
            }
            if (c === '$' && d === '"') { out += ' '; i++; continue; }
            if (c === '"' || c === "'") {
                const q = c;
                out += q; i++;
                while (i < n && code[i] !== q && code[i] !== '\n') {
                    if (code[i] === '\\' && i + 1 < n) { out += 'x'; i++; }
                    out += 'x'; i++;
                }
                if (code[i] === q) { out += q; i++; }
                continue;
            }
            out += c; i++;
        }
        return out;
    }

    // ------------------------------------------------------------------
    // 2) Chequeo de "compilación": errores típicos de sintaxis
    // ------------------------------------------------------------------
    function check(code) {
        const issues = [];
        const add = (line, message) => issues.push({ line, message });
        const lines = mask(code).split('\n');

        // Llaves, paréntesis y corchetes balanceados
        const names = { '(': "paréntesis '('", '[': "corchete '['", '{': "llave '{'" };
        const pairs = { ')': '(', ']': '[', '}': '{' };
        const stack = [];
        let broken = false;
        for (let li = 0; li < lines.length && !broken; li++) {
            for (const ch of lines[li]) {
                if (ch === '(' || ch === '[' || ch === '{') stack.push({ ch, line: li + 1 });
                else if (ch === ')' || ch === ']' || ch === '}') {
                    const top = stack.pop();
                    if (!top || top.ch !== pairs[ch]) {
                        add(li + 1, `Sobra un '${ch}': no hay un ${names[pairs[ch]]} que lo abra.`);
                        broken = true;
                        break;
                    }
                }
            }
        }
        if (!broken && stack.length) {
            const top = stack[stack.length - 1];
            add(top.line, `Falta cerrar el ${names[top.ch]} que se abre en esta línea.`);
        }

        const trimmed = lines.map(l => l.trim());
        const idxs = [];
        trimmed.forEach((t, i) => { if (t) idxs.push(i); });

        const signature = /^(?:(?:public|private|internal|protected|static)\s+)*(?!return\b|throw\b|new\b|else\b)[\w<>\[\],]+\s+\w+\s*\([^)]*\)$/;

        for (let k = 0; k < idxs.length; k++) {
            const i = idxs[k];
            const t = trimmed[i];
            const nextT = k + 1 < idxs.length ? trimmed[idxs[k + 1]] : '';
            const prevT = k > 0 ? trimmed[idxs[k - 1]] : '';

            if (/[;{},]$/.test(t) || /}$/.test(t)) continue;
            if ((/^(if|for|foreach|while|switch)\b/.test(t) || /^else\s+if\b/.test(t)) && /\)$/.test(t)) continue;
            if (/^(else|do|try|finally)$/.test(t)) continue;
            if (/^catch\b/.test(t)) continue;
            if (/^(case\b.*|default):$/.test(t)) continue;
            if (signature.test(t)) continue;
            if (/^(&&|\|\||\.|\?|:|\+|-|\*|\/|\)|\])/.test(nextT)) continue;
            const prevContinues = /(,|&&|\|\||[=+\-*/%?(\[.])$/.test(prevT) && !/(\+\+|--)$/.test(prevT);
            if (prevContinues) continue;
            if (/=\s*(new\s+[\w\[\]]+\s*)?\{$/.test(prevT)) continue;
            add(i + 1, 'Falta el punto y coma (;) al final de esta línea.');
        }

        // Asignación (=) dentro de una condición
        trimmed.forEach((t, i) => {
            const mm = /^(?:else\s+)?(?:if|while)\s*\(/.exec(t);
            if (!mm) return;
            let depth = 0, cond = '';
            for (let p = t.indexOf('(', mm.index); p < t.length; p++) {
                const ch = t[p];
                if (ch === '(') { depth++; if (depth === 1) continue; }
                if (ch === ')') { depth--; if (depth === 0) break; }
                cond += ch;
            }
            for (let p = 0; p < cond.length; p++) {
                if (cond[p] !== '=') continue;
                if (cond[p + 1] === '=') { p++; continue; }
                if ('=!<>'.includes(cond[p - 1] || '')) continue;
                add(i + 1, 'En una condición se compara con == (con un solo = se asigna).');
                break;
            }
        });

        // Tipos que no coinciden
        trimmed.forEach((t, i) => {
            let m = /^(int|long|double|float|decimal|bool)\s+\w+\s*=\s*"/.exec(t);
            if (m) add(i + 1, `No se puede guardar un texto (entre comillas) en una variable ${m[1]}.`);
            m = /^string\s+\w+\s*=\s*-?\d/.exec(t);
            if (m) add(i + 1, 'Un texto va entre comillas: acá se guarda un número en un string.');
        });

        issues.sort((a, b) => a.line - b.line);
        return issues;
    }

    // ------------------------------------------------------------------
    // 3) Traducción de C# a JavaScript
    // ------------------------------------------------------------------
    function members(s) {
        return s
            .replace(/\.Length\b/g, '.length')
            .replace(/\.(ToLowerInvariant|ToLower)\(/g, '.toLowerCase(')
            .replace(/\.(ToUpperInvariant|ToUpper)\(/g, '.toUpperCase(')
            .replace(/\.Contains\(/g, '.includes(')
            .replace(/\.LastIndexOf\(/g, '.lastIndexOf(')
            .replace(/\.IndexOf\(/g, '.indexOf(')
            .replace(/\.Substring\(/g, '.substr(')
            .replace(/\.Split\(/g, '.split(')
            .replace(/\.StartsWith\(/g, '.startsWith(')
            .replace(/\.EndsWith\(/g, '.endsWith(')
            .replace(/\.Replace\(/g, '.replaceAll(')
            .replace(/\.Trim\(/g, '.trim(')
            .replace(/\.TrimStart\(/g, '.trimStart(')
            .replace(/\.TrimEnd\(/g, '.trimEnd(')
            .replace(/\.ToString\(/g, '.toString(');
    }

    function transpile(code) {
        const strings = [];
        let src = '';
        const n = code.length;
        let i = 0;

        // 3.1) Sacar comentarios y textos, dejando marcadores
        while (i < n) {
            const c = code[i], d = code[i + 1];
            if (c === '/' && d === '/') { while (i < n && code[i] !== '\n') i++; continue; }
            if (c === '/' && d === '*') {
                i += 2;
                while (i < n && !(code[i] === '*' && code[i + 1] === '/')) { if (code[i] === '\n') src += '\n'; i++; }
                i += 2; src += ' ';
                continue;
            }
            const interpolated = c === '$' && d === '"';
            if (c === '"' || interpolated) {
                i += interpolated ? 2 : 1;
                let body = '';
                while (i < n && code[i] !== '"') {
                    if (code[i] === '\\' && i + 1 < n) { body += code[i] + code[i + 1]; i += 2; continue; }
                    body += code[i]; i++;
                }
                i++;
                if (interpolated) {
                    const tpl = body.replace(/`/g, '\\`').replace(/\{([^{}]*)\}/g, (m, e) => '${' + members(e.split(':')[0]) + '}');
                    strings.push('`' + tpl + '`');
                } else {
                    strings.push('"' + body + '"');
                }
                src += '__S' + (strings.length - 1) + '__';
                continue;
            }
            if (c === "'") {
                i++;
                let body = '';
                while (i < n && code[i] !== "'") {
                    if (code[i] === '\\' && i + 1 < n) { body += code[i] + code[i + 1]; i += 2; continue; }
                    body += code[i]; i++;
                }
                i++;
                strings.push('"' + (body === '"' ? '\\"' : body) + '"');
                src += '__S' + (strings.length - 1) + '__';
                continue;
            }
            src += c; i++;
        }

        // 3.2) Reglas de traducción
        const def = t => (/^(int|long|double|float|decimal)$/.test(t) ? '0' : t === 'bool' ? 'false' : 'null');

        src = src
            .replace(/^\s*using\s+[\w.]+\s*;\s*$/gm, '')
            .replace(/new\s+[\w]+\s*\[\s*\]\s*\{([\s\S]*?)\}/g, '__arr([$1])')
            .replace(/=\s*\{([^{}]*)\}/g, '= __arr([$1])')
            .replace(/new\s+(int|long|double|float|decimal|bool|string|char)\s*\[([^\]]+)\]/g,
                (m, t, size) => '__arr(new Array(' + size + ').fill(' + def(t) + '))');

        // Firmas de métodos
        src = src.split('\n').map(line => {
            const m = /^(\s*)(?:(?:public|private|internal|protected)\s+)?(?:static\s+)?(?:void|int|long|double|float|decimal|bool|string|char|var)(?:\[\])?\s+(\w+)\s*\(([^)]*)\)\s*(\{?)\s*$/.exec(line);
            if (!m) return line;
            const params = m[3].split(',').map(p => p.trim()).filter(Boolean).map(p => p.split(/\s+/).pop().replace(/=.*/, ''));
            return m[1] + 'function ' + m[2] + '(' + params.join(', ') + ') ' + m[4];
        }).join('\n');

        src = src
            .replace(/foreach\s*\(\s*[\w\[\]]+\s+(\w+)\s+in\s+([^)]+)\)/g, 'for (let $1 of $2)')
            .replace(/\b(?:int|long|double|float|decimal|bool|string|char|var)(?:\[\])?\s+(?=[A-Za-z_]\w*\s*(?:=|;|,))/g, 'let ')
            .replace(/\bint\.MaxValue\b/g, '2147483647')
            .replace(/\bConsole\.WriteLine\(/g, '__out(')
            .replace(/\bConsole\.Write\(/g, '__outn(')
            .replace(/\bConsole\.ReadLine\(/g, '__read(')
            .replace(/\b(?:int|Int32|double|Double|long)\.Parse\(/g, '__parse(')
            .replace(/\bConvert\.ToInt32\(/g, '__parse(')
            .replace(/\bArray\.IndexOf\(/g, '__arrIndexOf(')
            .replace(/\bMath\.Ceiling\(/g, 'Math.ceil(')
            .replace(/\bMath\.Truncate\(/g, 'Math.trunc(')
            .replace(/\bMath\.(Max|Min|Abs|Pow|Sqrt|Round|Floor)\(/g, (m, f) => 'Math.' + f.toLowerCase() + '(');

        src = members(src);

        // 3.3) Restaurar textos
        return src.replace(/__S(\d+)__/g, (m, k) => strings[+k]);
    }

    // ------------------------------------------------------------------
    // 4) Ejecución (esta función se copia tal cual dentro del Web Worker)
    // ------------------------------------------------------------------
    function execute(js, method, tests, report) {
        let out = [];

        const rangeError = (i, len) => new Error('IndexOutOfRangeException: el índice ' + i + ' está fuera del vector (tamaño ' + len + ')');
        const __arr = function (a) {
            return new Proxy(a, {
                get(t, p, r) {
                    if (typeof p === 'string' && /^-?\d+$/.test(p)) { const k = +p; if (k < 0 || k >= t.length) throw rangeError(k, t.length); }
                    return Reflect.get(t, p, r);
                },
                set(t, p, v, r) {
                    if (typeof p === 'string' && /^-?\d+$/.test(p)) { const k = +p; if (k < 0 || k >= t.length) throw rangeError(k, t.length); }
                    return Reflect.set(t, p, v, r);
                }
            });
        };
        const __out = function (s) { out.push(String(s === undefined ? '' : s)); };
        const __outn = function (s) { out.push(String(s)); };
        const __read = function () { return null; };
        const __parse = function (v) {
            const x = Number(v);
            if (v === null || v === undefined || String(v).trim() === '' || Number.isNaN(x)) throw new Error('FormatException: el texto no es un número válido');
            return x;
        };
        const __arrIndexOf = function (a, x) { return Array.prototype.indexOf.call(a, x); };
        const string = {
            IsNullOrWhiteSpace: function (s) { return s === null || s === undefined || String(s).trim() === ''; },
            IsNullOrEmpty: function (s) { return s === null || s === undefined || s === ''; },
            Join: function (sep, a) { return Array.from(a).join(sep); },
            Empty: ''
        };

        let fn;
        try {
            fn = new Function('__arr', '__out', '__outn', '__read', '__parse', '__arrIndexOf', 'string',
                js + '\nreturn (typeof ' + method + ' === "function") ? ' + method + ' : null;')(
                __arr, __out, __outn, __read, __parse, __arrIndexOf, string);
        } catch (e) {
            for (let i = 0; i < tests.length; i++) report(i, { ok: false, got: '', error: String(e && e.message || e), output: '' });
            return;
        }
        if (typeof fn !== 'function') {
            for (let i = 0; i < tests.length; i++) report(i, { ok: false, got: '', error: 'No encontré el método ' + method + '. Revisá que se llame igual.', output: '' });
            return;
        }

        for (let i = 0; i < tests.length; i++) {
            out = [];
            const t = tests[i];
            const args = t.args.map(function (a) { return Array.isArray(a) ? __arr(JSON.parse(JSON.stringify(a))) : a; });
            let result;
            try {
                result = fn.apply(null, args);
            } catch (e) {
                report(i, { ok: false, got: '', error: String(e && e.message || e), output: out.join('\n') });
                continue;
            }
            const got = result === undefined ? '(no devuelve nada)' : JSON.stringify(result);
            const ok = result !== undefined && JSON.stringify(result) === JSON.stringify(t.expected);
            report(i, { ok: ok, got: got, error: null, output: out.join('\n') });
        }
    }

    function workerSource() {
        return 'const execute = ' + execute.toString() + ';\n' +
            'onmessage = function (e) {\n' +
            '  const d = e.data;\n' +
            '  execute(d.js, d.method, d.tests, function (i, r) { postMessage({ type: "test", i: i, r: r }); });\n' +
            '  postMessage({ type: "done" });\n' +
            '};';
    }

    // ------------------------------------------------------------------
    // 5) API pública
    // ------------------------------------------------------------------
    const TIMEOUT_MS = 2500;

    function runInWorker(js, method, tests) {
        return new Promise(function (resolve) {
            const results = new Array(tests.length).fill(null);
            let worker, url, timer, finished = false;

            const finish = function (fatal, timedOut) {
                if (finished) return;
                finished = true;
                clearTimeout(timer);
                try { worker && worker.terminate(); } catch (e) { }
                try { url && URL.revokeObjectURL(url); } catch (e) { }
                let hung = timedOut;
                const list = results.map(function (r) {
                    if (r) return r;
                    if (hung) { hung = false; return { ok: false, got: '', error: 'Se quedó ejecutando sin terminar (¿un bucle infinito?)', output: '' }; }
                    return { ok: false, got: '', error: 'No se ejecutó', output: '' };
                });
                resolve({ issues: [], tests: list, fatal: fatal });
            };
            const arm = function () { clearTimeout(timer); timer = setTimeout(function () { finish(null, true); }, TIMEOUT_MS); };

            try {
                url = URL.createObjectURL(new Blob([workerSource()], { type: 'text/javascript' }));
                worker = new Worker(url);
            } catch (e) {
                // Sin Worker: se ejecuta directo (sin protección contra bucles infinitos)
                execute(js, method, tests, function (i, r) { results[i] = r; });
                finish(null, false);
                return;
            }
            worker.onmessage = function (ev) {
                if (ev.data.type === 'test') { results[ev.data.i] = ev.data.r; arm(); }
                else if (ev.data.type === 'done') finish(null, false);
            };
            worker.onerror = function (ev) { finish('Error al ejecutar el código: ' + (ev.message || ''), false); };
            arm();
            worker.postMessage({ js: js, method: method, tests: tests });
        });
    }

    async function run(code, method, tests) {
        const issues = check(code);
        if (issues.length) return { issues: issues, tests: [], fatal: null };

        let js;
        try {
            js = transpile(code);
            new Function(js); // solo para detectar errores de forma
        } catch (e) {
            return { issues: [], tests: [], fatal: 'No pude interpretar tu código. Revisá llaves, paréntesis y punto y coma. (' + e.message + ')' };
        }
        return runInWorker(js, method, tests);
    }

    const api = { run: run, check: check, transpile: transpile, execute: execute };
    root.ppCode = api;
    if (typeof module !== 'undefined' && module.exports) module.exports = api;

    // Tab dentro del editor inserta espacios en lugar de cambiar el foco
    if (typeof document !== 'undefined') {
        document.addEventListener('keydown', function (e) {
            const t = e.target;
            if (e.key === 'Tab' && t && t.classList && t.classList.contains('code-editor')) {
                e.preventDefault();
                t.setRangeText('    ', t.selectionStart, t.selectionEnd, 'end');
                t.dispatchEvent(new Event('input', { bubbles: true }));
            }
        });
    }
})(typeof window !== 'undefined' ? window : globalThis);
