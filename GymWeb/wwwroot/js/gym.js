// GymPro — sidebar toggle + mobile overlay

document.addEventListener('DOMContentLoaded', function () {
    // Nav info tooltips — show on hover over the whole nav-link row
    var tip = document.getElementById('navTip');
    if (tip) {
        document.querySelectorAll('.nav-link').forEach(function (link) {
            var icon = link.querySelector('.nav-tip');
            if (!icon) return;
            link.addEventListener('mouseenter', function (e) {
                tip.textContent = icon.dataset.tip;
                tip.classList.add('visible');
                moveTooltip(e);
            });
            link.addEventListener('mousemove', moveTooltip);
            link.addEventListener('mouseleave', function () {
                tip.classList.remove('visible');
            });
        });

        // Topbar title info icon
        var topbarTip = document.querySelector('.topbar-tip');
        if (topbarTip) {
            topbarTip.addEventListener('mouseenter', function (e) {
                tip.textContent = topbarTip.dataset.tip;
                tip.classList.add('visible');
                moveTooltip(e);
            });
            topbarTip.addEventListener('mousemove', moveTooltip);
            topbarTip.addEventListener('mouseleave', function () {
                tip.classList.remove('visible');
            });
        }
        function moveTooltip(e) {
            var x = e.clientX + 18;
            var y = e.clientY - 14;
            var w = 244;
            if (x + w > window.innerWidth) x = e.clientX - w - 10;
            if (y + tip.offsetHeight + 8 > window.innerHeight) y = window.innerHeight - tip.offsetHeight - 8;
            tip.style.left = x + 'px';
            tip.style.top  = y + 'px';
        }
    }

    var sidebar = document.getElementById('sidebar');
    var mainContent = document.getElementById('mainContent');
    var toggleBtn = document.getElementById('sidebarToggle');
    var mobileBtn = document.getElementById('mobileMenuBtn');
    var overlay = document.getElementById('sidebarOverlay');

    if (!sidebar) return;

    // Restore sidebar state from localStorage
    if (localStorage.getItem('sidebarCollapsed') === 'true') {
        sidebar.classList.add('collapsed');
    }

    if (toggleBtn) {
        toggleBtn.addEventListener('click', function () {
            sidebar.classList.toggle('collapsed');
            localStorage.setItem('sidebarCollapsed', sidebar.classList.contains('collapsed'));
        });
    }

    if (mobileBtn) {
        mobileBtn.addEventListener('click', function () {
            sidebar.classList.toggle('mobile-open');
        });
    }

    if (overlay) {
        overlay.addEventListener('click', function () {
            sidebar.classList.remove('mobile-open');
        });
    }

    // Auto-dismiss alerts
    setTimeout(function () {
        document.querySelectorAll('.alert-dismissible').forEach(function (el) {
            el.style.transition = 'opacity 0.4s';
            el.style.opacity = '0';
            setTimeout(function () { el.remove(); }, 400);
        });
    }, 4000);

    // Live search filter for tables
    var searchInput = document.getElementById('tableSearch');
    if (searchInput) {
        searchInput.addEventListener('input', function () {
            var q = this.value.toLowerCase();
            document.querySelectorAll('.gym-table tbody tr').forEach(function (row) {
                row.style.display = row.textContent.toLowerCase().includes(q) ? '' : 'none';
            });
        });
    }

    // ── Theme Palette Selector ──
    var paletteBtn = document.getElementById('themePaletteBtn');
    var paletteDropdown = document.getElementById('themePaletteDropdown');
    var currentTheme = localStorage.getItem('gym_theme') || 'cyan';

    function syncActiveThemeButtons(theme) {
        document.querySelectorAll('.theme-color-opt').forEach(function (btn) {
            btn.classList.toggle('active', btn.getAttribute('data-color') === theme);
        });
    }

    syncActiveThemeButtons(currentTheme);

    if (paletteBtn && paletteDropdown) {
        paletteBtn.addEventListener('click', function (e) {
            e.stopPropagation();
            paletteDropdown.classList.toggle('show');
        });

        document.addEventListener('click', function (e) {
            if (!paletteDropdown.contains(e.target) && e.target !== paletteBtn) {
                paletteDropdown.classList.remove('show');
            }
        });
    }

    // Color buttons click (works in Topbar and in Settings view)
    document.addEventListener('click', function (e) {
        var opt = e.target.closest('.theme-color-opt');
        if (!opt) return;

        var color = opt.getAttribute('data-color');
        if (!color) return;

        document.documentElement.setAttribute('data-theme', color);
        localStorage.setItem('gym_theme', color);
        syncActiveThemeButtons(color);

        // Micro-animación de confirmación
        opt.style.transform = 'scale(0.85)';
        setTimeout(function() {
            opt.style.transform = '';
        }, 150);
    });
});

