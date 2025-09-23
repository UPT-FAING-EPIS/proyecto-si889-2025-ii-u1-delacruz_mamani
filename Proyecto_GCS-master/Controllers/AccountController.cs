using Microsoft.Owin.Security;
using Proyecto_GCS.Repositories;
using Proyecto_GCS.ViewModels;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;

namespace Proyecto_GCS.Controllers
{
    public class AccountController : Controller
    {
        private readonly IUsuarioRepository _repo = new UsuarioRepository();

        [AllowAnonymous]
        public ActionResult Login(string returnUrl)
        {
            return View(new LoginViewModel { ReturnUrl = returnUrl });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AllowAnonymous]
        public async Task<ActionResult> Login(LoginViewModel vm)
        {
            if (!ModelState.IsValid) return View(vm);

            var user = await _repo.ValidarLoginAsync(vm.Correo, vm.Password);
            if (user == null)
            {
                ViewBag.Error = "Correo o contraseña incorrectos.";
                return View(vm);
            }

            var identity = new ClaimsIdentity("ApplicationCookie");
            identity.AddClaim(new Claim(ClaimTypes.Name, user.Nombres ?? user.Correo));
            identity.AddClaim(new Claim(ClaimTypes.Email, user.Correo));
            identity.AddClaim(new Claim(ClaimTypes.Role, user.Rol ?? "User"));
            identity.AddClaim(new Claim("IdUsuario", user.IdUsuario.ToString()));

            var auth = HttpContext.GetOwinContext().Authentication;
            auth.SignIn(new AuthenticationProperties { IsPersistent = true }, identity);

            if (!string.IsNullOrWhiteSpace(vm.ReturnUrl) && Url.IsLocalUrl(vm.ReturnUrl))
                return Redirect(vm.ReturnUrl);

            return RedirectToAction("Index", "Home");
        }

        [Authorize]
        public ActionResult Logout()
        {
            HttpContext.GetOwinContext().Authentication.SignOut("ApplicationCookie");
            return RedirectToAction("Login");
        }

        // GET: /Account/Register
        [AllowAnonymous]
        public ActionResult Register()
        {
            return View(new RegistroViewModel());
        }

        // POST: /Account/Register
        [HttpPost]
        [ValidateAntiForgeryToken]
        [AllowAnonymous]
        public async Task<ActionResult> Register(RegistroViewModel vm)
        {
            if (!ModelState.IsValid)
                return View(vm);

            // validar que no exista
            var existente = await _repo.ObtenerPorCorreoAsync(vm.Correo);
            if (existente != null)
            {
                ViewBag.Error = "Ya existe un usuario con ese correo.";
                return View(vm);
            }

            await _repo.RegistrarAsync(vm.Correo, vm.Password, vm.Nombres);

            TempData["Msg"] = "Registro exitoso, ahora puedes iniciar sesión.";
            return RedirectToAction("Login", "Account");
        }
    }
}
