using System;
using System.Collections.Generic;

namespace GymWeb.Models;

public partial class Producto
{
    public int IdProducto { get; set; }

    public string? Nombre { get; set; }

    public string? Descripcion { get; set; }

    public int? IdEstado { get; set; }

    public DateTime? FechaCreacion { get; set; }

    public decimal? Precio { get; set; }

    public int? IdUsuarioCreo { get; set; }

    public decimal? Costo { get; set; }

    public string? Categoria { get; set; } = "Otro";

    public int Stock { get; set; } = 0;

    public int StockMinimo { get; set; } = 5;

    public string? ImagenUrl { get; set; }

    public virtual ICollection<Detallesalidum> Detallesalida { get; set; } = new List<Detallesalidum>();

    public virtual Estado? IdEstadoNavigation { get; set; }

    public virtual Usuario? IdUsuarioCreoNavigation { get; set; }
}
