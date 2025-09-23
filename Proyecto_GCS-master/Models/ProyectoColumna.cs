namespace Proyecto_GCS.Models
{
    public class ProyectoColumna
    {
        public int IdColumna { get; set; }
        public int IdProyecto { get; set; }
        public string Titulo { get; set; }
        public string Clave { get; set; } // backlog, doing, done, etc
        public int Orden { get; set; }
    }
}
