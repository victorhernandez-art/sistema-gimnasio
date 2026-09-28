using System;
using System.Collections.Generic;

namespace GymWeb.Models;

public partial class Vwsociomembresia
{
    public int IdSocioMembresia { get; set; }

    public int? IdEstado { get; set; }

    public DateTime? FechaCreacion { get; set; }

    public int? IdUsuarioCreo { get; set; }

    public int? IdSocio { get; set; }

    public int? IdMembresia { get; set; }

    public decimal? Precio { get; set; }

    public DateTime? FechaInicioMembresia { get; set; }

    public string? Estado { get; set; }

    public string? NombreMembresia { get; set; }

    /// <summary>
    /// meses de la membresia
    /// </summary>
    public int? Meses { get; set; }

    public string? NombreSocio { get; set; }

    public string? Paterno { get; set; }

    public string? Materno { get; set; }

    public string? Telefono { get; set; }

    public string? Observaciones { get; set; }

    public DateTime? Vencimiento { get; set; }

    public byte[]? Foto { get; set; }

    /// <summary>
    /// hora que comienza la membresia para horarios especiales
    /// </summary>
    public TimeOnly? HoraInicio { get; set; }

    /// <summary>
    /// hora en que termina la membresia para horarios especiales
    /// </summary>
    public TimeOnly? HoraFinal { get; set; }
}
