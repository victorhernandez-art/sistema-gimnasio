namespace GymWeb.Models;

public class SocioHuella
{
    public int IdHuella { get; set; }
    public int IdSocio { get; set; }
    /// <summary>PIN único que identifica al socio en el dispositivo biométrico</summary>
    public int Pin { get; set; }
    /// <summary>Template biométrico SourceAFIS (portable, independiente del lector)</summary>
    public byte[]? Template { get; set; }
    public string? IdDispositivo { get; set; }
    public int IdEstado { get; set; } = 1;
    public DateTime? FechaRegistro { get; set; }

    public virtual Socio? IdSocioNavigation { get; set; }
}
