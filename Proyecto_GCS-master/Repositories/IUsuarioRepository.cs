using Dapper;
using Proyecto_GCS.Models;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using System.Configuration;
using System.Web.Configuration;

namespace Proyecto_GCS.Repositories
{
    public interface IUsuarioRepository
    {
        Task<Usuario> ObtenerPorCorreoAsync(string correo);
        Task<Usuario> ValidarLoginAsync(string correo, string passwordPlano);
        Task<int> RegistrarAsync(string correo, string passwordPlano, string nombres, string rol = "User");

    }

    public class UsuarioRepository : IUsuarioRepository
    {
        private readonly string _cnx;

        public UsuarioRepository()
        {
            _cnx = WebConfigurationManager.ConnectionStrings["ConexionGCS"].ConnectionString;
        }

        public async Task<Usuario> ObtenerPorCorreoAsync(string correo)
        {
            const string sql = @"
                                SELECT TOP 1 IdUsuario, Correo, PasswordHash, Nombres, Rol, Estado, UltimoCambioPasswordUtc
                                FROM Seguridad_Usuario WITH (NOLOCK)
                                WHERE Correo = @Correo";
            using (var cn = new SqlConnection(_cnx))
            {
                var lista = await cn.QueryAsync<Usuario>(sql, new { Correo = correo });
                return lista.FirstOrDefault();
            }
        }

        public async Task<Usuario> ValidarLoginAsync(string correo, string passwordPlano)
        {
            var usuario = await ObtenerPorCorreoAsync(correo);
            if (usuario == null || !usuario.Estado) return null;
            return EncriptacionPassword.Verificar(passwordPlano, usuario.PasswordHash) ? usuario : null;
        }

        public async Task<int> RegistrarAsync(string correo, string passwordPlano, string nombres, string rol = "User")
        {
            var hash = EncriptacionPassword.Hashear(passwordPlano);

            const string sql = @"
                                INSERT INTO Seguridad_Usuario (Correo, PasswordHash, Nombres, Rol, Estado)
                                VALUES (@Correo, @PasswordHash, @Nombres, @Rol, 1);
                                SELECT CAST(SCOPE_IDENTITY() as int);";

            using (var cn = new SqlConnection(_cnx))
            {
                return await cn.ExecuteScalarAsync<int>(sql, new
                {
                    Correo = correo,
                    PasswordHash = hash,
                    Nombres = nombres,
                    Rol = rol
                });
            }
        }

    }
}
