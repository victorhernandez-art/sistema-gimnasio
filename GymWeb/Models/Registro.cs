using System;
using System.Collections.Generic;

namespace GymWeb.Models;

public partial class Registro
{
    public int Idregistro { get; set; }

    public int? IdSocio { get; set; }

    public string? NombreVisita { get; set; }

    public decimal PrecioVisita { get; set; }

    public DateTime? FechaCreacion { get; set; }

    public virtual Socio? IdSocioNavigation { get; set; }
}
