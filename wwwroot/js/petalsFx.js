// wwwroot/js/petalsFx.js
// Лепестки + ветер + спавн при тапе

window.petalsFx = (() => {
    const state = {
        petalsRoot: null,
        running: false,
        petals: new Set(),
        lastTs: 0,

        // ветер
        wind: 0,
        windTarget: 0,
        windChangeTs: 0,

        // параметры
        maxPetals: 40,
        baseSpawnPerSec: 0.9, // базовый спавн

        dustRoot: null,
        gustUntil: 0,
        lastTapTs: 0,
    };

    function rand(min, max) { return Math.random() * (max - min) + min; }
    function clamp(v, min, max) { return Math.max(min, Math.min(max, v)); }

    function makeDustDot() {
        if (!state.dustRoot) return;

        const el = document.createElement("span");
        el.className = "dust-dot";

        const vw = Math.max(document.documentElement.clientWidth, window.innerWidth || 0);

        const left = rand(0, vw);
        const d = rand(2, 4.5);
        const op = rand(0.25, 0.7);
        const t = rand(4.5, 8.5);
        const dx = rand(-18, 18);

        el.style.left = `${left}px`;
        el.style.bottom = `${rand(0, 40)}px`;
        el.style.setProperty("--d", `${d}px`);
        el.style.setProperty("--op", op.toFixed(2));
        el.style.setProperty("--t", `${t.toFixed(2)}s`);
        el.style.setProperty("--dx", `${dx.toFixed(1)}px`);

        state.dustRoot.appendChild(el);

        // удалим после пары циклов
        const lifetimeMs = Math.ceil(t * 2000);
        setTimeout(() => {
            if (el && el.parentNode) el.parentNode.removeChild(el);
        }, lifetimeMs);
    }

    function seedDust(count = 18) {
        for (let i = 0; i < count; i++) makeDustDot();
        // периодически добавляем новые
        setInterval(() => makeDustDot(), 700);
    }

    function makePetal(xPx = null, yPx = null, burst = false) {
        if (!state.petalsRoot) return;
        if (state.petals.size >= state.maxPetals) {
            // мягко удаляем самый старый
            const first = state.petals.values().next().value;
            if (first) destroyPetal(first);
        }

        const el = document.createElement("span");
        el.className = "petal";

        const vw = Math.max(document.documentElement.clientWidth, window.innerWidth || 0);
        const vh = Math.max(document.documentElement.clientHeight, window.innerHeight || 0);

        const size = rand(12, 22);
        const opacity = rand(0.55, 0.95);

        // стартовая позиция
        const startX = xPx != null ? xPx : rand(0, vw);
        const startY = yPx != null ? yPx : -rand(40, 140);

        // скорость падения
        const fallSpeed = burst ? rand(140, 240) : rand(35, 85); // px/sec
        // боковой дрейф (будет + ветер)
        const drift = burst ? rand(-30, 30) : rand(-12, 12);
        // “покачивание”
        const swayAmp = burst ? rand(8, 18) : rand(10, 26);
        const swaySpeed = rand(0.8, 1.5); // rad/sec
        // вращение
        const rot = rand(0, 360);
        const rotSpeed = rand(-70, 70); // deg/sec

        el.style.setProperty("--size", `${size}px`);
        el.style.setProperty("--op", `${opacity}`);

        state.petalsRoot.appendChild(el);

        const p = {
            el,
            x: startX,
            y: startY,
            vx: drift,
            vy: fallSpeed,
            swayAmp,
            swaySpeed,
            swayPhase: rand(0, Math.PI * 2),
            rot,
            rotSpeed,
            life: 0,
            burst,
        };

        state.petals.add(p);
        // сразу выставим позицию
        el.style.left = `${p.x}px`;
        el.style.top = `${p.y}px`;
        el.style.transform = `translate3d(0,0,0) rotate(${p.rot}deg)`;

        return p;
    }

    function destroyPetal(p) {
        if (!p) return;
        state.petals.delete(p);
        if (p.el && p.el.parentNode) p.el.parentNode.removeChild(p.el);
    }

    function updateWind(ts) {
        // если сейчас активен порыв — реже меняем цель и разрешаем больший размах
        const gusting = ts < state.gustUntil;

        // раз в 1.8–3.5 сек (или реже во время порыва) выбираем новую цель
        const minI = gusting ? 1200 : 1800;
        const maxI = gusting ? 2400 : 3500;

        if (ts > state.windChangeTs) {
            state.windChangeTs = ts + rand(minI, maxI);
            state.windTarget = rand(gusting ? -120 : -45, gusting ? 120 : 45);
        }

        // плавное стремление к цели
        const k = gusting ? 0.035 : 0.02;
        state.wind += (state.windTarget - state.wind) * k;

        // ограничим
        state.wind = clamp(state.wind, gusting ? -140 : -55, gusting ? 140 : 55);
    }

    function tick(ts) {
        if (!state.running) return;
        if (!state.lastTs) state.lastTs = ts;
        const dt = (ts - state.lastTs) / 1000;
        state.lastTs = ts;

        updateWind(ts);

        const vw = Math.max(document.documentElement.clientWidth, window.innerWidth || 0);
        const vh = Math.max(document.documentElement.clientHeight, window.innerHeight || 0);

        // базовый автоспавн
        const spawnChance = state.baseSpawnPerSec * dt;
        if (Math.random() < spawnChance) {
            makePetal(null, null, false);
        }

        // обновляем лепестки
        for (const p of Array.from(state.petals)) {
            p.life += dt;

            // вертикаль
            p.y += p.vy * dt;

            // ветер + дрейф + синус покачивания
            const sway = Math.sin(p.life * p.swaySpeed + p.swayPhase) * p.swayAmp;
            p.x += (state.wind + p.vx) * dt + sway * dt;

            // вращение
            p.rot += p.rotSpeed * dt;

            // применение
            p.el.style.left = `${p.x}px`;
            p.el.style.top = `${p.y}px`;
            p.el.style.transform = `translate3d(0,0,0) rotate(${p.rot}deg)`;

            // если улетел
            if (p.y > vh + 120 || p.x < -200 || p.x > vw + 200) {
                destroyPetal(p);
            }
        }

        requestAnimationFrame(tick);
    }

    function burstAt(clientX, clientY) {
        // лёгкий "всплеск": 2–3 лепестка
        const count = Math.random() < 0.6 ? 2 : 3;

        for (let i = 0; i < count; i++) {
            const px = clientX + rand(-14, 14);
            const py = clientY + rand(-14, 14);
            makePetal(px, py, true);
        }
    }


    function gust(strength = 80, durationMs = 900) {
        // strength: px/sec, duration: ms
        const now = performance.now();
        state.gustUntil = Math.max(state.gustUntil, now + durationMs);

        // мгновенно толкнём target — дальше updateWind сгладит
        state.windTarget = clamp(state.windTarget + (strength * (Math.random() < 0.5 ? -1 : 1)), -140, 140);
    }

    function initScrollWind() {
        let lastY = window.scrollY;
        let lastT = performance.now();

        window.addEventListener("scroll", () => {
            const y = window.scrollY;
            const t = performance.now();

            const dy = y - lastY;
            const dt = Math.max(16, t - lastT);

            // скорость скролла (приблизительно)
            const v = Math.abs(dy) / dt; // px per ms
            // конвертируем в силу порыва
            const strength = clamp(v * 1800, 0, 110); // 0..110

            if (strength > 18) {
                // направление ветра в сторону скролла: вниз -> вправо/влево случайно, но устойчиво
                const dir = dy >= 0 ? 1 : -1;
                // делаем "порыв" чуть короткий
                const sign = (Math.random() < 0.5 ? -1 : 1);
                state.windTarget = clamp(state.windTarget + sign * dir * strength, -140, 140);
                state.gustUntil = Math.max(state.gustUntil, t + 450);
            }

            lastY = y;
            lastT = t;
        }, { passive: true });
    }
    function init() {
        state.petalsRoot = document.getElementById("petals");
        if (!state.petalsRoot) return;

        state.dustRoot = document.getElementById("glowDust");
        if (state.dustRoot) seedDust(window.innerWidth < 520 ? 12 : 18);

        initScrollWind();

        // стартовое заполнение
        for (let i = 0; i < 10; i++) makePetal(null, null, false);

        // Ловим тап/клик на документе, но НЕ мешаем кнопкам
        
        const handler = (ev) => {
            const now = performance.now();
            if (now - state.lastTapTs < 120) return; // не чаще 1 раза в 120мс
            state.lastTapTs = now;
            
            const target = ev.target;
            if (!target) return;

            // если тап по интерактиву — не спавним
            if (target.closest && target.closest("button, a, input, textarea, select, label")) {
                return;
            }

            // координаты
            if (ev.touches && ev.touches.length) {
                const t = ev.touches[0];
                burstAt(t.clientX, t.clientY);
            } else {
                burstAt(ev.clientX, ev.clientY);
            }
        };

        // capture:true — получим событие, но не блокируем его
        document.addEventListener("click", handler, { passive: true, capture: true });
        document.addEventListener("touchstart", handler, { passive: true, capture: true });

        // resize: лимит по количеству лепестков
        window.addEventListener("resize", () => {
            state.maxPetals = window.innerWidth < 520 ? 28 : 40;
        });
        state.maxPetals = window.innerWidth < 520 ? 28 : 40;

        if (!state.running) {
            state.running = true;
            requestAnimationFrame(tick);
        }
    }
    return { init, gust };
})();