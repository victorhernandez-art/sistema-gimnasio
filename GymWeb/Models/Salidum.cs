using System;
using System.Collections.Generic;

namespace GymWeb.Models;

public partial class Salidum
{
    public int IdSalida { get; set; }

    public int? IdEstado { get; set; }

    public DateTime? FechaCreacion { get; set; }

    public decimal? Total { get; set; }

    public int? IdUsuarioCreo { get; set; }

    public string? MetodoPago { get; set; } = "Efectivo";

    public string? Folio { get; set; }

    public decimal? Descuento { get; set; } = 0;

    public decimal? MontoRecibido { get; set; }

    public decimal? Cambio { get; set; }

    public string? Referencia { get; set; }

    public string? Notas { get; set; }

    public virtual ICollection<Detallesalidum> Detallesalida { get; set; } = new List<Detallesalidum>();

    public virtual Estado? IdEstadoNavigation { get; set; }

    public virtual Usuario? IdUsuarioCreoNavigation { get; set; }
}
