using System;

namespace GymWeb.Models;

public partial class Pago
{
    public int IdPago { get; set; }
    public int IdSocio { get; set; }
    public int? IdMembresia { get; set; }
    public decimal Monto { get; set; }
    public string MetodoPago { get; set; } = "Efectivo";
    public DateOnly FechaPago { get; set; }
    public string? Notas { get; set; }
    public int IdEstado { get; set; } = 1;
    public DateTime? FechaCreacion { get; set; }
    public int? IdUsuarioCreo { get; set; }

    public virtual Socio? IdSocioNavigation { get; set; }
    public virtual Membresium? IdMembresiaNavigation { get; set; }
    public virtual Usuario? IdUsuarioCreoNavigation { get; set; }
}
