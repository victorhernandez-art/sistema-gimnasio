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

    /// <summary>Tipo de documento (S/N, DNI, Cédula, Pasaporte, RUT, INE, etc.)</summary>
    public string? TipoDocumento { get; set; } = "S/N";

    /// <summary>Clave numérica formateada a 4 dígitos consecutivos (0001, 0002...)</summary>
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public string ClaveFormateada => IdSocio.ToString("D4");

    /// <summary>
    /// Nombre completo con compatibilidad retroactiva 100%:
    /// Si el socio tiene Paterno registrado, proviene del sistema anterior (Nombre + Paterno + Materno).
    /// Si no tiene Paterno, proviene del sistema unificado (Nombre completo en campo Nombre).
    /// </summary>
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public string NombreCompleto
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(Paterno))
                return $"{Nombre} {Paterno} {Materno}".Trim();
            return (Nombre ?? "").Trim();
        }
    }

    /// <summary>Indica si el socio fue registrado en la versión previa con apellido materno en lugar de documento</summary>
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public bool EsSocioFormatoAnterior => !string.IsNullOrWhiteSpace(Paterno);

    /// <summary>Número o folio de documento de identificación (vacío si es del formato anterior donde Materno era apellido)</summary>
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public string NumeroDocumento => EsSocioFormatoAnterior ? "" : (Materno ?? "").Trim();

    /// <summary>Documento Nacional de Identidad (DNI) para Argentina, almacenado en el campo Materno</summary>
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public string? Dni
    {
        get => Materno;
        set => Materno = value;
    }

    public string? Telefono { get; set; }

    public string? Observaciones { get; set; }

    public int? IdUsuarioCreo { get; set; }

    public byte[]? Foto { get; set; }

    public virtual Estado? IdEstadoNavigation { get; set; }

    public virtual Usuario? IdUsuarioCreoNavigation { get; set; }

    public virtual ICollection<Registro> Registros { get; set; } = new List<Registro>();

    public virtual ICollection<Sociomembresium> Sociomembresia { get; set; } = new List<Sociomembresium>();

    public virtual ICollection<Salidum> Salidas { get; set; } = new List<Salidum>();

    public virtual SocioHuella? SocioHuella { get; set; }
}
