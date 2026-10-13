using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace GymWeb.Helpers;

public class NetworkAdapterItem
{
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
    public string IpAddress { get; set; } = "";
}

public class RedLanInfo
{
    public string IpPrincipal { get; set; } = "127.0.0.1";
    public string IpLocal => IpPrincipal;
    public int Puerto { get; set; } = 5080;
    public string UrlCompleta => $"http://{IpPrincipal}:{Puerto}";
    public List<NetworkAdapterItem> Adaptadores { get; set; } = new();
    public string NombreEquipo { get; set; } = "";
    public bool ServidorActivo { get; set; } = true;
}

public static class NetworkHelper
{
    public static RedLanInfo ObtenerInfoRed(int puerto = 5080)
    {
        var info = new RedLanInfo
        {
            Puerto = puerto,
            NombreEquipo = Environment.MachineName
        };

        var adaptadores = new List<NetworkAdapterItem>();

        try
        {
            var interfaces = NetworkInterface.GetAllNetworkInterfaces();
            var candidatos = new List<(NetworkAdapterItem item, int prioridad)>();

            foreach (var iface in interfaces)
            {
                if (iface.OperationalStatus != OperationalStatus.Up) continue;
                if (iface.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;

                var desc = (iface.Description ?? "").ToLowerInvariant();
                var name = (iface.Name ?? "").ToLowerInvariant();

                bool esVirtual = desc.Contains("virtual") || desc.Contains("vmware") || desc.Contains("hyper-v") ||
                                 desc.Contains("wsl") || name.Contains("vethernet") || desc.Contains("pseudo");

                var ipProps = iface.GetIPProperties();
                bool tieneGateway = ipProps.GatewayAddresses != null &&
                                    ipProps.GatewayAddresses.Any(g => g.Address != null &&
                                                                      g.Address.AddressFamily == AddressFamily.InterNetwork &&
                                                                      !g.Address.Equals(IPAddress.Any) &&
                                                                      !g.Address.ToString().StartsWith("0."));

                foreach (var addr in ipProps.UnicastAddresses)
                {
                    if (addr.Address.AddressFamily == AddressFamily.InterNetwork)
                    {
                        string ipStr = addr.Address.ToString();
                        // Ignorar APIPA (169.254.x.x) y loopback
                        if (!ipStr.StartsWith("169.254.") && !ipStr.StartsWith("127."))
                        {
                            bool esWifi = iface.NetworkInterfaceType == NetworkInterfaceType.Wireless80211;
                            string tipo = esWifi ? "Wi-Fi" :
                                          iface.NetworkInterfaceType == NetworkInterfaceType.Ethernet ? "Ethernet" : "LAN";

                            var item = new NetworkAdapterItem
                            {
                                Name = $"{iface.Name} ({tipo})",
                                Type = tipo,
                                IpAddress = ipStr
                            };

                            // Puntuación de prioridad (más alto = mejor opción)
                            int prioridad = 0;
                            if (tieneGateway && esWifi) prioridad = 100;
                            else if (tieneGateway) prioridad = 90;
                            else if (esWifi && !esVirtual) prioridad = 70;
                            else if (!esVirtual) prioridad = 50;
                            else prioridad = 10;

                            candidatos.Add((item, prioridad));
                        }
                    }
                }
            }

            adaptadores = candidatos.OrderByDescending(c => c.prioridad).Select(c => c.item).ToList();
        }
        catch { }

        if (!adaptadores.Any())
        {
            try
            {
                var host = Dns.GetHostEntry(Dns.GetHostName());
                foreach (var ip in host.AddressList)
                {
                    if (ip.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip))
                    {
                        string s = ip.ToString();
                        if (!s.StartsWith("169.254."))
                        {
                            adaptadores.Add(new NetworkAdapterItem
                            {
                                Name = "Red Local IPv4",
                                Type = "LAN",
                                IpAddress = s
                            });
                        }
                    }
                }
            }
            catch { }
        }

        info.Adaptadores = adaptadores;
        info.IpPrincipal = adaptadores.FirstOrDefault()?.IpAddress ?? "127.0.0.1";
        return info;
    }
}
