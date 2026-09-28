using System;
using System.Collections.Generic;

namespace GymWeb.Models;

public partial class Vwproducto
{
    public int IdProducto { get; set; }

    public string? Nombre { get; set; }

    public string? Descripcion { get; set; }

    public int? IdEstado { get; set; }

    public DateTime? FechaCreacion { get; set; }

    public decimal? Precio { get; set; }

    public int? IdUsuarioCreo { get; set; }

    public string? Estado { get; set; }

    public decimal? Costo { get; set; }

    public string? Categoria { get; set; }

    public int Stock { get; set; }

    public int StockMinimo { get; set; }
}
