using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using System.Net.Mail;
using Proyecto_GCS.Models;
using Proyecto_GCS.Repositories;
using Proyecto_GCS.Utils;

namespace Proyecto_GCS.Controllers
{
    [Authorize]
    public class ProyectosController : Controller
    {
        private readonly IProyectoRepository _repo = new ProyectoRepository();

        // =======================
        // Helpers de identidad
        // =======================
        private int UserId()
        {
            var cp = User as ClaimsPrincipal;
            string Get(string type) => cp?.FindFirst(type)?.Value;

            var raw = Get(ClaimTypes.NameIdentifier) ?? Get("IdUsuario");
            if (int.TryParse(raw, out var id))
                return id;

            if (Session?["IdUsuario"] is int sId)
                return sId;
            if (Session?["IdUsuario"] is string sRaw && int.TryParse(sRaw, out var sParsed))
                return sParsed;

            return 0;
        }

        private string UserEmail()
        {
            var cp = User as ClaimsPrincipal;
            return cp?.FindFirst(ClaimTypes.Email)?.Value ?? string.Empty;
        }

        // =======================
        // Utilidades JSON
        // =======================
        private JsonResult JsonOk(object data = null)
        {
            if (data == null) return Json(new { ok = true }, JsonRequestBehavior.AllowGet);

            var dict = new Dictionary<string, object> { ["ok"] = true };
            foreach (var p in data.GetType().GetProperties())
                dict[p.Name] = p.GetValue(data, null);
            return Json(dict, JsonRequestBehavior.AllowGet);
        }

        private JsonResult JsonError(string msg, object extra = null)
        {
            var dict = new Dictionary<string, object> { ["ok"] = false, ["msg"] = msg ?? "Error" };
            if (extra != null)
                foreach (var p in extra.GetType().GetProperties())
                    dict[p.Name] = p.GetValue(extra, null);
            return Json(dict, JsonRequestBehavior.AllowGet);
        }

        private Task<bool> TieneAcceso(int idProyecto)
            => _repo.UsuarioTieneAccesoProyectoAsync(idProyecto, UserId());

        // =======================
        // LISTADO
        // =======================
        [HttpGet]
        public async Task<ActionResult> Index(string q = "", int page = 1, int pageSize = 12)
        {
            var idUsuario = UserId();
            bool verTodos = false; // si algún día quieres: User.IsInRole("Admin")
            var (items, total) = await _repo.ListarAsync(q, page, pageSize, idUsuario, verTodos);

            ViewBag.Query = q;
            ViewBag.Page = page;
            ViewBag.PageSize = pageSize;
            ViewBag.Total = total;

            return View(items);
        }

        // =======================
        // DETALLE
        // =======================
        [HttpGet]
        public async Task<ActionResult> Detalle(int id)
        {
            var p = await _repo.ObtenerAsync(id);
            if (p == null) return HttpNotFound();

            var esOwner = (p.IdUsuarioOwner.HasValue && p.IdUsuarioOwner.Value == UserId());
            ViewBag.EsOwner = esOwner;

            if (p.IdUsuarioOwner.HasValue && string.IsNullOrEmpty(p.OwnerNombre))
            {
                var owner = await _repo.ObtenerUsuarioPorIdAsync(p.IdUsuarioOwner.Value);
                p.OwnerNombre = owner?.Nombres;
            }
            return View("Detalle", p);
        }

        // =======================
        // ESTADO / CAMPOS
        // =======================
        [HttpPost]
        public async Task<ActionResult> CambiarEstado(int id, string estado)
        {
            try
            {
                if (id <= 0 || string.IsNullOrWhiteSpace(estado)) return JsonError("Parámetros inválidos");
                if (!await TieneAcceso(id)) return JsonError("Sin acceso");

                var ok = await _repo.CambiarEstadoAsync(id, estado.Trim());
                return ok ? JsonOk() : JsonError("No se pudo cambiar");
            }
            catch (Exception ex) { return JsonError("Error de servidor", new { ex.Message }); }
        }

        [HttpPost]
        public async Task<ActionResult> EditarCampo(int id, string campo, string valor)
        {
            try
            {
                if (id <= 0) return JsonError("id requerido");
                if (!await TieneAcceso(id)) return JsonError("Sin acceso");

                var ok = await _repo.EditarCampoAsync(id, campo, valor);
                return ok ? JsonOk() : JsonError("No se guardó");
            }
            catch (Exception ex) { return JsonError("Error de servidor", new { ex.Message }); }
        }

        // =======================
        // TABLERO / METODOLOGÍA
        // =======================
        [HttpGet]
        public async Task<ActionResult> TableroData(int idProyecto)
        {
            try
            {
                if (idProyecto <= 0) return JsonError("idProyecto requerido");
                if (!await TieneAcceso(idProyecto)) return JsonError("Sin acceso");

                var data = await _repo.ObtenerTableroAsync(idProyecto);
                return Json(data, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex) { return JsonError("Error de servidor", new { ex.Message }); }
        }

        [HttpPost]
        public async Task<ActionResult> SetMetodologia(int idProyecto, string metodologia, bool regenerar)
        {
            try
            {
                if (idProyecto <= 0) return JsonError("idProyecto requerido");
                if (string.IsNullOrWhiteSpace(metodologia)) return JsonError("metodologia requerida");
                if (!await TieneAcceso(idProyecto)) return JsonError("Sin acceso");

                var ok = await _repo.EstablecerMetodologiaAsync(idProyecto, metodologia, regenerar);
                if (!ok) return JsonError("No se pudo aplicar la metodología");

                var data = await _repo.ObtenerTableroAsync(idProyecto);
                return JsonOk(new { data });
            }
            catch (Exception ex) { return JsonError("Error de servidor", new { ex.Message }); }
        }

        [HttpPost]
        public async Task<ActionResult> RenombrarColumna(int idColumna, string titulo)
        {
            try
            {
                if (idColumna <= 0 || string.IsNullOrWhiteSpace(titulo))
                    return JsonError("Parámetros inválidos");

                var ok = await _repo.RenombrarColumnaAsync(idColumna, titulo.Trim());
                return ok ? JsonOk() : JsonError("No se pudo renombrar");
            }
            catch (Exception ex) { return JsonError("Error de servidor", new { ex.Message }); }
        }

        [HttpPost]
        public async Task<ActionResult> MoverTareaTablero(int idTarea, int idColumnaDestino, string idsDestinoCsv)
        {
            try
            {
                if (idTarea <= 0 || idColumnaDestino <= 0)
                    return JsonError("Parámetros inválidos");

                var ok = await _repo.MoverTareaAColumnaAsync(idTarea, idColumnaDestino);
                if (!ok) return JsonError("No se pudo mover");

                if (!string.IsNullOrWhiteSpace(idsDestinoCsv))
                {
                    var ids = idsDestinoCsv
                        .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(s => int.TryParse(s, out var n) ? n : 0)
                        .Where(n => n > 0)
                        .ToList();

                    if (ids.Count > 0)
                        await _repo.ReordenarTareasEnColumnaAsync(idColumnaDestino, ids);
                }

                return JsonOk();
            }
            catch (Exception ex) { return JsonError("Error de servidor", new { ex.Message }); }
        }

        // =======================
        // MIEMBROS
        // =======================
        [HttpGet]
        public async Task<ActionResult> MiembrosJson(int idProyecto)
        {
            try
            {
                if (idProyecto <= 0) return JsonError("idProyecto requerido");
                if (!await TieneAcceso(idProyecto)) return JsonError("Sin acceso");

                var p = await _repo.ObtenerAsync(idProyecto);
                var miembros = (await _repo.ListarMiembrosAsync(idProyecto)).ToList();

                var lista = new List<object>();
                if (p != null && p.IdUsuarioOwner.HasValue)
                {
                    var owner = await _repo.ObtenerUsuarioPorIdAsync(p.IdUsuarioOwner.Value);
                    if (owner != null)
                    {
                        lista.Add(new
                        {
                            IdProyecto = idProyecto,
                            IdUsuario = owner.IdUsuario,
                            Nombres = owner.Nombres,
                            Correo = owner.Correo,
                            RolMiembro = "Owner",
                            Estado = true,
                            EsOwner = true
                        });
                    }
                }

                foreach (var m in miembros)
                {
                    if (!(p?.IdUsuarioOwner.HasValue == true && p.IdUsuarioOwner.Value == m.IdUsuario))
                        lista.Add(new
                        {
                            m.IdProyecto,
                            m.IdUsuario,
                            m.Nombres,
                            m.Correo,
                            m.RolMiembro,
                            m.Estado,
                            EsOwner = false
                        });
                }

                return Json(lista, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex) { return JsonError("Error de servidor", new { ex.Message }); }
        }

        [HttpGet]
        public async Task<ActionResult> BuscarUsuarios(string q)
        {
            try
            {
                var users = await _repo.BuscarUsuariosAsync(q ?? "", 10);
                return Json(users, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex) { return JsonError("Error de servidor", new { ex.Message }); }
        }

        [HttpPost]
        public async Task<ActionResult> AgregarMiembro(int idProyecto, int idUsuario, string rolMiembro, string rolSistema = "")
        {
            try
            {
                if (idProyecto <= 0 || idUsuario <= 0)
                    return JsonError("Parámetros inválidos");
                if (!await TieneAcceso(idProyecto)) return JsonError("Sin acceso");

                var ok = await _repo.AgregarMiembroAsync(idProyecto, idUsuario, rolMiembro ?? "Miembro");
                if (!ok) return JsonError("No se pudo agregar");

                if (!string.IsNullOrWhiteSpace(rolSistema))
                    await _repo.ActualizarRolSistemaAsync(idUsuario, rolSistema);

                return await MiembrosJson(idProyecto);
            }
            catch (Exception ex) { return JsonError("Error de servidor", new { ex.Message }); }
        }

        [HttpPost]
        public async Task<ActionResult> QuitarMiembro(int idProyecto, int idUsuario)
        {
            try
            {
                if (idProyecto <= 0 || idUsuario <= 0)
                    return JsonError("Parámetros inválidos");
                if (!await TieneAcceso(idProyecto)) return JsonError("Sin acceso");

                var ok = await _repo.QuitarMiembroAsync(idProyecto, idUsuario);
                if (!ok) return JsonError("No se pudo quitar");

                var miembrosResp = await MiembrosJson(idProyecto) as JsonResult;
                return JsonOk(new { miembros = miembrosResp?.Data });
            }
            catch (Exception ex) { return JsonError("Error de servidor", new { ex.Message }); }
        }

        // =======================
        // INVITACIONES (correo)
        // =======================
        [HttpPost]
        public async Task<ActionResult> InvitarPorCorreo(int idProyecto, string correo)
        {
            try
            {
                if (idProyecto <= 0) return JsonError("idProyecto requerido");
                if (string.IsNullOrWhiteSpace(correo)) return JsonError("correo requerido");
                if (!await TieneAcceso(idProyecto)) return JsonError("Sin acceso");

                // 1) Crear invitación (token)
                var token = await _repo.CrearInvitacionAsync(idProyecto, correo.Trim(), UserId(), TimeSpan.FromDays(7));

                // 2) Construir link absoluto
                var link = Url.Action(
                    "AceptarInvitacion",
                    "Proyectos",
                    new { token },
                    Request?.Url?.Scheme ?? "http"
                );

                // 3) Enviar correo
                var html = $@"
                    <div style=""font-family:Segoe UI,Roboto,Arial,sans-serif;font-size:14px;"">
                        <p>Has sido invitado a colaborar en un proyecto.</p>
                        <p>
                            <a href=""{link}"" style=""background:#16a34a;color:#fff;padding:10px 14px;border-radius:8px;text-decoration:none;"">
                                Abrir proyecto
                            </a>
                        </p>
                        <p>Si no funciona el botón, copia y pega esta URL en tu navegador:<br/>{HttpUtility.HtmlEncode(link)}</p>
                    </div>";
                await Mailer.EnviarAsync(correo.Trim(), "Invitación a proyecto", html);

                return JsonOk(new { token, link });
            }
            catch (SmtpException ex) { return JsonError("SMTP: " + ex.Message); }
            catch (Exception ex) { return JsonError("Error de servidor", new { ex.Message }); }
        }

        [AllowAnonymous]
        [HttpGet]
        public async Task<ActionResult> AceptarInvitacion(string token)
        {
            if (string.IsNullOrWhiteSpace(token)) return Content("Invitación inválida.");

            var inv = await _repo.ObtenerInvitacionPorTokenAsync(token);
            if (inv == null) return Content("Invitación inválida.");
            if (inv.ExpiraUtc <= DateTime.UtcNow) return Content("La invitación ha expirado.");

            // Si el usuario no está autenticado, muéstrale el token y pídele iniciar sesión
            if (!Request.IsAuthenticated)
                return Content("Para aceptar la invitación, inicia sesión y vuelve a abrir este enlace: " +
                               Url.Action("AceptarInvitacion", "Proyectos", new { token }, Request.Url.Scheme));

            // Agregar como miembro (si no lo es)
            await _repo.AgregarMiembroAsync(inv.IdProyecto, UserId(), "Miembro");
            await _repo.MarcarInvitacionUsadaAsync(inv.IdInvitacion);

            return RedirectToAction("Detalle", new { id = inv.IdProyecto });
        }

        // =======================
        // TAREAS
        // =======================
        [HttpGet]
        public async Task<ActionResult> ListarTareas(int idProyecto)
        {
            try
            {
                if (idProyecto <= 0) return JsonError("idProyecto requerido");
                if (!await TieneAcceso(idProyecto)) return JsonError("Sin acceso");

                var tareas = await _repo.ListarTareasAsync(idProyecto);
                return Json(tareas, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex) { return JsonError("Error de servidor", new { ex.Message }); }
        }

        [HttpPost]
        public async Task<ActionResult> CrearTarea(int idProyecto, string titulo, int? asignadoA, string fechaVenc = null)
        {
            try
            {
                if (idProyecto <= 0) return JsonError("idProyecto requerido");
                if (string.IsNullOrWhiteSpace(titulo)) return JsonError("titulo requerido");
                if (!await TieneAcceso(idProyecto)) return JsonError("Sin acceso");

                DateTime? venc = null;
                if (!string.IsNullOrWhiteSpace(fechaVenc))
                {
                    if (DateTime.TryParseExact(fechaVenc, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
                        venc = dt;
                    else
                        return JsonError("fechaVenc inválida (yyyy-MM-dd)");
                }

                var idTarea = await _repo.CrearTareaAsync(idProyecto, titulo.Trim(), asignadoA, venc);
                var tareas = await _repo.ListarTareasAsync(idProyecto);
                return JsonOk(new { idTarea, tareas });
            }
            catch (Exception ex) { return JsonError("Error de servidor", new { ex.Message }); }
        }

        [HttpPost]
        public async Task<ActionResult> MarcarTarea(int idProyecto, int idTarea, bool hecho)
        {
            try
            {
                if (idProyecto <= 0 || idTarea <= 0) return JsonError("Parámetros inválidos");
                if (!await TieneAcceso(idProyecto)) return JsonError("Sin acceso");

                var ok = await _repo.MarcarTareaAsync(idTarea, hecho);
                var tareas = await _repo.ListarTareasAsync(idProyecto);
                return ok ? JsonOk(new { tareas }) : JsonError("No se pudo marcar");
            }
            catch (Exception ex) { return JsonError("Error de servidor", new { ex.Message }); }
        }

        [HttpPost]
        public async Task<ActionResult> EditarTarea(int idProyecto, int idTarea, string campo, string valor)
        {
            try
            {
                if (idProyecto <= 0 || idTarea <= 0) return JsonError("Parámetros inválidos");
                if (string.IsNullOrWhiteSpace(campo)) return JsonError("campo requerido");
                if (!await TieneAcceso(idProyecto)) return JsonError("Sin acceso");

                var ok = await _repo.EditarTareaCampoAsync(idTarea, (campo ?? "").Trim(), valor);
                var tareas = await _repo.ListarTareasAsync(idProyecto);
                return ok ? JsonOk(new { tareas }) : JsonError("No se pudo actualizar");
            }
            catch (Exception ex) { return JsonError("Error de servidor", new { ex.Message }); }
        }

        [HttpPost]
        public async Task<ActionResult> EliminarTarea(int idProyecto, int idTarea)
        {
            try
            {
                if (idProyecto <= 0 || idTarea <= 0) return JsonError("Parámetros inválidos");
                if (!await TieneAcceso(idProyecto)) return JsonError("Sin acceso");

                var ok = await _repo.EliminarTareaAsync(idTarea);
                var tareas = await _repo.ListarTareasAsync(idProyecto);
                return ok ? JsonOk(new { tareas }) : JsonError("No se pudo eliminar");
            }
            catch (Exception ex) { return JsonError("Error de servidor", new { ex.Message }); }
        }

        // =======================
        // ETIQUETAS / NOTAS
        // =======================
        [HttpGet]
        public async Task<ActionResult> ListarEtiquetas(int idProyecto)
        {
            try
            {
                if (idProyecto <= 0) return JsonError("idProyecto requerido");
                if (!await TieneAcceso(idProyecto)) return JsonError("Sin acceso");

                var etiquetas = await _repo.ListarEtiquetasAsync(idProyecto);
                return Json(etiquetas, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex) { return JsonError("Error de servidor", new { ex.Message }); }
        }

        [HttpPost]
        public async Task<ActionResult> AgregarEtiqueta(int idProyecto, string nombre)
        {
            try
            {
                if (idProyecto <= 0) return JsonError("idProyecto requerido");
                if (string.IsNullOrWhiteSpace(nombre)) return JsonError("nombre requerido");
                if (!await TieneAcceso(idProyecto)) return JsonError("Sin acceso");

                var e = await _repo.AgregarEtiquetaAsync(idProyecto, nombre);
                var etiquetas = await _repo.ListarEtiquetasAsync(idProyecto);
                return e != null ? JsonOk(new { etiquetas }) : JsonError("No se pudo agregar");
            }
            catch (Exception ex) { return JsonError("Error de servidor", new { ex.Message }); }
        }

        [HttpPost]
        public async Task<ActionResult> QuitarEtiqueta(int idProyecto, int idEtiqueta)
        {
            try
            {
                if (idProyecto <= 0 || idEtiqueta <= 0) return JsonError("Parámetros inválidos");
                if (!await TieneAcceso(idProyecto)) return JsonError("Sin acceso");

                var ok = await _repo.QuitarEtiquetaAsync(idProyecto, idEtiqueta);
                var etiquetas = await _repo.ListarEtiquetasAsync(idProyecto);
                return ok ? JsonOk(new { etiquetas }) : JsonError("No se pudo quitar");
            }
            catch (Exception ex) { return JsonError("Error de servidor", new { ex.Message }); }
        }

        [HttpGet]
        public async Task<ActionResult> ObtenerNota(int idProyecto)
        {
            try
            {
                if (idProyecto <= 0) return JsonError("idProyecto requerido");
                if (!await TieneAcceso(idProyecto)) return JsonError("Sin acceso");

                var nota = await _repo.ObtenerNotaAsync(idProyecto);
                return Json(nota ?? new ProyectoNota { IdProyecto = idProyecto, Contenido = "" }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex) { return JsonError("Error de servidor", new { ex.Message }); }
        }

        [HttpPost]
        public async Task<ActionResult> GuardarNota(int idProyecto, string contenido)
        {
            try
            {
                if (idProyecto <= 0) return JsonError("idProyecto requerido");
                if (!await TieneAcceso(idProyecto)) return JsonError("Sin acceso");

                var ok = await _repo.GuardarNotaAsync(idProyecto, contenido ?? "");
                return ok ? JsonOk() : JsonError("No se guardó");
            }
            catch (Exception ex) { return JsonError("Error de servidor", new { ex.Message }); }
        }
    }
}
