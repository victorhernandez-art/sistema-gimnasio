using System;
using System.Collections.Generic;

namespace GymWeb.Models;

public partial class Vwusuario
{
    public string? Estado { get; set; }

    public int IdUsuario { get; set; }

    public int? IdEstado { get; set; }

    public string? Usuario { get; set; }

    public string? Nombre { get; set; }

    public DateTime? FechaCreacion { get; set; }

    public string? Password { get; set; }
}
