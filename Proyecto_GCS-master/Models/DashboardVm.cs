using System;
using System.Collections.Generic;

namespace Proyecto_GCS.Models
{
    public class DashboardVm
    {
        public string UserName { get; set; }

        // Proyectos
        public int ProyectosTotal { get; set; }
        public int ProyActivos { get; set; }
        public int ProyPausados { get; set; }
        public int ProyCerrados { get; set; }

        // Cambios pendientes (mapeados a tareas Pendiente/Bloqueado)
        public int CambiosPendientes { get; set; }
        public int CambiosProyectos { get; set; }

        // Issues abiertos (mapeados a tareas no hechas)
        public int IssuesAbiertos { get; set; }
        public int IssuesCriticos { get; set; }  // Bloqueado
        public int IssuesMayores { get; set; }   // En proceso
        public int IssuesMenores { get; set; }   // Pendiente

        // "Deploys hoy" (mapeado a tareas hechas con FechaFin=HOY)
        public int DeploysHoy { get; set; }
        public string UltimoDeployHace { get; set; }

        // Actividad reciente (últimas tareas)
        public List<ActividadDto> ActividadReciente { get; set; } = new List<ActividadDto>();
    }

    public class ActividadDto
    {
        public int IdTarea { get; set; }
        public string Titulo { get; set; }
        public string Estado { get; set; }
        public bool Hecho { get; set; }
        public DateTime FechaCreacionUtc { get; set; }
        public int IdProyecto { get; set; }
        public string ProyectoNombre { get; set; }
    }
}
