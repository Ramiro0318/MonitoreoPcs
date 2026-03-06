using Cliente.Models.Entities;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Windows.Input;
using System.Windows.Threading;

namespace Cliente.ViewModels
{
    public enum Orden { REGISTROAPROBADO, REGISTRO, CONECTADO, APAGAR, REINICIAR, CAMBIARID, OLVIDAR, HEARTHBEAT }
    public class ClienteViewModel : INotifyPropertyChanged
    {

        public event PropertyChangedEventHandler? PropertyChanged;
        public ICommand EnviarRegistroCommand { get; set; }

        private DispatcherTimer TimerBeat;
        private int puerto = 60000; //Puerto de servidor
        private bool latiendo = false;
        string filename = "registro.json";
        public int LatidosEnviados { set; get; } //Esta propiedad no es necesaria, solo es para tener una referencia desde la vista
        public string IpPorValidar { get; set; } //Esta propiedad es para poder aplicar un IsValid para validar la ip
        public string Nombre { set; get; } = null!;
        public string Info { set; get; }
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
                //No estoy seguro si es necesario que la ip sea guardada en el registro, si lo veo necesario, lo haré después
                //Si no es necesario, puedo instanciarlo fuera del if y eliminar el duplicado en EnviarRegistro()
                //Empieza a escuchar
                Thread hiloEscuchar = new(RecibirMensajes);
                hiloEscuchar.IsBackground = true;
                hiloEscuchar.Start();

                EnviarHearthbeat();

            }
        }

        bool escuchando = false;

        public void EnviarRegistro()
        {
            if (!escuchando && IPAddress.IsValid(IpPorValidar) && !string.IsNullOrEmpty(Nombre))
            {
                try
                {
                    Ip = IPAddress.Parse(IpPorValidar);
                    IPEndPoint remoto = new IPEndPoint(Ip, puerto);

                    string comando = $"{Orden.REGISTRO}|{Nombre}";
                    byte[] buffer = Encoding.UTF8.GetBytes(comando);
                    Cliente.Send(buffer, buffer.Length, remoto);
                    Info = "Solicitud de registro enviada";

                    //Empieza a escuchar
                    Thread hiloEscuchar = new(RecibirMensajes);
                    hiloEscuchar.IsBackground = true;
                    hiloEscuchar.Start();
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

                LatidosEnviados++;
                PropertyChanged?.Invoke(this, new(nameof(LatidosEnviados)));

                if (LatidosEnviados >= 5)
                {
                    Info = "Se ha perdido la conexión con el servidor";
                    PropertyChanged?.Invoke(this, new(nameof(Info)));
                    //Se cambia de estado a desconectado
                }
            }
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
                        case "CONECTADO":
                            App.Current.Dispatcher.Invoke(() =>
                            {
                                Info = "CONECTADO!";
                                LatidosEnviados = 0;
                                PropertyChanged?.Invoke(this, new(nameof(Info)));
                            });
                            break;

                        case "REGISTROAPROBADO":
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
                            }
                            break;

                        case "APAGAR":
                            escuchando = false;
                            App.Current.Dispatcher.Invoke(() =>
                            {
                                Info = "Esta computadora se apagará en unos segundos...";
                                PropertyChanged?.Invoke(this, new(nameof(Info)));
                            });
                            Process.Start("shutdown", "/s /t 10");                             //s = Apagar
                            Thread.Sleep(10000);
                            break;

                        case "REINICIAR":
                            escuchando = false;
                            App.Current.Dispatcher.Invoke(() =>
                            {
                                Info = "Esta computadora se reiniciará en unos segundos...";
                                PropertyChanged?.Invoke(this, new(nameof(Info)));
                            });
                            Process.Start("shutdown", "/r /t 10");                             //r = Reiniciar
                            Thread.Sleep(10000);
                            break;

                        case "CAMBIARID":
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

                        case "OLVIDAR":
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
