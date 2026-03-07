using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using Servidor.Models.Entities;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Printing;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Servidor.ViewModels
{
    public enum Orden { REGISTROAPROBADO, REGISTRO, CONECTADO, APAGAR, REINICIAR, CAMBIARID, OLVIDAR, HEARTHBEAT }
    public class ServerViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        public ICommand RegistrarCommand { set; get; }
        public ICommand RechazarCommand { set; get; }
        public ICommand EnviarComandoCommand { set; get; }
        public ICommand IrEditarCommand { set; get; }
        public ICommand EditarCommand { set; get; }
        public ICommand EliminarCommand { set; get; }
        public ICommand LimpiarCommand { set; get; }

        private DispatcherTimer TimerEstado;
        private string computadorasFilename = "computadoras.json";
        private string conexionesFilename = "conexiones.json";
        private string comandosFilename = "comandos.json";
        private IPAddress ip = IPAddress.Parse("192.168.1.67");
        private int puerto = 60000;
        public int LatidosRecibidos { set; get; }
        public string? Info { set; get; }
        public PcInfo? ComputadoraSeleccionada { set; get; }
        public PcInfo? ComputadoraResponder { set; get; }
        public PcInfo? Clon { set; get; }
        public ObservableCollection<PcInfo> Computadoras { set; get; } = new();
        public ObservableCollection<PcInfo> HistorialConexiones { set; get; } = new();
        public ObservableCollection<ComandoInfo> HistorialComandos { set; get; } = new();
        UdpClient Server { set; get; }

        public ServerViewModel()
        {
            IPEndPoint endpoint = new IPEndPoint(ip, puerto);

            AbrirOC(Computadoras, computadorasFilename);
            AbrirOC(HistorialConexiones, conexionesFilename);
            AbrirOC(HistorialComandos, comandosFilename);


            RegistrarCommand = new RelayCommand<PcInfo>(Registrar);
            RechazarCommand = new RelayCommand(Rechazar);
            EnviarComandoCommand = new RelayCommand<Orden>(EnviarMensajes);
            IrEditarCommand = new RelayCommand(IrEditar);
            EditarCommand = new RelayCommand<PcInfo>(Editar);
            EliminarCommand = new RelayCommand(Eliminar);
            LimpiarCommand = new RelayCommand<string>(LimpiarOC);


            Server = new UdpClient(endpoint);
            Thread hiloEscuchar = new(RecibirMensajes);
            hiloEscuchar.IsBackground = true;
            hiloEscuchar.Start();


            TimerEstado = new DispatcherTimer();
            TimerEstado.Interval = TimeSpan.FromSeconds(1);
            TimerEstado.Tick += TimerEstado_Tick;
            TimerEstado.Start();
        }
        
        public void IrRegistrar(IPEndPoint remoto, string identificador)
        {
            PcInfo pc = new PcInfo
            {
                Nombre = identificador,
                Ip = remoto.Address.ToString(),
                Puerto = remoto.Port,
                EstadoConectado = false,
            };

            ComputadoraSeleccionada = pc;
            PropertyChanged?.Invoke(this, new(nameof(ComputadoraSeleccionada)));
        }

        private void Registrar(PcInfo pc)
        {
            if (pc != null)
            {
                ComputadoraSeleccionada = pc;
                EnviarMensajes(Orden.REGISTROAPROBADO);
                if (!Computadoras.Any(x => x.Identificador == pc.Identificador))
                {
                    Computadoras.Add(pc);
                    GuardarOC(Computadoras, computadorasFilename);
                }
            }
            ComputadoraSeleccionada = null;
            PropertyChanged?.Invoke(this, new(nameof(ComputadoraSeleccionada)));
        }

        private void Rechazar()
        {
            ComputadoraSeleccionada = null;
            Clon = null;
            PropertyChanged?.Invoke(this, new(nameof(ComputadoraSeleccionada)));
            PropertyChanged?.Invoke(this, new(nameof(Clon)));
        }

        private string? identificador;
        private void IrEditar()
        {
            if (ComputadoraSeleccionada != null)
            {
                identificador = ComputadoraSeleccionada.Identificador;
                Clon = new PcInfo
                {
                    Nombre = ComputadoraSeleccionada.Nombre,
                    Ip = ComputadoraSeleccionada.Ip,
                    Puerto = ComputadoraSeleccionada.Puerto,
                    HoraConexion = ComputadoraSeleccionada.HoraConexion,//Estas 3 no estoy seguro
                    UltimoLatido = ComputadoraSeleccionada.UltimoLatido,
                    EstadoConectado = ComputadoraSeleccionada.EstadoConectado

                };
                PropertyChanged?.Invoke(this, new(nameof(Clon)));
            }
        }

        private void Editar(PcInfo clon)
        {
            if (clon != null && !string.IsNullOrWhiteSpace(identificador))
            {
                var pcOriginal = Computadoras.FirstOrDefault(x => x.Identificador == identificador);
                if (pcOriginal != null && clon.Nombre != pcOriginal.Nombre)
                {
                    pcOriginal.Nombre = clon.Nombre;
                    var registroHistorial = HistorialConexiones.Where(x => x.Identificador == identificador).ToList();
                    registroHistorial.ForEach(x => x.Nombre = clon.Nombre);
                    GuardarOC(Computadoras, computadorasFilename);
                    GuardarOC(HistorialConexiones, conexionesFilename);

                    ComputadoraSeleccionada = clon;
                    PropertyChanged?.Invoke(this, new(nameof(ComputadoraSeleccionada)));
                    EnviarMensajes(Orden.CAMBIARID);

                }
                Clon = null;
                PropertyChanged?.Invoke(this, new(nameof(Clon)));
            }
            identificador = null;
        }

        private void Eliminar()
        {
            if (ComputadoraSeleccionada != null)
            {
                var pcOlviar = ComputadoraSeleccionada;
                EnviarMensajes(Orden.OLVIDAR);
                Computadoras.Remove(pcOlviar);
                GuardarOC(Computadoras, computadorasFilename);
                PropertyChanged?.Invoke(this, new(nameof(Computadoras)));
            }
        }

        public void RecibirMensajes()
        {
            while (true)
            {
                try
                {
                    IPEndPoint remoto = new IPEndPoint(IPAddress.Any, 0);
                    byte[] buffer = Server.Receive(ref remoto);

                    string comando = Encoding.UTF8.GetString(buffer);
                    string[] comandoSeparado = comando.Split('|');

                    if (comandoSeparado[0] == Orden.REGISTRO.ToString() && comandoSeparado.Length == 2)
                    {
                        //Mostrar solicitud de registro
                        App.Current.Dispatcher.BeginInvoke(() =>
                        {
                            IrRegistrar(remoto, comandoSeparado[1]);
                            Info = "Mensaje recibido";
                            PropertyChanged?.Invoke(this, new(nameof(Info)));
                        });

                    }
                    else if (comandoSeparado[0] == Orden.HEARTHBEAT.ToString() && comandoSeparado.Length == 2)
                    {
                        LatidosRecibidos++;
                        PropertyChanged?.Invoke(this, new(nameof(LatidosRecibidos)));

                        var pc = Computadoras.FirstOrDefault(x => x.Nombre == comandoSeparado[1]);
                        if (pc != null)
                        {
                            pc.UltimoLatido = DateTime.Now;
                            if (!pc.EstadoConectado)
                            {
                                pc.HoraConexion = DateTime.Now;
                                App.Current.Dispatcher.Invoke(() =>
                                {   //Guardar el historial en cada nueva conexión
                                    HistorialConexiones.Add(pc);
                                    GuardarOC(HistorialConexiones, conexionesFilename);
                                });
                                pc.EstadoConectado = true;
                            }
                            Info = "true";
                            PropertyChanged?.Invoke(this, new(nameof(Info)));
                            ComputadoraResponder = pc;
                            EnviarMensajes(Orden.CONECTADO);
                        }
                    }
                }
                catch { }
            }
        }

        private void TimerEstado_Tick(object? sender, EventArgs e)
        {
            foreach (var pc in Computadoras.ToList())
            {
                if (DateTime.Now - pc.UltimoLatido >= TimeSpan.FromSeconds(30) && pc.EstadoConectado)
                {
                    pc.EstadoConectado = false;
                }
            }
        }

        public void EnviarMensajes(Orden comando)
        {
            if ((ComputadoraSeleccionada != null || ComputadoraResponder != null) && comando != Orden.REGISTRO && comando != Orden.HEARTHBEAT)
            {
                var pc = comando != Orden.CONECTADO ? ComputadoraSeleccionada : ComputadoraResponder;
                if (comando != Orden.CONECTADO)
                {
                    HistorialComandos.Add(new ComandoInfo
                    {
                        Destino = pc.Identificador,
                        Comando = comando,
                        Fecha = DateTime.Now,
                        NuevoNombre = comando == Orden.CAMBIARID ? pc.Nombre : ""
                    });
                    GuardarOC(HistorialComandos, comandosFilename);
                }

                Info = $"a {comando.ToString()} {pc.Nombre}";

                string mensaje = comando == Orden.CAMBIARID ? $"{comando}|{pc.Nombre}" : comando.ToString();
                byte[] buffer = Encoding.UTF8.GetBytes(mensaje);
                IPEndPoint destino = new IPEndPoint(IPAddress.Parse(pc.Ip), pc.Puerto);
                Server.Send(buffer, buffer.Length, destino);

                PropertyChanged?.Invoke(this, new(nameof(Info)));
            }
        }


        private void GuardarOC<T>(ObservableCollection<T> oc, string filename)
        {
            string jsonString = JsonSerializer.Serialize(oc);
            File.WriteAllText(filename, jsonString);
        }

        private void AbrirOC<T>(ObservableCollection<T> oc, string filename)
        {
            if (File.Exists(filename))
            {
                var jsonString = File.ReadAllText(filename);
                var observableCollection = JsonSerializer.Deserialize<ObservableCollection<T>>(jsonString);

                if (observableCollection != null)
                {
                    foreach (var o in observableCollection)
                    {
                        oc.Add(o);
                    }
                }
            }
        }

        private void LimpiarOC(string Oc)
        {
            if (Oc == "conexiones")
            {
                HistorialConexiones.Clear();
                GuardarOC(HistorialConexiones, conexionesFilename);
            }
            else if (Oc == "comandos")
            {
                HistorialComandos.Clear();
                GuardarOC(HistorialComandos, comandosFilename);
            }
        }
    }
}
