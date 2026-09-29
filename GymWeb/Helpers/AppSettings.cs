namespace GymWeb.Helpers;

public static class AppSettings
{
    // Versión instalada del sistema (comparada contra el último release de GitHub)
    public const string CurrentVersion = "v2.2";

    public static string GymNombre { get; set; } = "GymPro";
    public static string GymDomicilio { get; set; } = "";
    public static string GymTelefono  { get; set; } = "";
    public static string GymPieTicket { get; set; } = "¡Gracias por su preferencia!";
    public static bool HasCustomBg   { get; set; } = false;
    public static bool HasCustomLogo { get; set; } = false;
    public static int  DiasAviso     { get; set; } = 5;
    public static string LicenseWhatsApp { get; set; } = "529611209361";
}
