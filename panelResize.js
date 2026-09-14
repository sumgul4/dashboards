// wwwroot/js/panelResize.js
// Önizleme sütununu sola sürükleyerek genişletir; giriş sütunu (kriter + bulgu taslağı) daralır.
// Genişlik .noc-cols üzerindeki --preview-w değişkenine yazılır.
// Blazor: her render sonrası auditAgent.initResize() çağrılır — idempotenttir.

window.auditAgent = window.auditAgent || {};

(function () {
    var KEY = 'teftis.previewWidth';
    var DEFAULT_W = 544;
    var MIN_W = 340;
    var INPUT_MIN = 320;
    var HANDLE_W = 24;

    var cols = null, handle = null, badge = null, panel = null;
    var startX = 0, startW = 0;

    function maxWidth() {
        return Math.max(MIN_W, cols.clientWidth - INPUT_MIN - HANDLE_W);
    }

    function setWidth(px) {
        if (!cols) return;
        var w = Math.min(maxWidth(), Math.max(MIN_W, Math.round(px)));
        cols.style.setProperty('--preview-w', w + 'px');
        if (badge) badge.textContent = w + ' px';
        try { localStorage.setItem(KEY, w); } catch (e) { }
        return w;
    }

    function current() {
        return panel ? panel.getBoundingClientRect().width : DEFAULT_W;
    }

    function onMove(e) { setWidth(startW + (startX - e.clientX)); }

    function onUp() {
        window.removeEventListener('pointermove', onMove);
        window.removeEventListener('pointerup', onUp);
        if (cols) cols.classList.remove('is-resizing');
    }

    function onDown(e) {
        if (e.button !== 0) return;
        e.preventDefault();
        startX = e.clientX;
        startW = current();
        cols.classList.add('is-resizing');
        window.addEventListener('pointermove', onMove);
        window.addEventListener('pointerup', onUp);
    }

    // 14.09 vers1 silinecek — başlangıç (eski sabit 24px adım)
    // function onKey(e) {
    //     if (e.key === 'ArrowLeft') { setWidth(current() + 24); e.preventDefault(); }
    //     else if (e.key === 'ArrowRight') { setWidth(current() - 24); e.preventDefault(); }
    // }
    // 14.09 vers1 silinecek — bitiş

    // 14.09 vers2 — başlangıç: ←/→ 16px, Shift ile 48px, Home varsayılana döner
    function onKey(e) {
        var step = e.shiftKey ? 48 : 16;
        if (e.key === 'ArrowLeft') { setWidth(current() + step); e.preventDefault(); }
        else if (e.key === 'ArrowRight') { setWidth(current() - step); e.preventDefault(); }
        else if (e.key === 'Home') { setWidth(DEFAULT_W); e.preventDefault(); }
    }
    // 14.09 vers2 — bitiş

    function onDbl() { setWidth(DEFAULT_W); }

    function bind() {
        cols = document.querySelector('.noc-cols');
        if (!cols) return false;

        handle = cols.querySelector('.noc-resizer');
        badge = cols.querySelector('.noc-width-badge');
        panel = cols.querySelector('.noc-col-preview, .noc-col-table');
        if (!handle) return false;

        // kaydedilmiş genişliği her render'da uygula (sekme değişimini de kapsar)
        var saved = DEFAULT_W;
        try { saved = parseInt(localStorage.getItem(KEY), 10) || DEFAULT_W; } catch (e) { }
        setWidth(saved);

        // aynı tutamaca ikinci kez listener bağlamayı önle
        if (handle.getAttribute('data-bound') === '1') return true;
        handle.setAttribute('data-bound', '1');

        handle.addEventListener('pointerdown', onDown);
        handle.addEventListener('keydown', onKey);
        handle.addEventListener('dblclick', onDbl);
        return true;
    }

    window.addEventListener('resize', function () { if (cols) setWidth(current()); });

    // Blazor'dan çağrılan giriş noktası — DOM hazır değilse bir sonraki frame'de tekrar dener.
    window.auditAgent.initResize = function () {
        if (!bind()) requestAnimationFrame(bind);
    };

    if (document.readyState !== 'loading') window.auditAgent.initResize();
    else document.addEventListener('DOMContentLoaded', window.auditAgent.initResize);
})();
