using System;

namespace Proyecto_GCS.Models
{
    public class ProyectoInvitacion
    {
        public int IdInvitacion { get; set; }
        public int IdProyecto { get; set; }
        public string CorreoDestino { get; set; }
        public Guid Token { get; set; }
        public string Estado { get; set; }       // Pendiente | Usada | Cancelada | Expirada
        public DateTime ExpiraUtc { get; set; }
        public DateTime FechaEnvioUtc { get; set; }
        public int IdUsuarioInvita { get; set; }
    }
}
