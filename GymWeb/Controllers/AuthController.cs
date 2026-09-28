using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace GymWeb.Controllers;

public abstract class AuthController : Controller
{
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        if (HttpContext.Session.GetString("UsuarioId") == null)
        {
            context.Result = RedirectToAction("Login", "Account");
            return;
        }
        base.OnActionExecuting(context);
    }

    protected int UsuarioId => int.Parse(HttpContext.Session.GetString("UsuarioId")!);
    protected string UsuarioRol => HttpContext.Session.GetString("UsuarioRol") ?? "admin";
    protected bool IsSuperAdmin => UsuarioRol == "superadmin";
}
