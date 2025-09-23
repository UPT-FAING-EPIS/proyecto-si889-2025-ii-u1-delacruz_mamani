// /Content/js/utils.js
// Utilidades compartidas para vistas de Proyectos (AlpineJS)

(function (w) {
    "use strict";

    // ---------------- Toast ----------------
    function toast(msg, type) {
        var el = document.getElementById("toast");
        if (!el) { alert(msg); return; }
        el.className =
            "fixed right-4 bottom-4 z-50 px-4 py-3 rounded-xl shadow text-sm " +
            (type === "warn" ? "bg-amber-500 text-white"
                : type === "error" ? "bg-red-600 text-white"
                    : "bg-emerald-600 text-white");
        el.textContent = msg;
        el.style.display = "block";
        setTimeout(function () { el.style.display = "none"; }, 1600);
    }

    // ---------------- Debounce ----------------
    function debounce(fn, ms) {
        var t;
        return function () {
            clearTimeout(t);
            var self = this, args = arguments;
            t = setTimeout(function () { fn.apply(self, args); }, ms);
        };
    }

    // ---------------- Fechas ----------------
    var meses = ["ene", "feb", "mar", "abr", "may", "jun", "jul", "ago", "sep", "oct", "nov", "dic"];

    // Normaliza cualquier input a 'YYYY-MM-DD'
    function toYMD(val) {
        if (!val) return "";
        if (typeof val === "string") {
            // /Date(1726704000000)/
            if (/^\/Date\(/.test(val)) {
                var ms = parseInt(val.replace(/\D/g, ""), 10);
                if (!isNaN(ms)) return new Date(ms).toISOString().slice(0, 10);
            }
            // ISO '2025-09-20T00:00:00' o ya 'YYYY-MM-DD'
            if (/^\d{4}-\d{2}-\d{2}/.test(val)) return val.slice(0, 10);
            var d = new Date(val);
            return isNaN(d) ? "" : d.toISOString().slice(0, 10);
        }
        var d2 = (val instanceof Date) ? val : new Date(val);
        return isNaN(d2) ? "" : d2.toISOString().slice(0, 10);
    }

    function pillRango(ini, fin) {
        ini = toYMD(ini); fin = toYMD(fin);
        if (!ini || !fin) return "";
        var i = new Date(ini + "T00:00:00");
        var f = new Date(fin + "T00:00:00");
        function dd(n) { return String(n).padStart(2, "0"); }
        return dd(i.getDate()) + " " + meses[i.getMonth()] + " - " + dd(f.getDate()) + " " + meses[f.getMonth()];
    }

    function pillFecha(ymd) {
        ymd = toYMD(ymd);
        if (!ymd) return "Seleccione fecha";
        var d = new Date(ymd + "T00:00:00");
        if (isNaN(d)) return "—";
        function dd(n) { return String(n).padStart(2, "0"); }
        return dd(d.getDate()) + " " + meses[d.getMonth()];
    }

    // ---------------- Nombres ----------------
    function initials(nombre) {
        if (!nombre) return "";
        return nombre.trim().split(/\s+/).slice(0, 2)
            .map(function (s) { return s[0].toUpperCase(); }).join("");
    }

    function shortName(n) {
        if (!n) return "";
        var p = n.trim().split(/\s+/);
        return p.length > 2 ? (p[0] + " " + (p[1][0].toUpperCase()) + ".") : n;
    }

    // Exponer
    w.utils = {
        toast: toast,
        debounce: debounce,
        toYMD: toYMD,
        pillRango: pillRango,
        pillFecha: pillFecha,
        initials: initials,
        shortName: shortName
    };
})(window);
