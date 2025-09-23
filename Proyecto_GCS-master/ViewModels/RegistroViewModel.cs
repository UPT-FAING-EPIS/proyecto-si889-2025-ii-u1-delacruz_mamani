using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace Proyecto_GCS.ViewModels
{
    public class RegistroViewModel
    {
        [Required, EmailAddress]
        public string Correo { get; set; }

        [Required, StringLength(100, MinimumLength = 6)]
        [DataType(DataType.Password)]
        public string Password { get; set; }

        [Required, Compare("Password", ErrorMessage = "Las contraseñas no coinciden.")]
        [DataType(DataType.Password)]
        public string ConfirmarPassword { get; set; }

        [Required]
        public string Nombres { get; set; }
    }
}