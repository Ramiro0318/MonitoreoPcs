using Cliente.Models.Entities;
using Cliente.ViewModels;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Timers;
using System.Windows.Documents.DocumentStructures;
using System.Windows.Threading;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Cliente.Services
{
    public class ClienteService
    {
        string filename = "registro.json";
        private int puerto = 60000; //Puerto de servidor
        public IPAddress? Ip { set; get; } // IpServidor

        private bool escuchando = false;
        private bool latiendo = false;
        private int latidosEnviados;
        private bool internet;
        private DateTime ultimoPing = DateTime.Now;
        private System.Timers.Timer TimerBeat;
        private Info? Registro { set; get; }
        UdpClient Cliente { get; set; }


        public event Action<string>? InformacionActualizada, RegistroEliminado;
        public event Action<Info>? RegistroAbierto, RegistroGuardado;
        public event Action<Info, string>? RegistroActualizado;
        public event Action<bool>? EstadoInternetCambiado;
        public event Action<Pagina>? PaginaCambiada;


        public void Iniciar()
        {
            try
            {

                AbrirRegistro();


                IPEndPoint endpoint = new(IPAddress.Any, 60001);
                Cliente = new UdpClient(endpoint);

                if (Registro != null)
                {
                    latiendo = true;

                    Thread hiloEscuchar = new(RecibirMensajes);
                    hiloEscuchar.IsBackground = true;
                    hiloEscuchar.Start();

                    Thread hiloInternet = new(RevisarInternet);
                    hiloInternet.IsBackground = true;
                    hiloInternet.Start();

                    EnviarHearthbeat();
                }
            }
            catch (Exception)
            {

                throw;
            }
        }
        private void AbrirRegistro()
        {
            if (File.Exists(filename))
            {
                var jsonString = File.ReadAllText(filename);
                var registro = JsonSerializer.Deserialize<Info>(jsonString);
                if (registro != null)
                {
                    Registro = registro;
                    RegistroAbierto?.Invoke(Registro);
                    PaginaCambiada?.Invoke(Pagina.Conectado);
                }
            }
        }



        public string ObtenerDireccionMac()
        {
            // 1. Obtener todas las interfaces de red (Ethernet, Wi-Fi, etc.)
            var interfaces = NetworkInterface.GetAllNetworkInterfaces();

            // 2. Filtrar para encontrar la más adecuada
            var tarjetaActiva = interfaces.FirstOrDefault(nic =>
                nic.OperationalStatus == OperationalStatus.Up && // Que esté encendida
                nic.NetworkInterfaceType != NetworkInterfaceType.Loopback && // Que no sea software interno
                nic.NetworkInterfaceType != NetworkInterfaceType.Tunnel); // Que no sea una VPN o Túnel

            if (tarjetaActiva != null)
            {
                // 3. Obtener la dirección física
                PhysicalAddress mac = tarjetaActiva.GetPhysicalAddress();
                byte[] bytes = mac.GetAddressBytes();

                // 4. Formatear los bytes a Hexadecimal (Ej: 00-1A-2B-3C-4D-5E)
                return string.Join("-", bytes.Select(b => b.ToString("X2")));
            }

            return "";
        }


        public void EnviarRegistro(string ip, string nombre, string laboratorio)
        {

            if (string.IsNullOrWhiteSpace(ip) && string.IsNullOrWhiteSpace(nombre) || string.IsNullOrWhiteSpace(laboratorio))
            {
                InformacionActualizada?.Invoke("No deje en blanco ningun dato.");
                return;
            }       
            if (!IPAddress.IsValid(ip))
            {
                InformacionActualizada?.Invoke("Introduzca una dirección IPv4 válida.");
                return;
            }
            var mac = ObtenerDireccionMac();
            if (mac == "")
            {
                InformacionActualizada?.Invoke("Existe un problema con su direccion MAC.");
                return;
            }
            else
            {
                try
                {
                    Ip = IPAddress.Parse(ip);

                    IPEndPoint remoto = new IPEndPoint(Ip, puerto);

                    string comando = $"{Orden.REGISTRO}|{nombre}|{laboratorio}|{mac}";
                    byte[] buffer = Encoding.UTF8.GetBytes(comando);
                    Cliente.Send(buffer, buffer.Length, remoto);

                    InformacionActualizada?.Invoke($"Solicitud de registro enviada.");

                    if (!escuchando)
                    {
                        //Empieza a escuchar si no está escuchando ya
                        Thread hiloEscuchar = new(RecibirMensajes);
                        hiloEscuchar.IsBackground = true;
                        hiloEscuchar.Start();
                    }
                }
                catch { }
            }
        }

        public void GuardarRegistro(string nombre, string laboratorio, string? mac)
        {
            if (nombre != null)
            {
                var registro = new Info
                {
                    NombreAsignado = nombre,
                    IpServidor = Ip.ToString(),
                    PuertoServidor = puerto,
                    Laboratorio = laboratorio,
                    MAC = mac
                };

                Registro = registro;
                RegistroGuardado?.Invoke(registro);
                string jsonString = JsonSerializer.Serialize(registro);
                File.WriteAllText(filename, jsonString);

            }
        }


        public void RecibirMensajes()
        {
            escuchando = true;
            InformacionActualizada?.Invoke("Escuchando mensajes");

            while (escuchando)
            {
                try
                {
                    IPEndPoint remoto = new(IPAddress.Any, 0);
                    byte[] buffer = Cliente.Receive(ref remoto);

                    string comando = Encoding.UTF8.GetString(buffer);
                    string[] comandoSeparado = comando.Split('|');

                    switch (comandoSeparado[0])
                    {
                        case nameof(Orden.ENLAZADO):
                            latidosEnviados = 0;
                            PaginaCambiada?.Invoke(Pagina.Conectado);
                            InformacionActualizada?.Invoke("ENLAZADO!");
                            break;

                        case nameof(Orden.REGISTROAPROBADO):
                            if (!latiendo)
                            {
                                latiendo = true;

                                GuardarRegistro(comandoSeparado[1], comandoSeparado[2], comandoSeparado[3]);

                                PaginaCambiada?.Invoke(Pagina.Conectado);
                                InformacionActualizada?.Invoke("Registro aprobado... ");
                                EnviarHearthbeat();

                                Thread hiloInternet = new(RevisarInternet);
                                hiloInternet.IsBackground = true;
                                hiloInternet.Start();
                            }
                            break;

                        case nameof(Orden.APAGAR):
                            escuchando = false;
                            InformacionActualizada?.Invoke("Esta computadora se apagará en unos segundos...");
                            Process.Start("shutdown", "/s /t 10");                             //s = Apagar
                            Thread.Sleep(10000);
                            break;

                        case nameof(Orden.REINICIAR):
                            escuchando = false;
                            PaginaCambiada?.Invoke(Pagina.Advertencia);
                            InformacionActualizada?.Invoke("Esta computadora se reiniciará en unos segundos...");
                            Process.Start("shutdown", "/r /t 10");                             //r = Reiniciar
                            Thread.Sleep(10000);
                            break;

                        case nameof(Orden.EDITARINFO):
                            if (comandoSeparado.Length == 3 && Registro != null)
                            {
                                PaginaCambiada?.Invoke(Pagina.Conectado);
                                Ip = IPAddress.Parse(Registro.IpServidor);
                                GuardarRegistro(comandoSeparado[1], comandoSeparado[2], Registro.MAC);
                                //RegistroActualizado?.Invoke(Registro, $"Se indico un cambio de id a {comandoSeparado[1]}"); ////////
                            }
                            break;

                        case nameof(Orden.OLVIDAR):
                            escuchando = false;
                            latiendo = false;
                            TimerBeat.Stop();
                            RegistroEliminado?.Invoke("Registro eliminado");
                            PaginaCambiada?.Invoke(Pagina.Registro);
                            File.Delete(filename);
                            break;
                    }
                }
                catch { if (!escuchando) break; }
            }

        }

        private void RevisarInternet()
        {
            while (true)
            {
                Thread.Sleep(5000);
                if (Registro != null)
                {
                    if (HacerPing())
                    {
                        if (!internet)
                        {
                            internet = true;
                            EstadoInternetCambiado?.Invoke(internet);
                        }
                        ultimoPing = DateTime.Now;

                        IPEndPoint remoto = new IPEndPoint(IPAddress.Parse(Registro.IpServidor), Registro.PuertoServidor);
                        string comando = $"{Orden.INTERNET}|{Registro.NombreAsignado}";
                        byte[] buffer = Encoding.UTF8.GetBytes(comando);
                        Cliente.Send(buffer, buffer.Length, remoto);

                    }
                    if (DateTime.Now - ultimoPing >= TimeSpan.FromSeconds(30) && internet)
                    {
                        internet = false;
                        EstadoInternetCambiado?.Invoke(internet);
                    }
                }
                else return;
            }
        }

        private bool HacerPing()
        {
            try
            {
                if (latiendo)
                {
                    using Ping ping = new();
                    PingReply respuesta = ping.Send("8.8.8.8", 1000);
                    return respuesta.Status == IPStatus.Success;
                }
                return false;
            }
            catch { return false; }
        }

        public void EnviarHearthbeat()
        {
            if (TimerBeat == null)
            {
                TimerBeat = new System.Timers.Timer(TimeSpan.FromSeconds(5));

                TimerBeat.Elapsed += TimerBeat_Tick;
                TimerBeat.AutoReset = true;
            }
            TimerBeat.Start();
        }


        private void TimerBeat_Tick(object? sender, EventArgs e)
        {
            if (Registro != null)
            {
                try
                {
                    IPEndPoint remoto = new IPEndPoint(IPAddress.Parse(Registro.IpServidor), Registro.PuertoServidor);
                    string comando = $"{Orden.HEARTHBEAT}|{Registro.NombreAsignado}";
                    byte[] buffer = Encoding.UTF8.GetBytes(comando);
                    Cliente.Send(buffer, buffer.Length, remoto);

                    latidosEnviados++;

                    if (latidosEnviados >= 5)
                    {
                        InformacionActualizada?.Invoke("Se ha perdido la conexión con el servidor");
                    }
                }
                catch { }
            }
        }
    }
}
