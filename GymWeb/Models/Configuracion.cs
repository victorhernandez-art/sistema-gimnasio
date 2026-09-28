using System;
using System.Collections.Generic;

namespace GymWeb.Models;

public partial class Configuracion
{
    public int IdConfiguracion { get; set; }

    public string? NombreGimnacio { get; set; }

    public string? Domicilio { get; set; }

    public string? Telefono { get; set; }

    public byte[]? Logo { get; set; }

    public DateTime? FechaModificacion { get; set; }

    public int? IdUsuarioModifico { get; set; }

    public int? MensajeVencimiento { get; set; }

    public string? Rfc { get; set; }

    public string? Mensaje { get; set; }

    public decimal PrecioVisita { get; set; }
}
