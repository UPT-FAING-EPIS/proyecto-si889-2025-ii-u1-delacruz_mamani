namespace Proyecto_GCS.Models
{
    public class Usuario
    {
        public int IdUsuario { get; set; }
        public string Correo { get; set; }
        public string PasswordHash { get; set; }
        public string Nombres { get; set; }
        public string Rol { get; set; }
        public bool Estado { get; set; }
        public string UltimoCambioPasswordUtc { get; set; }
    }
}