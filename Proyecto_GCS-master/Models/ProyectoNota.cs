using System;

namespace Proyecto_GCS.Models
{
    public class ProyectoNota
    {
        public int IdNota { get; set; }
        public int IdProyecto { get; set; }
        public string Contenido { get; set; }
        public DateTime FechaActualizacionUtc { get; set; }
    }
}
