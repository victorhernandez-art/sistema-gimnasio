using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace GymApp;

public partial class Form1 : Form
{
    private readonly WebView2 _webView;
    private readonly Panel _loadingPanel;
    private readonly Label _lblTitle;
    private readonly Label _lblStatus;
    private readonly Button _btnRetry;
    private string _targetUrl = "http://localhost:5080";
    private Process? _spawnedBackend;
    private Process? _spawnedBio;
    private int _retryCount = 0;
    private Form? _whatsAppForm;
    private WebView2? _whatsAppWebView;
    private CoreWebView2Environment? _webViewEnv;

    public Form1()
    {
        InitializeComponent();

        Text = "Sistema Gimnasio";
        Width = 1366;
        Height = 768;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(15, 20, 28);

        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        string icoPath = Path.Combine(baseDir, "app.ico");
        if (File.Exists(icoPath))
        {
            try { Icon = new Icon(icoPath); } catch { }
        }

        // Panel de bienvenida y carga con estética premium oscura
        _loadingPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(15, 20, 28)
        };

        _lblTitle = new Label
        {
            Text = "SISTEMA DE GIMNASIO",
            ForeColor = Color.FromArgb(0, 188, 212),
            Font = new Font("Segoe UI", 18F, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleCenter,
            Size = new Size(600, 40),
            Anchor = AnchorStyles.None
        };

        _lblStatus = new Label
        {
            Text = "Iniciando sistema...\nPor favor espere un momento.",
            ForeColor = Color.FromArgb(200, 210, 220),
            Font = new Font("Segoe UI", 11F, FontStyle.Regular),
            TextAlign = ContentAlignment.MiddleCenter,
            Size = new Size(600, 60),
            Anchor = AnchorStyles.None
        };

        _btnRetry = new Button
        {
            Text = "Reintentar Conexión",
            ForeColor = Color.White,
            BackColor = Color.FromArgb(0, 150, 180),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 10F, FontStyle.Bold),
            Size = new Size(200, 42),
            Visible = false,
            Cursor = Cursors.Hand,
            Anchor = AnchorStyles.None
        };
        _btnRetry.FlatAppearance.BorderSize = 0;
        _btnRetry.Click += async (s, e) => await StartAndConnectAsync();

        _loadingPanel.Controls.Add(_lblTitle);
        _loadingPanel.Controls.Add(_lblStatus);
        _loadingPanel.Controls.Add(_btnRetry);

        _loadingPanel.Resize += (s, e) =>
        {
            int cx = _loadingPanel.ClientSize.Width / 2;
            int cy = _loadingPanel.ClientSize.Height / 2;

            _lblTitle.Location = new Point(cx - (_lblTitle.Width / 2), cy - 80);
            _lblStatus.Location = new Point(cx - (_lblStatus.Width / 2), cy - 25);
            _btnRetry.Location = new Point(cx - (_btnRetry.Width / 2), cy + 50);
        };

        _webView = new WebView2
        {
            Dock = DockStyle.Fill,
            Visible = false
        };

        Controls.Add(_webView);
        Controls.Add(_loadingPanel);
        _loadingPanel.BringToFront();

        Load += Form1_Load;
    }

    private async void Form1_Load(object? sender, EventArgs e)
    {
        await StartAndConnectAsync();
    }

    private async Task StartAndConnectAsync()
    {
        _btnRetry.Visible = false;
        _lblStatus.Text = "Verificando servicios del sistema...\nPor favor espere un momento.";
        _loadingPanel.Visible = true;
        _loadingPanel.BringToFront();
        _webView.Visible = false;

        string baseDir = AppDomain.CurrentDomain.BaseDirectory;

        // 1. Probar si el backend ya responde en 5080 o 5250
        string? activeUrl = await TryGetActiveUrlAsync(timeoutMs: 800);

        // 2. Si no responde, asegurar que GymWeb.exe y BioService.exe arranquen
        if (string.IsNullOrEmpty(activeUrl))
        {
            EnsureProcessRunning("GymWeb", Path.Combine(baseDir, "GymWeb.exe"), out _spawnedBackend);
            EnsureProcessRunning("BioService", Path.Combine(baseDir, "BioService", "BioService.exe"), out _spawnedBio);

            _lblStatus.Text = "Iniciando motor de base de datos y servidor local...\nEsto tomará solo unos segundos.";

            // Polling durante hasta 30 segundos (60 intentos cada 500ms)
            for (int i = 0; i < 60; i++)
            {
                await Task.Delay(500);
                activeUrl = await TryGetActiveUrlAsync(timeoutMs: 700);
                if (!string.IsNullOrEmpty(activeUrl))
                    break;
            }
        }

        if (string.IsNullOrEmpty(activeUrl))
        {
            _lblStatus.Text = "No se pudo establecer conexión con el servidor local del gimnasio.\n\nVerifique que ningún software esté bloqueando el puerto 5080.";
            _btnRetry.Visible = true;
            return;
        }

        _targetUrl = activeUrl;
        _lblStatus.Text = "Cargando interfaz del gimnasio...";

        try
        {
            if (_webView.CoreWebView2 == null)
            {
                string cacheDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "GymAppCache"
                );

                var env = await CoreWebView2Environment.CreateAsync(null, cacheDir);
                await _webView.EnsureCoreWebView2Async(env);

                _webViewEnv = env;
                if (_webView.CoreWebView2 != null)
                {
                    _webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;
                    _webView.CoreWebView2.Settings.IsStatusBarEnabled = false;

                    // Manejo exclusivo de ventana única (Singleton) para WhatsApp Web idéntico a Sistema Taller
                    _webView.CoreWebView2.NewWindowRequested += async (s, args) =>
                    {
                        string uri = args.Uri ?? "";
                        if (uri.Contains("web.whatsapp.com", StringComparison.OrdinalIgnoreCase) ||
                            uri.Contains("api.whatsapp.com", StringComparison.OrdinalIgnoreCase) ||
                            uri.Contains("wa.me", StringComparison.OrdinalIgnoreCase))
                        {
                            args.Handled = true; // Impedir que WebView2 cree una ventana genérica duplicada
                            await AbrirOEnfocarWhatsAppAsync(uri);
                        }
                    };
                }

                _webView.NavigationCompleted += (s, args) =>
                {
                    if (args.IsSuccess)
                    {
                        _retryCount = 0;
                        _loadingPanel.Visible = false;
                        _webView.Visible = true;
                    }
                    else
                    {
                        // Si hubo un fallo de conexión temporal, reintentar automáticamente
                        if (_retryCount < 5)
                        {
                            _retryCount++;
                            Task.Delay(1000).ContinueWith(_ =>
                            {
                                if (!IsDisposed && _webView != null && _webView.CoreWebView2 != null)
                                {
                                    Invoke(() => _webView.CoreWebView2.Navigate(_targetUrl));
                                }
                            });
                        }
                        else
                        {
                            _loadingPanel.Visible = true;
                            _loadingPanel.BringToFront();
                            _webView.Visible = false;
                            _lblStatus.Text = "Ocurrió una interrupción al cargar la pantalla del sistema.\nHaga clic en 'Reintentar Conexión'.";
                            _btnRetry.Visible = true;
                        }
                    }
                };

                _webView.Source = new Uri(_targetUrl);
            }
            else
            {
                _loadingPanel.Visible = false;
                _webView.Visible = true;
                _webView.CoreWebView2.Navigate(_targetUrl);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                "Error al inicializar el visor del Gimnasio: " + ex.Message,
                "Sistema Gimnasio",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error
            );
        }
    }

    private static void EnsureProcessRunning(string processName, string exePath, out Process? spawnedProcess)
    {
        spawnedProcess = null;
        try
        {
            var running = Process.GetProcessesByName(processName);
            if (running.Length > 0)
                return; // Ya está corriendo (por ejemplo como Servicio de Windows o proceso previo)

            if (File.Exists(exePath))
            {
                var psi = new ProcessStartInfo
                {
                    FileName = exePath,
                    WorkingDirectory = Path.GetDirectoryName(exePath)!,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                spawnedProcess = Process.Start(psi);
            }
        }
        catch { }
    }

    private static async Task<string?> TryGetActiveUrlAsync(int timeoutMs)
    {
        // Revisar 5080 (producción) y 5250 (pruebas)
        string[] ports = { "5080", "5250" };
        using var client = new HttpClient { Timeout = TimeSpan.FromMilliseconds(timeoutMs) };

        foreach (var port in ports)
        {
            try
            {
                var res = await client.GetAsync($"http://localhost:{port}");
                if (res.IsSuccessStatusCode || (int)res.StatusCode < 500)
                    return $"http://localhost:{port}";
            }
            catch { }
        }

        return null;
    }

    private async Task AbrirOEnfocarWhatsAppAsync(string uri)
    {
        try
        {
            if (_whatsAppForm != null && !_whatsAppForm.IsDisposed)
            {
                // Si la ventana ya existe pero está minimizada en Windows, restaurarla inmediatamente
                if (_whatsAppForm.WindowState == FormWindowState.Minimized)
                {
                    _whatsAppForm.WindowState = FormWindowState.Normal;
                }

                _whatsAppForm.BringToFront();
                _whatsAppForm.Activate();

                // Navegar al nuevo chat o conversación sin crear otra ventana
                if (_whatsAppWebView != null && _whatsAppWebView.CoreWebView2 != null)
                {
                    _whatsAppWebView.CoreWebView2.Navigate(uri);
                }
                return;
            }

            // Crear ventana integrada centrada (1080x760 px, idéntica a Sistema Taller)
            _whatsAppForm = new Form
            {
                Text = "WhatsApp Web — Sistema Gimnasio",
                Width = 1080,
                Height = 760,
                MinimumSize = new Size(800, 600),
                StartPosition = FormStartPosition.CenterScreen,
                BackColor = Color.FromArgb(17, 27, 33)
            };

            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string icoPath = Path.Combine(baseDir, "app.ico");
            if (File.Exists(icoPath))
            {
                try { _whatsAppForm.Icon = new Icon(icoPath); } catch { }
            }

            _whatsAppWebView = new WebView2
            {
                Dock = DockStyle.Fill
            };
            _whatsAppForm.Controls.Add(_whatsAppWebView);

            _whatsAppForm.FormClosed += (s, e) =>
            {
                _whatsAppWebView?.Dispose();
                _whatsAppWebView = null;
                _whatsAppForm = null;
            };

            _whatsAppForm.Show(this);

            if (_webViewEnv != null)
            {
                await _whatsAppWebView.EnsureCoreWebView2Async(_webViewEnv);
            }
            else
            {
                await _whatsAppWebView.EnsureCoreWebView2Async();
            }

            _whatsAppWebView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;
            _whatsAppWebView.CoreWebView2.Settings.IsStatusBarEnabled = false;

            _whatsAppWebView.CoreWebView2.Navigate(uri);
            _whatsAppForm.BringToFront();
            _whatsAppForm.Activate();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error al abrir WhatsApp Web singleton: {ex.Message}");
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (e.CloseReason == CloseReason.UserClosing)
        {
            var res = MessageBox.Show(
                "¿Deseas salir del Sistema del Gimnasio?\n\n(Si solo estabas en la ventana de impresión, presiona 'No' y haz clic en el botón 'Cancelar' o presiona la tecla Escape en tu teclado).",
                "Confirmar salida - Sistema Gimnasio",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question,
                MessageBoxDefaultButton.Button2
            );

            if (res != DialogResult.Yes)
            {
                e.Cancel = true;
                return;
            }
        }

        // Si iniciamos los procesos en segundo plano como aplicación independiente (sin servicio de Windows), cerrarlos
        try
        {
            if (_spawnedBackend != null && !_spawnedBackend.HasExited)
            {
                _spawnedBackend.Kill(true);
            }
        }
        catch { }

        try
        {
            if (_spawnedBio != null && !_spawnedBio.HasExited)
            {
                _spawnedBio.Kill(true);
            }
        }
        catch { }

        base.OnFormClosing(e);
    }
}
