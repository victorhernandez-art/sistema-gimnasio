using System;
using System.Collections.Generic;

namespace GymWeb.Models;

public partial class Detallesalidum
{
    public int IddetalleSalida { get; set; }

    public int? IdProducto { get; set; }

    public decimal? PrecioUnitario { get; set; }

    public int? IdSalida { get; set; }

    public int Cantidad { get; set; }


    public virtual Producto? IdProductoNavigation { get; set; }

    public virtual Salidum? IdSalidaNavigation { get; set; }
}
