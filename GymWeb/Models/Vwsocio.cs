using System;
using System.Collections.Generic;

namespace GymWeb.Models;

public partial class Vwsocio
{
    public int IdSocio { get; set; }

    public int? IdEstado { get; set; }

    public DateTime? FechaCreacion { get; set; }

    public string? Nombre { get; set; }

    public string? Paterno { get; set; }

    public string? Materno { get; set; }

    public string? Telefono { get; set; }

    public string? Observaciones { get; set; }

    public int? IdUsuarioCreo { get; set; }

    public byte[]? Foto { get; set; }

    public string? Estado { get; set; }
}
