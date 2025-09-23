using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Proyecto_GCS.Models
{
    public class ProyectoMiembro
    {
        public int IdProyecto { get; set; }
        public int IdUsuario { get; set; }
        public string RolMiembro { get; set; }
        public DateTime FechaAltaUtc { get; set; }

        // para la vista:
        public string Nombres { get; set; }
        public string Correo { get; set; }
        public bool Estado { get; set; }
    }
}