using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Proyecto_GCS.Models;

namespace Proyecto_GCS.Repositories
{
    public interface IProyectoRepository
    {
        // Proyectos
        Task<int> CrearRapidoAsync(string codigo, string nombre, int idOwner);
        Task<bool> EditarCampoAsync(int id, string campo, string valor);
        Task<(IEnumerable<Proyecto> items, int total)> ListarAsync(string q, int page, int pageSize, int idUsuario, bool verTodos = false);
        Task<Proyecto> ObtenerAsync(int id);
        Task<int> CrearAsync(Proyecto p);
        Task<bool> EditarAsync(Proyecto p);
        Task<bool> CambiarEstadoAsync(int id, string estado);
        Task<bool> EliminarAsync(int id);

        // Acceso / usuarios
        Task<bool> UsuarioTieneAccesoProyectoAsync(int idProyecto, int idUsuario);
        Task<Usuario> ObtenerUsuarioPorIdAsync(int idUsuario);
        Task<bool> ActualizarRolSistemaAsync(int idUsuario, string rol);

        // Miembros
        Task<IEnumerable<ProyectoMiembro>> ListarMiembrosAsync(int idProyecto);
        Task<bool> AgregarMiembroAsync(int idProyecto, int idUsuario, string rolMiembro);
        Task<bool> QuitarMiembroAsync(int idProyecto, int idUsuario);
        Task<IEnumerable<Usuario>> BuscarUsuariosAsync(string q, int top = 10);

        // Invitaciones
        Task<string> CrearInvitacionAsync(int idProyecto, string correoDestino, int idUsuarioInvita, TimeSpan? vigencia = null);
        Task<ProyectoInvitacion> ObtenerInvitacionPorTokenAsync(string token);
        Task<bool> MarcarInvitacionUsadaAsync(int idInvitacion);

        // Tareas (simples)
        Task<IEnumerable<ProyectoTarea>> ListarTareasAsync(int idProyecto);
        Task<int> CrearTareaAsync(int idProyecto, string titulo, int? asignadoA, DateTime? fechaVenc);
        Task<bool> MarcarTareaAsync(int idTarea, bool hecho);
        Task<bool> EditarTareaCampoAsync(int idTarea, string campo, string valor);
        Task<bool> EliminarTareaAsync(int idTarea);
        Task<bool> ReordenarTareasAsync(int idProyecto, IEnumerable<int> idsEnOrden);

        // Etiquetas
        Task<IEnumerable<Etiqueta>> ListarEtiquetasAsync(int idProyecto);
        Task<Etiqueta> AgregarEtiquetaAsync(int idProyecto, string nombre);
        Task<bool> QuitarEtiquetaAsync(int idProyecto, int idEtiqueta);

        // Notas
        Task<ProyectoNota> ObtenerNotaAsync(int idProyecto);
        Task<bool> GuardarNotaAsync(int idProyecto, string contenido);

        // Metodología / Tablero (columnas)
        Task<bool> EstablecerMetodologiaAsync(int idProyecto, string metodologia, bool regenerar);
        Task<IEnumerable<ProyectoColumna>> ListarColumnasAsync(int idProyecto);
        Task<int> CrearColumnaAsync(int idProyecto, string titulo, string clave = null);
        Task<bool> RenombrarColumnaAsync(int idColumna, string titulo);
        Task<bool> ReordenarColumnasAsync(int idProyecto, IEnumerable<int> idsEnOrden);
        Task<bool> MoverTareaAColumnaAsync(int idTarea, int idColumnaDestino);
        Task<bool> ReordenarTareasEnColumnaAsync(int idColumna, IEnumerable<int> idsEnOrden);
        Task<object> ObtenerTableroAsync(int idProyecto);
    }
}
