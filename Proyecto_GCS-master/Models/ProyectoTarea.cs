using System;
using System.Collections.Generic;

namespace Proyecto_GCS.Models
{
    public class ProyectoTarea
    {
        public int IdTarea { get; set; }
        public int IdProyecto { get; set; }
        public string Titulo { get; set; }
        public bool Hecho { get; set; }
        public int? AsignadoA { get; set; }
        public DateTime? FechaVencimiento { get; set; }
        public int Orden { get; set; }
        public DateTime FechaCreacionUtc { get; set; }

        public int IdColumna { get; set; }  // para el tablero

        //para UI 
        public string AsignadoNombre { get; set; }
        public string AsignadoCorreo { get; set; }
    }
}
