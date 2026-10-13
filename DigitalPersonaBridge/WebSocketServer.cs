using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace DigitalPersonaBridge;

public class WebSocketServer
{
    private readonly int _puerto;
    private readonly DigitalPersonaHandler _handler;
    private readonly ConcurrentDictionary<string, WebSocket> _clientes = new();
    private HttpListener? _listener;
    private bool _corriendo = false;

    public WebSocketServer(int puerto, DigitalPersonaHandler handler)
    {
        _puerto = puerto;
        _handler = handler;

        // Suscribir eventos del lector de huellas para retransmitir por WebSocket a los navegadores
        _handler.OnStatusChanged += (msg) => Broadcast(new { @event = "status", message = msg });
        _handler.OnFingerTouch += () => Broadcast(new { @event = "finger_touch", message = "Dedo detectado en el sensor..." });
        _handler.OnFingerGone += () => Broadcast(new { @event = "finger_gone", message = "Dedo retirado del lector..." });
        _handler.OnSampleQuality += (actual, total) => Broadcast(new
        {
            @event = "sample_quality",
            muestra = actual,
            totalMuestras = total,
            message = $"Muestra {actual} de {total} procesada correctamente."
        });
        _handler.OnTemplateCaptured += (templateB64) => Broadcast(new
        {
            @event = "enrolled",
            ok = true,
            template = templateB64,
            quality = 100,
            readerName = "DigitalPersona U.are.U 4500 (Non-WBF)",
            message = "Huella capturada y plantilla generada con éxito."
        });
        _handler.OnError += (err) => Broadcast(new { @event = "error", message = err });
    }

    public async Task StartAsync(CancellationToken ct = default)
    {
        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://localhost:{_puerto}/");
        _listener.Prefixes.Add($"http://127.0.0.1:{_puerto}/");
        _listener.Start();
        _corriendo = true;

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"=========================================================");
        Console.WriteLine($"   PUENTE BIOMETRICO DIGITALPERSONA U.ARE.U 4500");
        Console.WriteLine($"   Servidor WebSocket activo en: ws://localhost:{_puerto}");
        Console.WriteLine($"=========================================================\n");
        Console.ResetColor();

        while (_corriendo && !ct.IsCancellationRequested)
        {
            try
            {
                var context = await _listener.GetContextAsync();
                if (context.Request.IsWebSocketRequest)
                {
                    _ = ProcesarClienteWebSocketAsync(context);
                }
                else
                {
                    // Soporte HTTP: chequeo de estado o disparo de tecla Enter para WhatsApp
                    context.Response.Headers.Add("Access-Control-Allow-Origin", "*");
                    context.Response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
                    context.Response.Headers.Add("Access-Control-Allow-Headers", "*");

                    if (context.Request.HttpMethod == "OPTIONS")
                    {
                        context.Response.StatusCode = 200;
                        context.Response.Close();
                        continue;
                    }

                    context.Response.ContentType = "application/json; charset=utf-8";

                    string path = context.Request.Url?.AbsolutePath?.ToLowerInvariant() ?? "";
                    if (path.Contains("whatsapp") || path.Contains("press-enter"))
                    {
                        int delay = 5500;
                        var query = context.Request.QueryString;
                        if (!string.IsNullOrEmpty(query["delay"]) && int.TryParse(query["delay"], out var d))
                        {
                            delay = d;
                        }

                        _ = Task.Run(async () =>
                        {
                            await WhatsAppAutomationHelper.PresionarEnterWhatsAppAsync(delay);
                        });

                        var respJson = JsonSerializer.Serialize(new
                        {
                            ok = true,
                            action = "whatsapp_press_enter",
                            delayMs = delay,
                            message = $"Auto-Enter programado para ejecutarse en {delay}ms."
                        });
                        var respBuf = Encoding.UTF8.GetBytes(respJson);
                        context.Response.ContentLength64 = respBuf.Length;
                        await context.Response.OutputStream.WriteAsync(respBuf, ct);
                        context.Response.Close();
                        continue;
                    }

                    var json = JsonSerializer.Serialize(new
                    {
                        ok = true,
                        service = "DigitalPersonaBridge",
                        wsUrl = $"ws://localhost:{_puerto}",
                        readerConnected = _handler.IsReaderConnected,
                        readerName = "DigitalPersona U.are.U 4500 (HID)"
                    });
                    var buffer = Encoding.UTF8.GetBytes(json);
                    context.Response.ContentLength64 = buffer.Length;
                    await context.Response.OutputStream.WriteAsync(buffer, ct);
                    context.Response.Close();
                }
            }
            catch (HttpListenerException) when (!_corriendo)
            {
                break;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WS LISTENER ERROR] {ex.Message}");
            }
        }
    }

    private async Task ProcesarClienteWebSocketAsync(HttpListenerContext context)
    {
        WebSocket? webSocket = null;
        string idCliente = Guid.NewGuid().ToString("N");

        try
        {
            var wsContext = await context.AcceptWebSocketAsync(subProtocol: null);
            webSocket = wsContext.WebSocket;
            _clientes.TryAdd(idCliente, webSocket);

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"[CONEXION] Cliente conectado desde el navegador (ID: {idCliente[..8]}).");
            Console.ResetColor();

            // 1. Enviar estado inicial del lector al cliente
            bool readerOk = _handler.IsReaderConnected || _handler.VerificarLectorUsbConectado();
            await EnviarMensajeAsync(webSocket, new
            {
                @event = "status",
                connected = true,
                readerConnected = readerOk,
                readerName = "DigitalPersona U.are.U 4500",
                message = readerOk ? "Lector DigitalPersona U.are.U 4500 conectado y listo." : "Lector conectado — Listo para capturar"
            });

            // 2. Loop de recepción de comandos desde el frontend
            var buffer = new byte[4096];
            while (webSocket.State == WebSocketState.Open)
            {
                var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    break;
                }

                if (result.MessageType == WebSocketMessageType.Text)
                {
                    string texto = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    ProcesarComandoFrontend(texto, webSocket);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[WS CLIENT ERROR] {ex.Message}");
        }
        finally
        {
            _clientes.TryRemove(idCliente, out _);
            if (webSocket != null)
            {
                try { await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Cerrado", CancellationToken.None); } catch { }
                webSocket.Dispose();
            }
            Console.WriteLine($"[DESCONEXION] Cliente desconectado (ID: {idCliente[..8]}).");
        }
    }

    private void ProcesarComandoFrontend(string json, WebSocket sender)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            string action = "";
            if (root.TryGetProperty("action", out var actionProp))
            {
                action = actionProp.GetString() ?? "";
            }
            else if (root.TryGetProperty("command", out var cmdProp))
            {
                action = cmdProp.GetString() ?? "";
            }

            Console.WriteLine($"[COMANDO RECIBIDO] '{action}'");

            switch (action.ToLowerInvariant())
            {
                case "start_enroll":
                case "capture":
                case "iniciar_captura":
                    _handler.IniciarCaptura();
                    break;

                case "cancel":
                case "cancelar":
                    _handler.CancelarCaptura();
                    break;

                case "status":
                    bool readerOk = _handler.IsReaderConnected || _handler.VerificarLectorUsbConectado();
                    _ = EnviarMensajeAsync(sender, new
                    {
                        @event = "status",
                        connected = true,
                        readerConnected = readerOk,
                        readerName = "DigitalPersona U.are.U 4500",
                        message = readerOk ? "Lector DigitalPersona conectado y listo." : "Esperando sensor..."
                    });
                    break;

                case "whatsapp_auto_send":
                case "whatsapp_press_enter":
                case "auto_enter":
                    int delay = 5500;
                    if (root.TryGetProperty("delay", out var dProp) && dProp.TryGetInt32(out var dVal))
                    {
                        delay = dVal;
                    }
                    else if (root.TryGetProperty("delayMs", out var dmProp) && dmProp.TryGetInt32(out var dmVal))
                    {
                        delay = dmVal;
                    }

                    _ = Task.Run(async () =>
                    {
                        bool ok = await WhatsAppAutomationHelper.PresionarEnterWhatsAppAsync(delay);
                        await EnviarMensajeAsync(sender, new
                        {
                            @event = "whatsapp_sent",
                            ok = ok,
                            message = "Tecla Enter disparada automáticamente a WhatsApp."
                        });
                    });
                    break;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[PARSE ERROR] {ex.Message}");
        }
    }

    public void Broadcast(object data)
    {
        string json = JsonSerializer.Serialize(data);
        byte[] bytes = Encoding.UTF8.GetBytes(json);

        foreach (var kvp in _clientes)
        {
            var ws = kvp.Value;
            if (ws.State == WebSocketState.Open)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
                    }
                    catch { }
                });
            }
        }
    }

    private static async Task EnviarMensajeAsync(WebSocket ws, object data)
    {
        if (ws.State != WebSocketState.Open) return;
        string json = JsonSerializer.Serialize(data);
        byte[] bytes = Encoding.UTF8.GetBytes(json);
        await ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
    }

    public void Stop()
    {
        _corriendo = false;
        try { _listener?.Stop(); } catch { }
    }
}
