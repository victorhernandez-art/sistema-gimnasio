using System;
using System.Collections.Generic;

namespace GymWeb.Models;

public partial class Rptventaproducto
{
    public DateOnly? Fecha { get; set; }

    public DateTime? Fechacreacion { get; set; }

    public string? Nombre { get; set; }

    public decimal? CostoUnitario { get; set; }

    public decimal? PrecioUnitario { get; set; }

    public decimal? Ganancia { get; set; }
}
