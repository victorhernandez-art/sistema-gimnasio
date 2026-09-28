using System;
using System.Collections.Generic;

namespace GymWeb.Models;

public partial class Usuario
{
    public int IdUsuario { get; set; }

    public int? IdEstado { get; set; }

    public string? Usuario1 { get; set; }

    public string? Nombre { get; set; }

    public DateTime? FechaCreacion { get; set; }

    public string? Password { get; set; }

    public string? Rol { get; set; }


    public virtual Estado? IdEstadoNavigation { get; set; }

    public virtual ICollection<Membresium> Membresia { get; set; } = new List<Membresium>();

    public virtual ICollection<Producto> Productos { get; set; } = new List<Producto>();

    public virtual ICollection<Salidum> Salida { get; set; } = new List<Salidum>();

    public virtual ICollection<Sociomembresium> Sociomembresia { get; set; } = new List<Sociomembresium>();

    public virtual ICollection<Socio> Socios { get; set; } = new List<Socio>();
}
