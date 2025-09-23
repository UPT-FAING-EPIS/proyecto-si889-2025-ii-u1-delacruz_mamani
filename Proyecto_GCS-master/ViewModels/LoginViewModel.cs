using System.ComponentModel.DataAnnotations;

namespace Proyecto_GCS.ViewModels
{
    public class LoginViewModel
    {
        [Required, EmailAddress]
        public string Correo { get; set; }

        [Required, DataType(DataType.Password)]
        public string Password { get; set; }

        public string ReturnUrl { get; set; }
    }
}