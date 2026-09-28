using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace GymWeb.Controllers;

public abstract class SuperAdminController : AuthController
{
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        base.OnActionExecuting(context);
        if (context.Result != null) return; // ya redirigió por AuthController

        if (HttpContext.Session.GetString("UsuarioRol") != "superadmin")
        {
            // Si la licencia está expirada o inactiva, permitir acceso a Configuración para ingresar la clave
            var lic = Helpers.LicenseService.GetStatus();
            if (!lic.Valid)
            {
                return;
            }

            context.Result = RedirectToAction("Index", "Home");
        }
    }
}
