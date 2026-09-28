using System;
using System.Collections.Generic;

namespace GymWeb.Models;

public partial class Vwmembresia
{
    public int IdMembresia { get; set; }

    public string? Nombre { get; set; }

    public int? IdEstado { get; set; }

    public DateTime? FechaCreacion { get; set; }

    public decimal? Precio { get; set; }

    public int? IdUsuarioCreo { get; set; }

    /// <summary>
    /// meses de la membresia
    /// </summary>
    public int? Meses { get; set; }

    /// <summary>
    /// hora que comienza la membresia para horarios especiales
    /// </summary>
    public TimeOnly? HoraInicio { get; set; }

    /// <summary>
    /// hora en que termina la membresia para horarios especiales
    /// </summary>
    public TimeOnly? HoraFinal { get; set; }

    public string? Estado { get; set; }
}
