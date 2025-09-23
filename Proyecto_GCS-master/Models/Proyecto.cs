using System;

namespace Proyecto_GCS.Models
{
    public class Proyecto
    {
        public int IdProyecto { get; set; }
        public string Codigo { get; set; }
        public string Nombre { get; set; }
        public string Descripcion { get; set; }
        public string Estado { get; set; } // Activo | Pausado | Cerrado
        public DateTime? FechaInicio { get; set; }
        public DateTime? FechaFin { get; set; }
        public int? IdUsuarioOwner { get; set; }
        public DateTime FechaCreacionUtc { get; set; }
        public string OwnerNombre { get; set; } // join
        public string Metodologia { get; set; }
    }
}