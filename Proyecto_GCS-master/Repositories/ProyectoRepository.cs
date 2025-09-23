using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Configuration;
using Dapper;
using Proyecto_GCS.Models;

namespace Proyecto_GCS.Repositories
{
    public class ProyectoRepository : IProyectoRepository
    {
        private readonly string _cnx;
        public ProyectoRepository()
        {
            _cnx = WebConfigurationManager.ConnectionStrings["ConexionGCS"].ConnectionString;
        }
        private SqlConnection Conn() => new SqlConnection(_cnx);

        // ===== Proyectos =====

        public async Task<int> CrearRapidoAsync(string codigo, string nombre, int idOwner)
        {
            using (var cn = Conn())
            {
                const string sql = @"
INSERT INTO dbo.Proyecto (Codigo, Nombre, Estado, IdUsuarioOwner)
VALUES (@Codigo, @Nombre, N'Activo', @IdOwner);
SELECT CAST(SCOPE_IDENTITY() AS INT);";
                return await cn.ExecuteScalarAsync<int>(sql, new { Codigo = codigo, Nombre = nombre, IdOwner = idOwner });
            }
        }

        public async Task<bool> EditarCampoAsync(int id, string campo, string valor)
        {
            string col = null;
            switch ((campo ?? "").Trim().ToLowerInvariant())
            {
                case "nombre": col = "Nombre"; break;
                case "descripcion": col = "Descripcion"; break;
                case "fechainicio": col = "FechaInicio"; break;
                case "fechafin": col = "FechaFin"; break;
                case "idusuarioowner": col = "IdUsuarioOwner"; break;
                default: return false;
            }

            object v = valor;

            if (col == "FechaInicio" || col == "FechaFin")
            {
                if (string.IsNullOrWhiteSpace(valor)) v = DBNull.Value;
                else
                {
                    if (!DateTime.TryParseExact(valor, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
                        return false;
                    v = dt;
                }
            }
            else if (col == "IdUsuarioOwner")
            {
                if (string.IsNullOrWhiteSpace(valor)) v = DBNull.Value;
                else
                {
                    if (!int.TryParse(valor, out var n)) return false;
                    v = n;
                }
            }

            var sql = $"UPDATE dbo.Proyecto SET {col}=@valor WHERE IdProyecto=@id;";
            using (var cn = Conn())
                return (await cn.ExecuteAsync(sql, new { id, valor = v })) > 0;
        }

        public async Task<(IEnumerable<Proyecto> items, int total)> ListarAsync(string q, int page, int pageSize, int idUsuario, bool verTodos = false)
        {
            q = (q ?? "").Trim();
            using (var cn = Conn())
            {
                var where = "WHERE 1=1";
                if (!string.IsNullOrEmpty(q))
                    where += " AND (p.Nombre LIKE @Q OR p.Codigo LIKE @Q)";

                if (!verTodos)
                    where += @" AND (
                        p.IdUsuarioOwner = @IdUsuario OR
                        EXISTS(SELECT 1 FROM dbo.ProyectoMiembro pm WHERE pm.IdProyecto = p.IdProyecto AND pm.IdUsuario = @IdUsuario)
                    )";

                var sql = $@"
SELECT p.*, u.Nombres AS OwnerNombre
FROM dbo.Proyecto p
LEFT JOIN dbo.Seguridad_Usuario u ON u.IdUsuario = p.IdUsuarioOwner
{where}
ORDER BY p.FechaCreacionUtc DESC
OFFSET @Skip ROWS FETCH NEXT @Take ROWS ONLY;

SELECT COUNT(1)
FROM dbo.Proyecto p
{where};";

                var args = new { Q = $"%{q}%", Skip = (page - 1) * pageSize, Take = pageSize, IdUsuario = idUsuario };
                using (var multi = await cn.QueryMultipleAsync(sql, args))
                {
                    var items = (await multi.ReadAsync<Proyecto>()).ToList();
                    var total = await multi.ReadFirstAsync<int>();
                    return (items, total);
                }
            }
        }

        public async Task<Proyecto> ObtenerAsync(int id)
        {
            using (var cn = Conn())
            {
                const string sql = @"
SELECT p.*, u.Nombres AS OwnerNombre
FROM dbo.Proyecto p
LEFT JOIN dbo.Seguridad_Usuario u ON u.IdUsuario = p.IdUsuarioOwner
WHERE p.IdProyecto = @id;";
                return await cn.QueryFirstOrDefaultAsync<Proyecto>(sql, new { id });
            }
        }

        public async Task<int> CrearAsync(Proyecto p)
        {
            using (var cn = Conn())
            {
                const string sql = @"
INSERT INTO dbo.Proyecto (Codigo, Nombre, Descripcion, Estado, FechaInicio, FechaFin, IdUsuarioOwner)
VALUES (@Codigo, @Nombre, @Descripcion, @Estado, @FechaInicio, @FechaFin, @IdUsuarioOwner);
SELECT CAST(SCOPE_IDENTITY() AS INT);";
                return await cn.ExecuteScalarAsync<int>(sql, p);
            }
        }

        public async Task<bool> EditarAsync(Proyecto p)
        {
            using (var cn = Conn())
            {
                const string sql = @"
UPDATE dbo.Proyecto SET
  Codigo=@Codigo, Nombre=@Nombre, Descripcion=@Descripcion, Estado=@Estado,
  FechaInicio=@FechaInicio, FechaFin=@FechaFin, IdUsuarioOwner=@IdUsuarioOwner
WHERE IdProyecto=@IdProyecto;";
                return (await cn.ExecuteAsync(sql, p)) > 0;
            }
        }

        public async Task<bool> CambiarEstadoAsync(int id, string estado)
        {
            using (var cn = Conn())
            {
                const string sql = "UPDATE dbo.Proyecto SET Estado=@estado WHERE IdProyecto=@id;";
                return (await cn.ExecuteAsync(sql, new { id, estado })) > 0;
            }
        }

        public async Task<bool> EliminarAsync(int id)
        {
            using (var cn = Conn())
            {
                const string sql = "DELETE FROM dbo.Proyecto WHERE IdProyecto=@id;";
                return (await cn.ExecuteAsync(sql, new { id })) > 0;
            }
        }

        // ===== Acceso / Usuarios =====

        public async Task<bool> UsuarioTieneAccesoProyectoAsync(int idProyecto, int idUsuario)
        {
            using (var cn = Conn())
            {
                const string sql = @"
SELECT CASE WHEN EXISTS (
  SELECT 1
  FROM dbo.Proyecto p
  LEFT JOIN dbo.ProyectoMiembro pm ON pm.IdProyecto = p.IdProyecto AND pm.IdUsuario = @idUsuario
  WHERE p.IdProyecto = @idProyecto
    AND (p.IdUsuarioOwner = @idUsuario OR pm.IdUsuario IS NOT NULL)
) THEN 1 ELSE 0 END;";
                return await cn.ExecuteScalarAsync<bool>(sql, new { idProyecto, idUsuario });
            }
        }

        public async Task<Usuario> ObtenerUsuarioPorIdAsync(int idUsuario)
        {
            using (var cn = Conn())
            {
                const string sql = @"SELECT IdUsuario, Correo, Nombres, Rol, Estado
FROM dbo.Seguridad_Usuario
WHERE IdUsuario = @idUsuario;";
                return await cn.QueryFirstOrDefaultAsync<Usuario>(sql, new { idUsuario });
            }
        }

        public async Task<bool> ActualizarRolSistemaAsync(int idUsuario, string rol)
        {
            using (var cn = Conn())
            {
                const string sql = @"UPDATE dbo.Seguridad_Usuario SET Rol=@rol WHERE IdUsuario=@idUsuario;";
                return (await cn.ExecuteAsync(sql, new { idUsuario, rol })) > 0;
            }
        }

        // ===== Miembros =====

        public async Task<IEnumerable<ProyectoMiembro>> ListarMiembrosAsync(int idProyecto)
        {
            using (var cn = Conn())
            {
                const string sql = @"
SELECT pm.IdProyecto, pm.IdUsuario, pm.RolMiembro, pm.FechaAltaUtc, pm.Estado,
       u.Nombres, u.Correo
FROM dbo.ProyectoMiembro pm
JOIN dbo.Seguridad_Usuario u ON u.IdUsuario = pm.IdUsuario
WHERE pm.IdProyecto = @idProyecto
ORDER BY u.Nombres;";
                return await cn.QueryAsync<ProyectoMiembro>(sql, new { idProyecto });
            }
        }

        public async Task<bool> AgregarMiembroAsync(int idProyecto, int idUsuario, string rolMiembro)
        {
            using (var cn = Conn())
            {
                const string sql = @"
IF NOT EXISTS(SELECT 1 FROM dbo.ProyectoMiembro WHERE IdProyecto=@idProyecto AND IdUsuario=@idUsuario)
BEGIN
  INSERT INTO dbo.ProyectoMiembro (IdProyecto, IdUsuario, RolMiembro) VALUES (@idProyecto, @idUsuario, @rolMiembro);
END";
                return (await cn.ExecuteAsync(sql, new { idProyecto, idUsuario, rolMiembro })) >= 0;
            }
        }

        public async Task<bool> QuitarMiembroAsync(int idProyecto, int idUsuario)
        {
            using (var cn = Conn())
            {
                const string sql = "DELETE FROM dbo.ProyectoMiembro WHERE IdProyecto=@idProyecto AND IdUsuario=@idUsuario;";
                return (await cn.ExecuteAsync(sql, new { idProyecto, idUsuario })) > 0;
            }
        }

        public async Task<IEnumerable<Usuario>> BuscarUsuariosAsync(string q, int top = 10)
        {
            q = (q ?? "").Trim();
            using (var cn = Conn())
            {
                const string sql = @"
SELECT TOP (@top) IdUsuario, Correo, Nombres, Rol, Estado
FROM dbo.Seguridad_Usuario
WHERE Estado=1 AND (Nombres LIKE @Q OR Correo LIKE @Q)
ORDER BY Nombres;";
                return await cn.QueryAsync<Usuario>(sql, new { top, Q = $"%{q}%" });
            }
        }

        // ===== Invitaciones =====

        public async Task<string> CrearInvitacionAsync(int idProyecto, string correoDestino, int idUsuarioInvita, TimeSpan? vigencia = null)
        {
            var token = Guid.NewGuid();
            var expira = DateTime.UtcNow.Add(vigencia ?? TimeSpan.FromDays(7));

            const string sql = @"
INSERT INTO dbo.ProyectoInvitacion (IdProyecto, CorreoDestino, Token, Estado, ExpiraUtc, IdUsuarioInvita)
VALUES (@IdProyecto, @CorreoDestino, @Token, N'Pendiente', @ExpiraUtc, @IdUsuarioInvita);
SELECT CAST(SCOPE_IDENTITY() AS INT);";

            using (var cn = Conn())
            {
                await cn.ExecuteScalarAsync<int>(sql, new
                {
                    IdProyecto = idProyecto,
                    CorreoDestino = correoDestino.Trim(),
                    Token = token,
                    ExpiraUtc = expira,
                    IdUsuarioInvita = idUsuarioInvita
                });
            }
            return token.ToString("N");
        }

        public async Task<ProyectoInvitacion> ObtenerInvitacionPorTokenAsync(string token)
        {
            if (!Guid.TryParseExact(token, "N", out var t) && !Guid.TryParse(token, out t))
                return null;

            const string sql = @"
SELECT TOP 1 *
FROM dbo.ProyectoInvitacion
WHERE Token = @Token;";

            using (var cn = Conn())
                return await cn.QueryFirstOrDefaultAsync<ProyectoInvitacion>(sql, new { Token = t });
        }

        public async Task<bool> MarcarInvitacionUsadaAsync(int idInvitacion)
        {
            const string sql = @"
UPDATE dbo.ProyectoInvitacion
SET Estado = N'Usada'
WHERE IdInvitacion = @IdInvitacion;";
            using (var cn = Conn())
                return (await cn.ExecuteAsync(sql, new { IdInvitacion = idInvitacion })) > 0;
        }

        // ===== Tareas (simples) =====

        public async Task<IEnumerable<ProyectoTarea>> ListarTareasAsync(int idProyecto)
        {
            using (var cn = Conn())
            {
                const string sql = @"
SELECT t.*, u.Nombres AsignadoNombre, u.Correo AsignadoCorreo
FROM dbo.ProyectoTarea t
LEFT JOIN dbo.Seguridad_Usuario u ON u.IdUsuario = t.AsignadoA
WHERE t.IdProyecto = @idProyecto
ORDER BY t.IdColumna, t.Orden, t.FechaCreacionUtc;";
                return await cn.QueryAsync<ProyectoTarea>(sql, new { idProyecto });
            }
        }

        public async Task<int> CrearTareaAsync(int idProyecto, string titulo, int? asignadoA, DateTime? fechaVenc)
        {
            using (var cn = Conn())
            {
                const string sql = @"
DECLARE @col INT = (SELECT TOP 1 IdColumna FROM dbo.ProyectoColumna WHERE IdProyecto=@IdProyecto ORDER BY Orden);
INSERT INTO dbo.ProyectoTarea (IdProyecto, Titulo, Hecho, AsignadoA, FechaVencimiento, Orden, IdColumna)
VALUES (
    @IdProyecto, @Titulo, 0, @AsignadoA, @FechaVencimiento,
    CASE
        WHEN @col IS NULL THEN ISNULL((SELECT MAX(Orden)+1 FROM dbo.ProyectoTarea WHERE IdProyecto=@IdProyecto),0)
        ELSE ISNULL((SELECT MAX(Orden)+1 FROM dbo.ProyectoTarea WHERE IdProyecto=@IdProyecto AND IdColumna=@col),0)
    END,
    @col
);
SELECT CAST(SCOPE_IDENTITY() AS INT);";
                return await cn.ExecuteScalarAsync<int>(sql, new
                {
                    IdProyecto = idProyecto,
                    Titulo = titulo.Trim(),
                    AsignadoA = asignadoA,
                    FechaVencimiento = fechaVenc
                });
            }
        }

        public async Task<bool> MarcarTareaAsync(int idTarea, bool hecho)
        {
            using (var cn = Conn())
                return (await cn.ExecuteAsync("UPDATE dbo.ProyectoTarea SET Hecho=@hecho WHERE IdTarea=@idTarea;", new { idTarea, hecho })) > 0;
        }

        public async Task<bool> EditarTareaCampoAsync(int idTarea, string campo, string valor)
        {
            string col = null; object v = valor;
            switch ((campo ?? "").Trim().ToLowerInvariant())
            {
                case "titulo":
                    col = "Titulo"; break;
                case "asignadoa":
                    col = "AsignadoA";
                    v = string.IsNullOrWhiteSpace(valor) ? (object)DBNull.Value : int.Parse(valor);
                    break;
                case "fechavencimiento":
                    col = "FechaVencimiento";
                    if (string.IsNullOrWhiteSpace(valor)) v = DBNull.Value;
                    else
                    {
                        if (!DateTime.TryParseExact(valor, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
                            return false;
                        v = dt;
                    }
                    break;
                default: return false;
            }
            using (var cn = Conn())
                return (await cn.ExecuteAsync($"UPDATE dbo.ProyectoTarea SET {col}=@v WHERE IdTarea=@id;", new { v, id = idTarea })) > 0;
        }

        public async Task<bool> EliminarTareaAsync(int idTarea)
        {
            using (var cn = Conn())
                return (await cn.ExecuteAsync("DELETE FROM dbo.ProyectoTarea WHERE IdTarea=@id;", new { id = idTarea })) > 0;
        }

        public async Task<bool> ReordenarTareasAsync(int idProyecto, IEnumerable<int> idsEnOrden)
        {
            using (var cn = Conn())
            {
                cn.Open();
                using (var tx = cn.BeginTransaction())
                {
                    int orden = 0; int afectados = 0;
                    foreach (var id in idsEnOrden)
                        afectados += await cn.ExecuteAsync(
                            "UPDATE dbo.ProyectoTarea SET Orden=@o WHERE IdTarea=@id AND IdProyecto=@p;",
                            new { o = orden++, id, p = idProyecto }, tx);
                    tx.Commit();
                    return afectados > 0;
                }
            }
        }

        // ===== Etiquetas =====

        public async Task<IEnumerable<Etiqueta>> ListarEtiquetasAsync(int idProyecto)
        {
            using (var cn = Conn())
            {
                const string sql = @"
SELECT e.IdEtiqueta, e.Nombre
FROM dbo.ProyectoEtiqueta pe
JOIN dbo.Etiqueta e ON e.IdEtiqueta = pe.IdEtiqueta
WHERE pe.IdProyecto = @idProyecto
ORDER BY e.Nombre;";
                return await cn.QueryAsync<Etiqueta>(sql, new { idProyecto });
            }
        }

        public async Task<Etiqueta> AgregarEtiquetaAsync(int idProyecto, string nombre)
        {
            nombre = (nombre ?? "").Trim();
            if (string.IsNullOrEmpty(nombre)) return null;

            using (var cn = Conn())
            {
                var idEtiqueta = await cn.ExecuteScalarAsync<int?>(@"
DECLARE @id INT = (SELECT IdEtiqueta FROM dbo.Etiqueta WHERE Nombre=@n);
IF @id IS NULL
BEGIN
  INSERT INTO dbo.Etiqueta(Nombre) VALUES(@n);
  SET @id = SCOPE_IDENTITY();
END
INSERT INTO dbo.ProyectoEtiqueta(IdProyecto, IdEtiqueta)
SELECT @p, @id
WHERE NOT EXISTS(SELECT 1 FROM dbo.ProyectoEtiqueta WHERE IdProyecto=@p AND IdEtiqueta=@id);
SELECT @id;", new { n = nombre, p = idProyecto });

                if (idEtiqueta == null) return null;
                return await cn.QueryFirstOrDefaultAsync<Etiqueta>("SELECT IdEtiqueta, Nombre FROM dbo.Etiqueta WHERE IdEtiqueta=@id;", new { id = idEtiqueta });
            }
        }

        public async Task<bool> QuitarEtiquetaAsync(int idProyecto, int idEtiqueta)
        {
            using (var cn = Conn())
                return (await cn.ExecuteAsync("DELETE FROM dbo.ProyectoEtiqueta WHERE IdProyecto=@p AND IdEtiqueta=@e;",
                                              new { p = idProyecto, e = idEtiqueta })) > 0;
        }

        // ===== Notas =====

        public async Task<ProyectoNota> ObtenerNotaAsync(int idProyecto)
        {
            using (var cn = Conn())
                return await cn.QueryFirstOrDefaultAsync<ProyectoNota>("SELECT TOP 1 * FROM dbo.ProyectoNota WHERE IdProyecto=@p;", new { p = idProyecto });
        }

        public async Task<bool> GuardarNotaAsync(int idProyecto, string contenido)
        {
            using (var cn = Conn())
            {
                const string sql = @"
IF EXISTS(SELECT 1 FROM dbo.ProyectoNota WHERE IdProyecto=@p)
  UPDATE dbo.ProyectoNota SET Contenido=@c, FechaActualizacionUtc=SYSUTCDATETIME() WHERE IdProyecto=@p;
ELSE
  INSERT INTO dbo.ProyectoNota (IdProyecto, Contenido) VALUES (@p, @c);";
                return (await cn.ExecuteAsync(sql, new { p = idProyecto, c = (contenido ?? "") })) >= 1;
            }
        }

        // ===== Metodología / Tablero =====

        private IEnumerable<(string clave, string titulo)> PlantillaColumnas(string metodologia)
        {
            metodologia = (metodologia ?? "").ToUpperInvariant();
            if (metodologia == "RUP")
                return new[] { ("inception", "Inicio"), ("elaboration", "Elaboración"), ("construction", "Construcción"), ("transition", "Transición") };
            if (metodologia == "KANBAN")
                return new[] { ("todo", "Por hacer"), ("doing", "En proceso"), ("blocked", "Bloqueado"), ("done", "Hecho") };
            // SCRUM por defecto
            return new[] { ("backlog", "Backlog"), ("sprint", "Sprint"), ("doing", "En curso"), ("review", "En revisión"), ("done", "Hecho") };
        }

        public async Task<bool> EstablecerMetodologiaAsync(int idProyecto, string metodologia, bool regenerar)
        {
            using (var cn = Conn())
            {
                await cn.OpenAsync();
                using (var tx = cn.BeginTransaction())
                {
                    // set metodologia
                    await cn.ExecuteAsync("UPDATE dbo.Proyecto SET Metodologia=@m WHERE IdProyecto=@p;",
                                          new { m = metodologia?.ToUpperInvariant(), p = idProyecto }, tx);

                    // columnas existentes
                    var cols = (await cn.QueryAsync<ProyectoColumna>(
                        "SELECT * FROM dbo.ProyectoColumna WHERE IdProyecto=@p ORDER BY Orden;", new { p = idProyecto }, tx)).ToList();

                    if (!cols.Any() || regenerar)
                    {
                        // borrar columnas y dejar tareas sin columna
                        if (cols.Any())
                        {
                            await cn.ExecuteAsync("UPDATE dbo.ProyectoTarea SET IdColumna=NULL WHERE IdProyecto=@p;", new { p = idProyecto }, tx);
                            await cn.ExecuteAsync("DELETE FROM dbo.ProyectoColumna WHERE IdProyecto=@p;", new { p = idProyecto }, tx);
                        }

                        int orden = 0;
                        foreach (var c in PlantillaColumnas(metodologia))
                        {
                            await cn.ExecuteAsync(@"
INSERT INTO dbo.ProyectoColumna (IdProyecto, Titulo, Clave, Orden)
VALUES (@p, @t, @k, @o);", new { p = idProyecto, t = c.titulo, k = c.clave, o = orden++ }, tx);
                        }

                        var idPrimeraCol = await cn.ExecuteScalarAsync<int>(
                            "SELECT TOP 1 IdColumna FROM dbo.ProyectoColumna WHERE IdProyecto=@p ORDER BY Orden;", new { p = idProyecto }, tx);

                        // asignar tareas existentes a la primera columna
                        await cn.ExecuteAsync("UPDATE dbo.ProyectoTarea SET IdColumna=@c WHERE IdProyecto=@p;", new { c = idPrimeraCol, p = idProyecto }, tx);

                        // reorden por creación dentro de esa columna
                        const string upOrden = @"
;WITH x AS (
  SELECT IdTarea, ROW_NUMBER() OVER(ORDER BY FechaCreacionUtc, IdTarea) AS rn
  FROM dbo.ProyectoTarea
  WHERE IdProyecto=@p AND IdColumna=@c
)
UPDATE t SET Orden = x.rn
FROM dbo.ProyectoTarea t
JOIN x ON x.IdTarea = t.IdTarea;";
                        await cn.ExecuteAsync(upOrden, new { p = idProyecto, c = idPrimeraCol }, tx);
                    }

                    tx.Commit();
                    return true;
                }
            }
        }

        public async Task<IEnumerable<ProyectoColumna>> ListarColumnasAsync(int idProyecto)
        {
            using (var cn = Conn())
                return await cn.QueryAsync<ProyectoColumna>("SELECT * FROM dbo.ProyectoColumna WHERE IdProyecto=@p ORDER BY Orden;", new { p = idProyecto });
        }

        public async Task<int> CrearColumnaAsync(int idProyecto, string titulo, string clave = null)
        {
            using (var cn = Conn())
                return await cn.ExecuteScalarAsync<int>(@"
INSERT INTO dbo.ProyectoColumna (IdProyecto, Titulo, Clave, Orden)
VALUES (@p, @t, @k, ISNULL((SELECT MAX(Orden)+1 FROM dbo.ProyectoColumna WHERE IdProyecto=@p),0));
SELECT CAST(SCOPE_IDENTITY() AS INT);", new { p = idProyecto, t = titulo.Trim(), k = (clave ?? (object)DBNull.Value) });
        }

        public async Task<bool> RenombrarColumnaAsync(int idColumna, string titulo)
        {
            using (var cn = new SqlConnection(_cnx))
            {
                var sql = @"UPDATE dbo.ProyectoColumna SET Titulo = @titulo WHERE IdColumna = @idColumna;";
                var rows = await cn.ExecuteAsync(sql, new { idColumna, titulo });
                return rows > 0;
            }
        }

        public async Task<bool> ReordenarColumnasAsync(int idProyecto, IEnumerable<int> idsEnOrden)
        {
            using (var cn = Conn())
            {
                cn.Open();
                using (var tx = cn.BeginTransaction())
                {
                    int o = 0, afectados = 0;
                    foreach (var id in idsEnOrden)
                        afectados += await cn.ExecuteAsync("UPDATE dbo.ProyectoColumna SET Orden=@o WHERE IdColumna=@id AND IdProyecto=@p;",
                                                           new { o, id, p = idProyecto }, tx);
                    tx.Commit();
                    return afectados > 0;
                }
            }
        }

        public async Task<bool> MoverTareaAColumnaAsync(int idTarea, int idColumnaDestino)
        {
            using (var cn = Conn())
                return (await cn.ExecuteAsync(@"
UPDATE dbo.ProyectoTarea
SET IdColumna=@c,
    Orden = ISNULL((SELECT MAX(Orden)+1 FROM dbo.ProyectoTarea WHERE IdColumna=@c),0)
WHERE IdTarea=@t;", new { c = idColumnaDestino, t = idTarea })) > 0;
        }

        public async Task<bool> ReordenarTareasEnColumnaAsync(int idColumna, IEnumerable<int> idsEnOrden)
        {
            using (var cn = Conn())
            {
                cn.Open();
                using (var tx = cn.BeginTransaction())
                {
                    int o = 0, afectados = 0;
                    foreach (var id in idsEnOrden)
                        afectados += await cn.ExecuteAsync("UPDATE dbo.ProyectoTarea SET Orden=@o WHERE IdTarea=@id AND IdColumna=@c;",
                                                           new { o, id, c = idColumna }, tx);
                    tx.Commit();
                    return afectados > 0;
                }
            }
        }

        public async Task<object> ObtenerTableroAsync(int idProyecto)
        {
            using (var cn = Conn())
            {
                var p = await cn.QueryFirstOrDefaultAsync<Proyecto>("SELECT * FROM dbo.Proyecto WHERE IdProyecto=@p;", new { p = idProyecto });
                var cols = (await cn.QueryAsync<ProyectoColumna>("SELECT * FROM dbo.ProyectoColumna WHERE IdProyecto=@p ORDER BY Orden;", new { p = idProyecto })).ToList();
                var tareas = (await cn.QueryAsync<ProyectoTarea>(@"
SELECT t.*, u.Nombres AsignadoNombre, u.Correo AsignadoCorreo
FROM dbo.ProyectoTarea t
LEFT JOIN dbo.Seguridad_Usuario u ON u.IdUsuario=t.AsignadoA
WHERE t.IdProyecto=@p
ORDER BY t.IdColumna, t.Orden, t.FechaCreacionUtc;", new { p = idProyecto })).ToList();

                var columnas = cols.Select(c => new
                {
                    c.IdColumna,
                    c.Titulo,
                    c.Clave,
                    c.Orden,
                    // Si algún día decides guardar ColorHex en DB, puedes exponerlo también:
                    //c.ColorHex,
                    tareas = tareas.Where(t => t.IdColumna == c.IdColumna).Select(t => new
                    {
                        t.IdTarea,
                        t.Titulo,
                        t.Hecho,
                        t.AsignadoA,
                        t.AsignadoNombre,
                        t.AsignadoCorreo,
                        FechaVencimiento = t.FechaVencimiento?.ToString("yyyy-MM-dd"),
                        t.Orden
                    })
                });

                return new { metodologia = p?.Metodologia, columnas };
            }
        }
    }
}
