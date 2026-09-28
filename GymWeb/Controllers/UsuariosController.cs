using GymWeb.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GymWeb.Controllers;

public class UsuariosController : SuperAdminController
{
    private readonly GymContext _db;
    public UsuariosController(GymContext db) => _db = db;

    public async Task<IActionResult> Index()
    {
        ViewData["Title"] = "Usuarios";
        var list = await _db.Usuarios.Include(u => u.IdEstadoNavigation).OrderBy(u => u.Nombre).ToListAsync();
        return View(list);
    }

    [HttpGet]
    public IActionResult Create()
    {
        ViewData["Title"] = "Nuevo Usuario";
        return View(new Usuario());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Usuario u, string passwordConfirm)
    {
        if (string.IsNullOrWhiteSpace(u.Password) || u.Password.Length < 8)
            ModelState.AddModelError("", "La contraseña debe tener al menos 8 caracteres.");
        if (u.Password != passwordConfirm)
            ModelState.AddModelError("", "Las contraseñas no coinciden.");
        if (!ModelState.IsValid) { ViewData["Title"] = "Nuevo Usuario"; return View(u); }

        var exists = await _db.Usuarios.AnyAsync(x => x.Usuario1 == u.Usuario1);
        if (exists)
        {
            ModelState.AddModelError("", "El nombre de usuario ya existe.");
            ViewData["Title"] = "Nuevo Usuario";
            return View(u);
        }

        u.FechaCreacion = DateTime.Now;
        u.IdEstado = 1;
        if (string.IsNullOrWhiteSpace(u.Rol)) u.Rol = "admin";
        u.Password = BCrypt.Net.BCrypt.HashPassword(u.Password!);
        _db.Usuarios.Add(u);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Usuario creado.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        ViewData["Title"] = "Editar Usuario";
        var u = await _db.Usuarios.FindAsync(id);
        if (u == null) return NotFound();
        return View(u);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, string nombre, string? newPassword, string? passwordConfirm, string? rol)
    {
        var u = await _db.Usuarios.FindAsync(id);
        if (u == null) return NotFound();
        u.Nombre = nombre;
        if (!string.IsNullOrWhiteSpace(newPassword))
        {
            if (newPassword.Length < 8)
            {
                TempData["Error"] = "La contraseña debe tener al menos 8 caracteres.";
                return RedirectToAction(nameof(Edit), new { id });
            }
            if (newPassword != passwordConfirm)
            {
                TempData["Error"] = "Las contraseñas no coinciden.";
                return RedirectToAction(nameof(Edit), new { id });
            }
            u.Password = BCrypt.Net.BCrypt.HashPassword(newPassword);
        }
        // No permitir cambiar el propio rol
        if (id != UsuarioId && !string.IsNullOrWhiteSpace(rol))
            u.Rol = rol;
        await _db.SaveChangesAsync();
        TempData["Success"] = "Usuario actualizado.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Activate(int id)
    {
        var u = await _db.Usuarios.FindAsync(id);
        if (u != null) { u.IdEstado = 1; await _db.SaveChangesAsync(); }
        TempData["Success"] = "Usuario activado correctamente.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var u = await _db.Usuarios.FindAsync(id);
        if (u != null) { u.IdEstado = 2; await _db.SaveChangesAsync(); }
        TempData["Success"] = "Usuario desactivado.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Eliminar(int id)
    {
        if (id == UsuarioId)
        {
            TempData["Error"] = "No puedes eliminarte a ti mismo.";
            return RedirectToAction(nameof(Index));
        }
        var u = await _db.Usuarios.FindAsync(id);
        if (u == null) return NotFound();
        try
        {
            _db.Usuarios.Remove(u);
            await _db.SaveChangesAsync();
            TempData["Success"] = $"Usuario '{u.Usuario1}' eliminado permanentemente.";
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateException)
        {
            TempData["Error"] = "No se puede eliminar porque tiene registros asociados en el sistema. Puedes desactivarlo en su lugar.";
        }
        return RedirectToAction(nameof(Index));
    }
}
