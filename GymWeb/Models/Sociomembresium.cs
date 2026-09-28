using System;
using System.Collections.Generic;

namespace GymWeb.Models;

public partial class Sociomembresium
{
    public int IdSocioMembresia { get; set; }

    public int? IdEstado { get; set; }

    public DateTime? FechaCreacion { get; set; }

    public int? IdUsuarioCreo { get; set; }

    public int? IdSocio { get; set; }

    public int? IdMembresia { get; set; }

    public decimal? Precio { get; set; }

    public DateTime? FechaInicioMembresia { get; set; }

    public virtual Estado? IdEstadoNavigation { get; set; }

    public virtual Membresium? IdMembresiaNavigation { get; set; }

    public virtual Socio? IdSocioNavigation { get; set; }

    public virtual Usuario? IdUsuarioCreoNavigation { get; set; }
}
