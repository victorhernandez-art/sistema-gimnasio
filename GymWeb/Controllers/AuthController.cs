using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace GymWeb.Controllers;

public abstract class AuthController : Controller
{
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        var hasAllowAnonymous = context.ActionDescriptor.EndpointMetadata
            .Any(em => em is Microsoft.AspNetCore.Authorization.AllowAnonymousAttribute);
        if (hasAllowAnonymous)
        {
            base.OnActionExecuting(context);
            return;
        }

        if (HttpContext.Session.GetString("UsuarioId") == null)
        {
            context.Result = RedirectToAction("Login", "Account");
            return;
        }
        base.OnActionExecuting(context);
    }

    protected int UsuarioId => int.TryParse(HttpContext.Session.GetString("UsuarioId"), out var id) ? id : 1;
    protected string UsuarioRol => HttpContext.Session.GetString("UsuarioRol") ?? "admin";
    protected bool IsSuperAdmin => UsuarioRol == "superadmin";
}
