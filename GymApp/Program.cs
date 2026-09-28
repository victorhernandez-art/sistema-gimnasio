using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace GymApp;

static class Program
{
    [DllImport("shell32.dll", SetLastError = true)]
    private static extern void SetCurrentProcessExplicitAppUserModelID([MarshalAs(UnmanagedType.LPWStr)] string appId);

    [STAThread]
    static void Main()
    {
        try
        {
            // Registrar ID de aplicación para que Windows asigne el icono oficial en la barra de tareas
            SetCurrentProcessExplicitAppUserModelID("SistemaGimnasio.Desktop.App");
        }
        catch { }

        ApplicationConfiguration.Initialize();
        Application.Run(new Form1());
    }
}