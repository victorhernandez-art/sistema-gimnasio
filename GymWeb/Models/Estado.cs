using System;
using System.Collections.Generic;

namespace GymWeb.Models;

public partial class Estado
{
    public int IdEstados { get; set; }

    public string? Estado1 { get; set; }


    public virtual ICollection<Membresium> Membresia { get; set; } = new List<Membresium>();

    public virtual ICollection<Producto> Productos { get; set; } = new List<Producto>();

    public virtual ICollection<Salidum> Salida { get; set; } = new List<Salidum>();

    public virtual ICollection<Sociomembresium> Sociomembresia { get; set; } = new List<Sociomembresium>();

    public virtual ICollection<Socio> Socios { get; set; } = new List<Socio>();

    public virtual ICollection<Usuario> Usuarios { get; set; } = new List<Usuario>();
}
