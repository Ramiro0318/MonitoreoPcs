using Cliente.Models.Entities;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace Cliente.ViewModels
{
    public enum Orden { REGISTROAPROBADO, REGISTRO, ENLAZADO, APAGAR, REINICIAR, CAMBIARID, OLVIDAR, HEARTHBEAT, INTERNET }
    public class ClienteViewModel : INotifyPropertyChanged
    {

        public event PropertyChangedEventHandler? PropertyChanged;
        public ICommand EnviarRegistroCommand { get; set; }

        private DispatcherTimer TimerBeat;
        private int puerto = 60000; //Puerto de servidor
        private bool latiendo;
        private bool escuchando = false;
        string filename = "registro.json";
        private int latidosEnviados;
        private DateTime ultimoPing = DateTime.Now;
        public string IpPorValidar { get; set; }
        public string Nombre { set; get; } = null!;
        public string Info { set; get; }
        public bool Internet { set; get; }
        public IPAddress Ip { set; get; } // IpServidor
        public Info? Registro { set; get; }
        UdpClient Cliente { get; set; }


        public ClienteViewModel()
        {
            EnviarRegistroCommand = new RelayCommand(EnviarRegistro);
            //Deserializar el registro
            AbrirRegistro();

            IPEndPoint endpoint = new(IPAddress.Any, 60001);
            Cliente = new UdpClient(endpoint);
            if (Registro != null)
            {
                Thread hiloEscuchar = new(RecibirMensajes);
                hiloEscuchar.IsBackground = true;
                hiloEscuchar.Start();

                Thread hiloInternet = new(RevisarInternet);
                hiloInternet.IsBackground = true;
                hiloInternet.Start();

                EnviarHearthbeat();

            }
        }


        public void EnviarRegistro()
        {
            if (IPAddress.IsValid(IpPorValidar) && !string.IsNullOrEmpty(Nombre))
            {
                try
                {
                    Ip = IPAddress.Parse(IpPorValidar);
                    IPEndPoint remoto = new IPEndPoint(Ip, puerto);

                    string comando = $"{Orden.REGISTRO}|{Nombre}";
                    byte[] buffer = Encoding.UTF8.GetBytes(comando);
                    Cliente.Send(buffer, buffer.Length, remoto);
                    Info = "Solicitud de registro enviada";

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
            PropertyChanged?.Invoke(this, new(nameof(Info)));
        }


        public void EnviarHearthbeat()
        {

            TimerBeat = new DispatcherTimer();
            TimerBeat.Interval = TimeSpan.FromSeconds(5);
            TimerBeat.Tick += TimerBeat_Tick;
            TimerBeat.Start();
        }

        private void TimerBeat_Tick(object? sender, EventArgs e)
        {
            if (Registro != null)
            {
                IPEndPoint remoto = new IPEndPoint(IPAddress.Parse(Registro.IpServidor), Registro.PuertoServidor);
                string comando = $"{Orden.HEARTHBEAT}|{Registro.NombreAsignado}";
                byte[] buffer = Encoding.UTF8.GetBytes(comando);
                Cliente.Send(buffer, buffer.Length, remoto);

                latidosEnviados++;
                PropertyChanged?.Invoke(this, new(nameof(latidosEnviados)));

                if (latidosEnviados >= 5)
                {
                    Info = "Se ha perdido la conexión con el servidor";
                    PropertyChanged?.Invoke(this, new(nameof(Info)));
                }
            }
        }


        private void RevisarInternet()
        {
            while (true)
            {
                Thread.Sleep(5000);
                if (Registro != null)
                {
                    IPEndPoint remoto = new IPEndPoint(IPAddress.Parse(Registro.IpServidor), Registro.PuertoServidor);
                    if (HacerPing())
                    {
                        string comando = $"{Orden.INTERNET}|{Registro.NombreAsignado}";
                        byte[] buffer = Encoding.UTF8.GetBytes(comando);
                        Cliente.Send(buffer, buffer.Length, remoto);
                        ultimoPing = DateTime.Now;
                    }
                    if (DateTime.Now - ultimoPing >= TimeSpan.FromSeconds(30) && Internet)
                    {
                        App.Current.Dispatcher.Invoke(() =>
                        {
                            Internet = false;
                            PropertyChanged?.Invoke(this, new(nameof(Internet)));
                        });
                    }
                    Thread.Sleep(5000);
                }
                else return;
            }
        }

        private bool HacerPing()
        {
            try
            {
                using Ping ping = new();
                PingReply respuesta = ping.Send("8.8.8.8", 1000);
                return respuesta.Status == IPStatus.Success;
            }
            catch { return false; }
        }

        public void RecibirMensajes()
        {
            escuchando = true;
            Info = "Escuchando mensajes";
            PropertyChanged?.Invoke(this, new(nameof(Info)));
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
                            App.Current.Dispatcher.Invoke(() =>
                            {
                                Info = "ENLAZADO!";
                                latidosEnviados = 0;
                                PropertyChanged?.Invoke(this, new(nameof(Info)));
                            });
                            break;

                        case nameof(Orden.REGISTROAPROBADO):
                            if (!latiendo)
                            {
                                latiendo = true;
                                GuardarRegistro();
                                App.Current.Dispatcher.Invoke(() =>
                                {
                                    Info = "Registro aprobado... ";
                                    PropertyChanged?.Invoke(this, new(nameof(Info)));
                                    //Serializar la ip y puerto
                                    EnviarHearthbeat();
                                });
                                Thread hiloInternet = new(RevisarInternet);
                                hiloInternet.IsBackground = true;
                                hiloInternet.Start();
                            }
                            break;

                        case nameof(Orden.APAGAR):
                            escuchando = false;
                            App.Current.Dispatcher.Invoke(() =>
                            {
                                Info = "Esta computadora se apagará en unos segundos...";
                                PropertyChanged?.Invoke(this, new(nameof(Info)));
                            });
                            Process.Start("shutdown", "/s /t 10");                             //s = Apagar
                            Thread.Sleep(10000);
                            break;

                        case nameof(Orden.REINICIAR):
                            escuchando = false;
                            App.Current.Dispatcher.Invoke(() =>
                            {
                                Info = "Esta computadora se reiniciará en unos segundos...";
                                PropertyChanged?.Invoke(this, new(nameof(Info)));
                            });
                            Process.Start("shutdown", "/r /t 10");                             //r = Reiniciar
                            Thread.Sleep(10000);
                            break;

                        case nameof(Orden.CAMBIARID):
                            if (comandoSeparado.Length == 2 && Registro != null)
                            {
                                Registro.NombreAsignado = comandoSeparado[1];
                                App.Current.Dispatcher.Invoke(() =>
                                {
                                    Info = $"Se indico un cambio de id a {comandoSeparado[1]}";
                                    GuardarRegistro();
                                    PropertyChanged?.Invoke(this, new(nameof(Info)));
                                    PropertyChanged?.Invoke(this, new(nameof(Registro)));
                                });
                            }
                            break;

                        case nameof(Orden.OLVIDAR):
                            escuchando = false;
                            App.Current.Dispatcher.Invoke(() =>
                            {
                                Info = "Registro eliminado";
                                latiendo = false;
                                TimerBeat.Stop();
                                Registro = null;
                                PropertyChanged?.Invoke(this, new(nameof(Info)));
                                PropertyChanged?.Invoke(this, new(nameof(Registro)));
                            });
                            File.Delete(filename);
                            break;
                    }
                }
                catch { if (!escuchando) break; }
            }

        }

        private void GuardarRegistro()
        {
            if (Registro == null)
            {
                var registro = new Info
                {
                    NombreAsignado = Nombre,
                    IpServidor = Ip.ToString(),
                    PuertoServidor = puerto
                };
                Registro = registro;
            }
            string jsonString = JsonSerializer.Serialize(Registro);
            File.WriteAllText(filename, jsonString);
            PropertyChanged?.Invoke(this, new(nameof(Registro)));
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
                    PropertyChanged?.Invoke(this, new(nameof(Registro)));
                }
            }
        }
    }
}
