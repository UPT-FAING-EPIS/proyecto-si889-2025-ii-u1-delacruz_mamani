// /Content/js/proyectosdetalle.js
// Lógica de la vista Detalle de Proyecto (AlpineJS)

(function (w) {
    "use strict";

    // Rutas desde la vista (inyectadas en window.ProyectosCfg)
    var U = (w.ProyectosCfg && w.ProyectosCfg.urls) || {};
    // Fallback seguro para el id del proyecto (por si no llega al crear tareas)
    var PID = Number((w.ProyectosCfg && w.ProyectosCfg.idProyecto) || 0);

    // Helper seguro para parsear JSON
    async function asJson(res) {
        if (!res.ok) throw new Error("HTTP " + res.status);
        return res.json();
    }

    // --- Utilidad global para dropdown "Estado" que se usa con onclick en Razor ---
    w.cambiarEstado = async function (id, estado) {
        try {
            const r = await fetch(U.CambiarEstado, {
                method: "POST",
                headers: { "Content-Type": "application/x-www-form-urlencoded" },
                body: new URLSearchParams({ id: id, estado: estado })
            });
            const d = await asJson(r);
            if (d.ok) { utils.toast("Estado actualizado"); location.reload(); }
            else utils.toast("No se pudo cambiar", "error");
        } catch (e) { utils.toast("Error de red", "error"); }
    };

    // ----------------- Helpers de color (para tablero) -----------------
    function hexToRgb(hex) {
        hex = (hex || "").replace("#", "");
        if (hex.length === 3) hex = hex.split("").map(function (c) { return c + c; }).join("");
        var bigint = parseInt(hex, 16);
        if (isNaN(bigint)) return { r: 59, g: 130, b: 246 }; // azul por defecto
        return { r: (bigint >> 16) & 255, g: (bigint >> 8) & 255, b: bigint & 255 };
    }
    function rgba(hex, a) {
        var c = hexToRgb(hex);
        return "rgba(" + c.r + "," + c.g + "," + c.b + "," + (a == null ? 1 : a) + ")";
    }

    // Componente principal (Alpine)
    w.detalleProyecto = function (idProyecto, fechaIniYmd, fechaFinYmd) {
        return {
            // Tabs
            tab: "tablero",

            // Base proyecto
            idProyecto: Number(idProyecto) || PID,
            metodologia: "",
            columnas: [],

            // 🎨 Paleta de colores para columnas (fallback; si el backend manda col.ColorHex, se usa esa)
            paletaCols: ['#3b82f6', '#22c55e', '#f59e0b', '#ef4444', '#14b8a6', '#8b5cf6', '#0ea5e9', '#a3e635'],

            colorColumna(iOrCol) {
                if (typeof iOrCol === 'object' && iOrCol && iOrCol.ColorHex) return iOrCol.ColorHex;
                let idx;
                if (typeof iOrCol === 'number') idx = iOrCol;
                else if (iOrCol && iOrCol.IdColumna) idx = this.columnas.findIndex(c => c.IdColumna === iOrCol.IdColumna);
                else idx = 0;
                if (idx < 0) idx = 0;
                return this.paletaCols[idx % this.paletaCols.length];
            },
            stylesDeColumna(col) {
                const base = this.colorColumna(col);
                return { borderColor: base, backgroundColor: rgba(base, 0.06) };
            },
            aplicarColoresATareas() {
                (this.columnas || []).forEach((col, idx) => {
                    const color = this.colorColumna(col.ColorHex ? col : idx);
                    (col.tareas || []).forEach(t => {
                        t._colColor = color;
                        t._colBg = rgba(color, 0.06);
                    });
                });
            },

            // Fechas del proyecto
            fechaInicio: fechaIniYmd || "",
            fechaFin: fechaFinYmd || "",
            msgFecha: "",

            // Miembros
            miembrosProyecto: [],
            qUsuario: "",
            usuariosEncontrados: [],
            openInvite: false,
            correo: "",

            // DataTable (Miembros)
            qMiembro: "",
            sortKey: "Nombres",
            sortDir: "asc",
            page: 1,
            pageSize: 10,

            // Tareas simples (tab "Tareas")
            tareas: [],
            nuevaTitulo: "",
            nuevaAsignado: "",
            nuevaFecha: "",
            _isAddBusy: false,

            // Etiquetas y notas
            etiquetas: [],
            nuevaEtiqueta: "",
            nota: "",
            estadoNota: "",

            // Drag & Drop tablero
            arrastrando: null,
            colOrigen: null,

            // -------- Helpers de formateo (usan utils.js) --------
            ymd: function (v) { return utils.toYMD(v); },
            pillFecha: function (f) { return utils.pillFecha(this.ymd(f)); },
            rangoBonito: function () { return utils.pillRango(this.fechaInicio, this.fechaFin); },
            initials: function (n) { return utils.initials(n); },
            shortName: function (n) { return utils.shortName(n); },

            // -------- Métricas de tareas simples --------
            get tareasHechas() { return this.tareas.filter(function (t) { return t.Hecho; }).length; },
            porcentajeProgreso: function () {
                var tot = this.tareas.length || 1;
                return Math.round(this.tareasHechas * 100 / tot);
            },

            // -------- DataTable computed (Miembros) --------
            get miembrosFiltrados() {
                const q = (this.qMiembro || "").toLowerCase();
                return (this.miembrosProyecto || []).filter(m =>
                    (m.Nombres || "").toLowerCase().includes(q) ||
                    (m.Correo || "").toLowerCase().includes(q) ||
                    (m.RolMiembro || "").toLowerCase().includes(q)
                );
            },
            get miembrosOrdenados() {
                const k = this.sortKey, dir = this.sortDir;
                return this.miembrosFiltrados.slice().sort((a, b) => {
                    const av = (a[k] ?? "").toString().toLowerCase();
                    const bv = (b[k] ?? "").toString().toLowerCase();
                    const cmp = av > bv ? 1 : av < bv ? -1 : 0;
                    return dir === "asc" ? cmp : -cmp;
                });
            },
            get totalPages() { return Math.max(1, Math.ceil(this.miembrosOrdenados.length / this.pageSize)); },
            get miembrosPagina() {
                const start = (this.page - 1) * this.pageSize;
                return this.miembrosOrdenados.slice(start, start + this.pageSize);
            },
            sortBy(k) {
                if (this.sortKey === k) this.sortDir = this.sortDir === "asc" ? "desc" : "asc";
                else { this.sortKey = k; this.sortDir = "asc"; }
                this.page = 1;
            },

            // ---------------- Ciclo de vida ----------------
            async init() {
                try {
                    await Promise.all([
                        this.cargarTablero(),
                        this.cargarMiembros(),
                        this.cargarTareas(),
                        this.cargarEtiquetas(),
                        this.cargarNota()
                    ]);
                } catch (e) {
                    console && console.warn && console.warn(e);
                }
            },

            // ---------------- Fechas del proyecto ----------------
            async guardarFechas() {
                this.msgFecha = "";
                if (this.fechaInicio && this.fechaFin) {
                    var i = new Date(this.fechaInicio + "T00:00:00");
                    var f = new Date(this.fechaFin + "T00:00:00");
                    if (f < i) { this.msgFecha = "La fecha fin no puede ser menor a la fecha inicio."; return; }
                }
                try {
                    await Promise.all([
                        fetch(U.EditarCampo, {
                            method: "POST", headers: { "Content-Type": "application/x-www-form-urlencoded" },
                            body: new URLSearchParams({ id: this.idProyecto, campo: "fechainicio", valor: this.fechaInicio })
                        }),
                        fetch(U.EditarCampo, {
                            method: "POST", headers: { "Content-Type": "application/x-www-form-urlencoded" },
                            body: new URLSearchParams({ id: this.idProyecto, campo: "fechafin", valor: this.fechaFin })
                        })
                    ]);
                    utils.toast("Fechas guardadas");
                } catch (e) { utils.toast("Error al guardar fechas", "error"); }
            },

            // ---------------- Tablero / metodología ----------------
            async cargarTablero() {
                try {
                    const r = await fetch(U.TableroData + "?idProyecto=" + this.idProyecto);
                    const d = await asJson(r);
                    this.metodologia = d.metodologia || "";
                    this.columnas = d.columnas || [];
                    this.aplicarColoresATareas();
                } catch (e) { utils.toast("No se pudo cargar el tablero", "error"); }
            },

            async cambiarMetodologia(regenerar) {
                if (!this.metodologia) return;
                try {
                    const r = await fetch(U.SetMetodologia, {
                        method: "POST", headers: { "Content-Type": "application/x-www-form-urlencoded" },
                        body: new URLSearchParams({ idProyecto: this.idProyecto, metodologia: this.metodologia, regenerar: regenerar })
                    });
                    const d = await asJson(r);
                    if (d.ok) {
                        this.metodologia = d.data.metodologia;
                        this.columnas = d.data.columnas;
                        this.aplicarColoresATareas();
                        utils.toast("Metodología aplicada");
                    }
                    else utils.toast(d.msg || "No se pudo cambiar", "error");
                } catch (e) { utils.toast("Error de red", "error"); }
            },

            renombrarColumna(col, titulo) {
                col.Titulo = titulo;
                fetch(U.RenombrarColumna, {
                    method: "POST",
                    headers: { "Content-Type": "application/x-www-form-urlencoded" },
                    body: new URLSearchParams({ idColumna: col.IdColumna, titulo: titulo })
                }).then(asJson).then(function (d) {
                    if (!d.ok) utils.toast("No se pudo renombrar", "error");
                }).catch(function () { utils.toast("Error de red", "error"); });
            },

            async soltarTarjeta(colDestino) {
                if (!this.arrastrando || !colDestino) return;

                // Orden en destino
                const idsDestino = Array.from(new Set(
                    (colDestino.tareas || []).map(x => x.IdTarea).concat([this.arrastrando.IdTarea])
                ));

                try {
                    const r = await fetch(U.MoverTareaTablero, {
                        method: "POST", headers: { "Content-Type": "application/x-www-form-urlencoded" },
                        body: new URLSearchParams({
                            idTarea: this.arrastrando.IdTarea,
                            idColumnaDestino: colDestino.IdColumna,
                            idsDestinoCsv: idsDestino.join(",")
                        })
                    });
                    const d = await asJson(r);

                    if (d.ok) {
                        if (this.colOrigen && Array.isArray(this.colOrigen.tareas)) {
                            this.colOrigen.tareas = this.colOrigen.tareas.filter(t => t.IdTarea !== this.arrastrando.IdTarea);
                        }
                        (colDestino.tareas = colDestino.tareas || []).push(this.arrastrando);

                        const nuevoColor = this.colorColumna(colDestino);
                        this.arrastrando._colColor = nuevoColor;
                        this.arrastrando._colBg = rgba(nuevoColor, 0.06);

                        await this.cargarTablero();
                        utils.toast("Tarea movida");
                    } else {
                        utils.toast("No se pudo mover", "error");
                    }
                } catch (e) {
                    utils.toast("Error de red", "error");
                } finally {
                    this.arrastrando = null; this.colOrigen = null;
                }
            },

            // ---------------- Miembros ----------------
            async cargarMiembros() {
                try {
                    const r = await fetch(U.MiembrosJson + "?idProyecto=" + this.idProyecto);
                    const lista = await asJson(r);
                    this.miembrosProyecto = Array.isArray(lista) ? lista : (lista.miembros || []);
                    this.page = Math.min(this.page, this.totalPages);
                } catch (e) { utils.toast("No se pudo cargar miembros", "error"); }
            },

            buscarUsuarios: utils.debounce(async function () {
                if (this.qUsuario.trim().length < 2) { this.usuariosEncontrados = []; return; }
                try {
                    const r = await fetch(U.BuscarUsuarios + "?q=" + encodeURIComponent(this.qUsuario));
                    this.usuariosEncontrados = await asJson(r);
                } catch (e) { this.usuariosEncontrados = []; }
            }, 250),

            async agregarMiembro(idUsuario, rolProyecto, rolSistema) {
                try {
                    const r = await fetch(U.AgregarMiembro, {
                        method: "POST", headers: { "Content-Type": "application/x-www-form-urlencoded" },
                        body: new URLSearchParams({
                            idProyecto: this.idProyecto,
                            idUsuario: idUsuario,
                            rolMiembro: rolProyecto || "Miembro",
                            rolSistema: rolSistema || ""
                        })
                    });
                    const lista = await asJson(r);
                    this.miembrosProyecto = Array.isArray(lista) ? lista : (lista.miembros || []);
                    this.qUsuario = ""; this.usuariosEncontrados = [];
                    utils.toast("Miembro agregado");
                    this.page = 1;
                } catch (e) { utils.toast("No se pudo agregar", "error"); }
            },

            async quitarMiembro(idUsuario) {
                try {
                    const r = await fetch(U.QuitarMiembro, {
                        method: "POST", headers: { "Content-Type": "application/x-www-form-urlencoded" },
                        body: new URLSearchParams({ idProyecto: this.idProyecto, idUsuario: idUsuario })
                    });
                    const d = await asJson(r);
                    if (d.ok) {
                        this.miembrosProyecto = d.miembros || [];
                        utils.toast("Miembro quitado");
                        this.page = Math.min(this.page, this.totalPages);
                    } else utils.toast("No se pudo quitar", "error");
                } catch (e) { utils.toast("Error de red", "error"); }
            },

            async enviarInvitacion() {
                if (!this.correo) { utils.toast("Ingresa un correo", "warn"); return; }
                try {
                    const r = await fetch(U.InvitarPorCorreo, {
                        method: "POST", headers: { "Content-Type": "application/x-www-form-urlencoded" },
                        body: new URLSearchParams({ idProyecto: this.idProyecto, correo: this.correo })
                    });
                    const d = await asJson(r);
                    if (d.ok) { utils.toast("Invitación enviada"); this.openInvite = false; this.correo = ""; }
                    else utils.toast(d.msg || "No se pudo enviar", "error");
                } catch (e) { utils.toast("Error de red", "error"); }
            },

            // ---------------- Tareas simples (tab "Tareas") ----------------
            async cargarTareas() {
                try {
                    const r = await fetch(U.ListarTareas + "?idProyecto=" + this.idProyecto);
                    this.tareas = await asJson(r);
                } catch (e) { utils.toast("No se pudo cargar tareas", "error"); }
            },

            async crearTarea() {
                const pid = Number(this.idProyecto || PID || 0);
                if (!pid) { utils.toast('Falta id del proyecto', 'error'); return; }

                if (this._isAddBusy) return;
                if (!this.nuevaTitulo.trim()) { utils.toast("Escribe un título", "warn"); return; }
                this._isAddBusy = true;
                try {
                    const r = await fetch(U.CrearTarea, {
                        method: "POST", headers: { "Content-Type": "application/x-www-form-urlencoded" },
                        body: new URLSearchParams({
                            idProyecto: String(pid),
                            titulo: this.nuevaTitulo,
                            asignadoA: this.nuevaAsignado || '',
                            fechaVenc: this.nuevaFecha || ''
                        })
                    });
                    const d = await asJson(r);
                    if (d.ok) {
                        this.tareas = d.tareas;
                        this.nuevaTitulo = ""; this.nuevaAsignado = ""; this.nuevaFecha = "";
                    } else utils.toast(d.msg || "No se pudo crear", "error");
                } catch (e) { utils.toast("Error de red", "error"); }
                finally { this._isAddBusy = false; }
            },

            async marcar(t, checked) {
                const prev = !!t.Hecho;
                t.Hecho = checked; // UI optimista
                try {
                    const r = await fetch(U.MarcarTarea, {
                        method: "POST", headers: { "Content-Type": "application/x-www-form-urlencoded" },
                        body: new URLSearchParams({ idTarea: t.IdTarea, hecho: checked, idProyecto: this.idProyecto })
                    });
                    const d = await asJson(r);
                    if (d.ok) this.tareas = d.tareas;
                    else { t.Hecho = prev; utils.toast("No se pudo marcar", "error"); }
                } catch (e) { t.Hecho = prev; utils.toast("Error de red", "error"); }
            },

            async editarTarea(t, campo, valor) {
                try {
                    const r = await fetch(U.EditarTarea, {
                        method: "POST", headers: { "Content-Type": "application/x-www-form-urlencoded" },
                        body: new URLSearchParams({ idTarea: t.IdTarea, campo: campo, valor: valor, idProyecto: this.idProyecto })
                    });
                    const d = await asJson(r);
                    if (d.ok) this.tareas = d.tareas;
                    else utils.toast("No se pudo actualizar", "error");
                } catch (e) { utils.toast("Error de red", "error"); }
            },

            async eliminarTarea(t) {
                try {
                    const r = await fetch(U.EliminarTarea, {
                        method: "POST", headers: { "Content-Type": "application/x-www-form-urlencoded" },
                        body: new URLSearchParams({ idTarea: t.IdTarea, idProyecto: this.idProyecto })
                    });
                    const d = await asJson(r);
                    if (d.ok) this.tareas = d.tareas;
                    else utils.toast("No se pudo eliminar", "error");
                } catch (e) { utils.toast("Error de red", "error"); }
            },

            // ---------------- Etiquetas / Notas ----------------
            async cargarEtiquetas() {
                try {
                    const r = await fetch(U.ListarEtiquetas + "?idProyecto=" + this.idProyecto);
                    this.etiquetas = await asJson(r);
                } catch (e) { utils.toast("No se pudo cargar etiquetas", "error"); }
            },

            async agregarEtiqueta() {
                if (!this.nuevaEtiqueta.trim()) { utils.toast("Ingresa un nombre", "warn"); return; }
                try {
                    const r = await fetch(U.AgregarEtiqueta, {
                        method: "POST", headers: { "Content-Type": "application/x-www-form-urlencoded" },
                        body: new URLSearchParams({ idProyecto: this.idProyecto, nombre: this.nuevaEtiqueta })
                    });
                    const d = await asJson(r);
                    if (d.ok) { this.etiquetas = d.etiquetas; this.nuevaEtiqueta = ""; }
                    else utils.toast("No se pudo agregar", "error");
                } catch (e) { utils.toast("Error de red", "error"); }
            },

            async quitarEtiqueta(e) {
                try {
                    const r = await fetch(U.QuitarEtiqueta, {
                        method: "POST", headers: { "Content-Type": "application/x-www-form-urlencoded" },
                        body: new URLSearchParams({ idProyecto: this.idProyecto, idEtiqueta: e.IdEtiqueta })
                    });
                    const d = await asJson(r);
                    if (d.ok) this.etiquetas = d.etiquetas;
                    else utils.toast("No se pudo quitar", "error");
                } catch (e) { utils.toast("Error de red", "error"); }
            },

            async cargarNota() {
                try {
                    const r = await fetch(U.ObtenerNota + "?idProyecto=" + this.idProyecto);
                    const n = await asJson(r);
                    this.nota = (n && n.Contenido) || "";
                } catch (e) { utils.toast("No se pudo cargar la nota", "error"); }
            },

            guardarNota: utils.debounce(async function () {
                try {
                    const r = await fetch(U.GuardarNota, {
                        method: "POST", headers: { "Content-Type": "application/x-www-form-urlencoded" },
                        body: new URLSearchParams({ idProyecto: this.idProyecto, contenido: this.nota })
                    });
                    const d = await asJson(r);
                    this.estadoNota = d.ok ? "Guardado" : "No se guardó";
                } catch (e) {
                    this.estadoNota = "No se guardó";
                }
                var self = this; setTimeout(function () { self.estadoNota = ""; }, 1200);
            }, 500),

            // Campos básicos inline (nombre, descripción, etc.)
            guardar: function (campo, valor) {
                fetch(U.EditarCampo, {
                    method: "POST", headers: { "Content-Type": "application/x-www-form-urlencoded" },
                    body: new URLSearchParams({ id: this.idProyecto, campo: campo, valor: valor })
                }).then(function (r) { return asJson(r); })
                    .then(function (d) { utils.toast(d.ok ? "Guardado" : "No se guardó", d.ok ? "ok" : "error"); })
                    .catch(function () { utils.toast("Error de red", "error"); });
            }
        };
    };

    // Sub-componente Alpine para el DataTable (Miembros)
    function miembrosDT() {
        return {
            openAdd: false,
            rolProyecto: "Miembro",
            rolSistema: "",

            // proxies hacia el $root (detalleProyecto)
            get page() { return this.$root.page; }, set page(v) { this.$root.page = v; },
            get pageSize() { return this.$root.pageSize; }, set pageSize(v) { this.$root.pageSize = v; },
            get totalPages() { return this.$root.totalPages; },
            get sortKey() { return this.$root.sortKey; }, set sortKey(v) { this.$root.sortKey = v; },
            get sortDir() { return this.$root.sortDir; }, set sortDir(v) { this.$root.sortDir = v; },

            // Acceso a computeds del root
            get miembrosPagina() { return this.$root.miembrosPagina; },
            get miembrosOrdenados() { return this.$root.miembrosOrdenados; },
            get miembrosFiltrados() { return this.$root.miembrosFiltrados; },

            sortBy(k) { this.$root.sortBy(k); },

            agregar(u) {
                this.$root.agregarMiembro(u.IdUsuario, this.rolProyecto, this.rolSistema);
                this.openAdd = false;
            }
        };
    }
    w.miembrosDT = miembrosDT;

})(window);
