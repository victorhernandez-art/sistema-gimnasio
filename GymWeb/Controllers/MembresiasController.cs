using GymWeb.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GymWeb.Controllers;

public class MembresiasController : AuthController
{
    private readonly GymContext _db;
    public MembresiasController(GymContext db) => _db = db;

    public async Task<IActionResult> Index()
    {
        ViewData["Title"] = "Membresías";
        var list = await _db.Membresia.Include(m => m.IdEstadoNavigation)
            .OrderByDescending(m => m.FechaCreacion).ToListAsync();
        return View(list);
    }

    [HttpGet]
    public IActionResult Create()
    {
        ViewData["Title"] = "Nueva Membresía";
        return View(new Membresium());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Membresium m)
    {
        if (!ModelState.IsValid) { ViewData["Title"] = "Nueva Membresía"; return View(m); }
        m.FechaCreacion = DateTime.Now;
        m.IdUsuarioCreo = UsuarioId;
        m.IdEstado = 1;
        _db.Membresia.Add(m);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Membresía creada.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        ViewData["Title"] = "Editar Membresía";
        var m = await _db.Membresia.FindAsync(id);
        if (m == null) return NotFound();
        return View(m);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Membresium m)
    {
        if (!ModelState.IsValid) { ViewData["Title"] = "Editar Membresía"; return View(m); }
        var existing = await _db.Membresia.FindAsync(m.IdMembresia);
        if (existing == null) return NotFound();
        existing.Nombre = m.Nombre;
        existing.Precio = m.Precio;
        existing.Meses = m.Meses;
        existing.HoraInicio = m.HoraInicio;
        existing.HoraFinal = m.HoraFinal;
        await _db.SaveChangesAsync();
        TempData["Success"] = "Membresía actualizada.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var m = await _db.Membresia.FindAsync(id);
        if (m != null) { m.IdEstado = 2; await _db.SaveChangesAsync(); }
        TempData["Success"] = "Membresía eliminada.";
        return RedirectToAction(nameof(Index));
    }
}
