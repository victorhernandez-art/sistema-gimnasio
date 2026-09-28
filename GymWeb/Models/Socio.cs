using System;
using System.Collections.Generic;

namespace GymWeb.Models;

public partial class Socio
{
    public int IdSocio { get; set; }

    public int? IdEstado { get; set; }

    public DateTime? FechaCreacion { get; set; }

    public string? Nombre { get; set; }

    public string? Paterno { get; set; }

    public string? Materno { get; set; }

    public string? Telefono { get; set; }

    public string? Observaciones { get; set; }

    public int? IdUsuarioCreo { get; set; }

    public byte[]? Foto { get; set; }

    public virtual Estado? IdEstadoNavigation { get; set; }

    public virtual Usuario? IdUsuarioCreoNavigation { get; set; }

    public virtual ICollection<Registro> Registros { get; set; } = new List<Registro>();

    public virtual ICollection<Sociomembresium> Sociomembresia { get; set; } = new List<Sociomembresium>();


    public virtual SocioHuella? SocioHuella { get; set; }
}
