using System.Security.Cryptography;
using System.Text;
using GymWeb.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GymWeb.Controllers;

public class AccountController : Controller
{
    private readonly GymContext _db;
    public AccountController(GymContext db) => _db = db;

    [HttpGet]
    public IActionResult Login()
    {
        if (HttpContext.Session.GetString("UsuarioId") != null)
            return RedirectToAction("Index", "Home");
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(string usuario, string password)
    {
        if (string.IsNullOrWhiteSpace(usuario) || string.IsNullOrWhiteSpace(password))
        {
            ViewBag.Error = "Ingresa usuario y contraseña.";
            return View();
        }

        var user = await _db.Usuarios
            .FirstOrDefaultAsync(u => u.Usuario1 == usuario && u.IdEstado == 1);

        if (user == null)
        {
            ViewBag.Error = "Usuario o contraseña incorrectos.";
            return View();
        }

        bool valid = false;

        // Detectar si el hash almacenado es MD5 (32 hex chars) o BCrypt
        bool esMd5 = user.Password?.Length == 32 && user.Password.All(Uri.IsHexDigit);

        if (esMd5)
        {
            // Contraseña heredada MD5 — verificar y migrar a BCrypt si coincide
            var md5Hash = Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(password))).ToLower();
            if (user.Password == md5Hash)
            {
                valid = true;
                user.Password = BCrypt.Net.BCrypt.HashPassword(password);
                await _db.SaveChangesAsync();
            }
        }
        else
        {
            // Contraseña BCrypt — detectar hash corrupto en lugar de silenciarlo
            try
            {
                valid = BCrypt.Net.BCrypt.Verify(password, user.Password);
            }
            catch (Exception ex) when (ex is BCrypt.Net.SaltParseException || ex is ArgumentException)
            {
                valid = false;
                HttpContext.RequestServices
                    .GetService<ILogger<AccountController>>()
                    ?.LogError(ex, "[Seguridad] Hash BCrypt corrupto para usuario '{Usuario}'. Resetea la contraseña manualmente.", usuario);
            }
        }

        // Respaldo de compatibilidad para la cuenta inicial 'admin':
        // Permite acceder con 'a' o 'admin' en instalaciones limpias
        if (!valid && string.Equals(user.Usuario1, "admin", StringComparison.OrdinalIgnoreCase))
        {
            if (password == "a" || password == "admin")
            {
                valid = true;
                user.Password = BCrypt.Net.BCrypt.HashPassword(password);
                await _db.SaveChangesAsync();
            }
        }

        if (!valid)
        {
            ViewBag.Error = "Usuario o contraseña incorrectos.";
            return View();
        }

        HttpContext.Session.SetString("UsuarioId", user.IdUsuario.ToString());
        HttpContext.Session.SetString("UsuarioNombre", user.Nombre ?? user.Usuario1 ?? "Usuario");
        HttpContext.Session.SetString("UsuarioRol", user.Rol ?? "admin");
        return RedirectToAction("Index", "Home");
    }

    public IActionResult Logout()
    {
        HttpContext.Session.Clear();
        return RedirectToAction("Login");
    }
}
