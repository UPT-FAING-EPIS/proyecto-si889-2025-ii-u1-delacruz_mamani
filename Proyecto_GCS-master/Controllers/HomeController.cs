using System;
using System.Data.SqlClient;
using System.Globalization;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Web.Configuration;
using System.Web.Mvc;
using Dapper;
using Proyecto_GCS.Models;

namespace Proyecto_GCS.Controllers
{
    [Authorize]
    public class HomeController : Controller
    {
        private string Cnx => WebConfigurationManager.ConnectionStrings["ConexionGCS"].ConnectionString;
        private SqlConnection Conn() => new SqlConnection(Cnx);

        private int UserId()
        {
            var cp = User as ClaimsPrincipal;
            string Get(string type) => cp?.FindFirst(type)?.Value;

            var raw = Get(ClaimTypes.NameIdentifier) ?? Get("IdUsuario");
            if (int.TryParse(raw, out var id)) return id;

            if (Session?["IdUsuario"] is int sId) return sId;
            if (Session?["IdUsuario"] is string sRaw && int.TryParse(sRaw, out var sParsed)) return sParsed;
            return 0;
        }

        public async Task<ActionResult> Index()
        {
            var vm = new DashboardVm
            {
                UserName = User?.Identity?.IsAuthenticated == true ? (User.Identity.Name ?? "Usuario") : "Invitado"
            };

            var uid = UserId();
            if (uid <= 0) return View(vm);

            using (var cn = Conn())
            {
                await cn.OpenAsync();

                // NOTA: todos los conteos se basan sólo en proyectos a los que el usuario tiene acceso
                // (owner o miembro)
                var sql = @"
DECLARE @U INT = @uid;
DECLARE @HOY DATE = CONVERT(date, SYSUTCDATETIME());

;WITH P AS (
    SELECT p.IdProyecto, p.Estado, p.Nombre
    FROM dbo.Proyecto p
    WHERE p.IdUsuarioOwner = @U
       OR EXISTS (SELECT 1 FROM dbo.ProyectoMiembro pm WHERE pm.IdProyecto = p.IdProyecto AND pm.IdUsuario = @U)
),
T AS (
    SELECT t.*
    FROM dbo.ProyectoTarea t
    JOIN P ON P.IdProyecto = t.IdProyecto
)
-- 1) Proyectos (totales por estado)
SELECT 
    COUNT(1) AS ProyectosTotal,
    SUM(CASE WHEN P.Estado = N'Activo'  THEN 1 ELSE 0 END) AS ProyActivos,
    SUM(CASE WHEN P.Estado = N'Pausado' THEN 1 ELSE 0 END) AS ProyPausados,
    SUM(CASE WHEN P.Estado = N'Cerrado' THEN 1 ELSE 0 END) AS ProyCerrados
FROM P;

-- 2) Cambios pendientes (mapeo: tareas Estado IN (Pendiente, Bloqueado))
SELECT
    SUM(CASE WHEN (t.Estado IN (N'Pendiente', N'Bloqueado')) THEN 1 ELSE 0 END) AS CambiosPendientes,
    COUNT(DISTINCT CASE WHEN (t.Estado IN (N'Pendiente', N'Bloqueado')) THEN t.IdProyecto END) AS CambiosProyectos
FROM T t;

-- 3) Issues abiertos (tareas no hechas) por severidad (mapeo por Estado)
SELECT
    SUM(CASE WHEN t.Hecho = 0 THEN 1 ELSE 0 END) AS IssuesAbiertos,
    SUM(CASE WHEN t.Hecho = 0 AND t.Estado = N'Bloqueado'  THEN 1 ELSE 0 END) AS Criticos,
    SUM(CASE WHEN t.Hecho = 0 AND t.Estado = N'En proceso' THEN 1 ELSE 0 END) AS Mayores,
    SUM(CASE WHEN t.Hecho = 0 AND t.Estado = N'Pendiente'  THEN 1 ELSE 0 END) AS Menores
FROM T t;

-- 4) Deploys hoy (mapeo: tareas Hechas con FechaFin = HOY)
SELECT COUNT(1) AS DeploysHoy
FROM T t
WHERE t.Hecho = 1 AND t.FechaFin = @HOY;

-- 5) Último 'deploy' (usamos la última tarea hecha por FechaCreacionUtc como aproximación)
SELECT TOP 1 t.FechaCreacionUtc
FROM T t
WHERE t.Hecho = 1
ORDER BY t.FechaCreacionUtc DESC;

-- 6) Actividad reciente (últimas 5 tareas creadas)
SELECT TOP 5 
    t.IdTarea, t.Titulo, t.Estado, t.Hecho, t.FechaCreacionUtc,
    p.IdProyecto, p.Nombre AS ProyectoNombre
FROM T t
JOIN P p ON p.IdProyecto = t.IdProyecto
ORDER BY t.FechaCreacionUtc DESC;
";

                using (var multi = await cn.QueryMultipleAsync(sql, new { uid }))
                {
                    // 1)
                    var p = await multi.ReadFirstOrDefaultAsync();
                    if (p != null)
                    {
                        vm.ProyectosTotal = (int)(p.ProyectosTotal ?? 0);
                        vm.ProyActivos = (int)(p.ProyActivos ?? 0);
                        vm.ProyPausados = (int)(p.ProyPausados ?? 0);
                        vm.ProyCerrados = (int)(p.ProyCerrados ?? 0);
                    }

                    // 2)
                    var c = await multi.ReadFirstOrDefaultAsync();
                    if (c != null)
                    {
                        vm.CambiosPendientes = (int)(c.CambiosPendientes ?? 0);
                        vm.CambiosProyectos = (int)(c.CambiosProyectos ?? 0);
                    }

                    // 3)
                    var i = await multi.ReadFirstOrDefaultAsync();
                    if (i != null)
                    {
                        vm.IssuesAbiertos = (int)(i.IssuesAbiertos ?? 0);
                        vm.IssuesCriticos = (int)(i.Criticos ?? 0);
                        vm.IssuesMayores = (int)(i.Mayores ?? 0);
                        vm.IssuesMenores = (int)(i.Menores ?? 0);
                    }

                    // 4)
                    vm.DeploysHoy = await multi.ReadFirstOrDefaultAsync<int>();

                    // 5)
                    var last = await multi.ReadFirstOrDefaultAsync<DateTime?>();
                    vm.UltimoDeployHace = HumanizarHace(last);

                    // 6)
                    vm.ActividadReciente = (await multi.ReadAsync<ActividadDto>()).ToList();
                }
            }

            return View(vm);
        }

        private static string HumanizarHace(DateTime? utc)
        {
            if (utc == null) return "—";
            var span = DateTime.UtcNow - utc.Value;
            if (span.TotalSeconds < 90) return "hace 1 min";
            if (span.TotalMinutes < 60) return $"hace {Math.Round(span.TotalMinutes):0} min";
            if (span.TotalHours < 24) return $"hace {Math.Round(span.TotalHours):0} h";
            return $"hace {Math.Round(span.TotalDays):0} d";
        }
    }
}
