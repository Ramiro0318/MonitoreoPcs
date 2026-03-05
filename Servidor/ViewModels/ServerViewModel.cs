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
    public class ServerViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        public event EventHandler? CanExecuteChanged;

        public ICommand RegistrarCommand { set; get; }
        public ICommand RechazarCommand { set; get; }
        public ICommand EnviarComandoCommand { set; get; }
        public ICommand IrEditarCommand { set; get; }
        public ICommand EditarCommand { set; get; }
        public ICommand EliminarCommand { set; get; }

        private DispatcherTimer TimerEstado;
        private string computadorasFilename = "computadoras.json";
        private string historialFilename = "historial.json";
        private IPAddress ip = IPAddress.Parse("192.168.1.67");
        private int puerto = 60000;
        public int LatidosRecibidos { set; get; }
        public string Info { set; get; } = "Error";
        public PcInfo? ComputadoraSeleccionada { set; get; }
        public PcInfo? Clon { set; get; }
        public ObservableCollection<PcInfo> Computadoras { set; get; } = new();
        public ObservableCollection<PcInfo> HistorialComputadoras { set; get; } = new();
        UdpClient Server { set; get; }

        public ServerViewModel()
        {
            IPEndPoint endpoint = new IPEndPoint(ip, puerto);

            AbrirOC(Computadoras, computadorasFilename);
            AbrirOC(HistorialComputadoras, historialFilename);


            RegistrarCommand = new RelayCommand<PcInfo>(Registrar);
            RechazarCommand = new RelayCommand(Rechazar);
            EnviarComandoCommand = new RelayCommand<string>(EnviarMensajes);
            IrEditarCommand = new RelayCommand(IrEditar);
            EditarCommand = new RelayCommand<PcInfo>(Editar);
            EliminarCommand = new RelayCommand(Eliminar);


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
            //Cambiar de vista o mostrar modal de registro con botones para aceptar o rechazar
        }

        private void Registrar(PcInfo pc)
        {
            if (pc != null)
            {
                ComputadoraSeleccionada = pc;
                EnviarMensajes("REGISTROAPROBADO");
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
            //Depende de cómo esté diseñado, el método rechazar se puede reutilizar como cancelar al editar
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
                    var registroHistorial = HistorialComputadoras.Where(x => x.Identificador == identificador).ToList();
                    registroHistorial.ForEach(x => x.Nombre = clon.Nombre);
                    GuardarOC(Computadoras, computadorasFilename);
                    GuardarOC(HistorialComputadoras, historialFilename);
                    //Dependiendo de el diseño se debe de mostrar la actualización de las listas de una forma o de otra.
                    //Se pueden ver los cambios al cerrar y volver a abrir el programa
                    //La manera más sencilla de refrescar sería limpiando las OC aquí y volverlas a cargar con el método de deserializar
                    ComputadoraSeleccionada = clon;
                    PropertyChanged?.Invoke(this, new(nameof(ComputadoraSeleccionada)));
                    EnviarMensajes("CAMBIARID");

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
                EnviarMensajes("OLVIDAR");
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

                    if (comandoSeparado[0] == "REGISTRO" && comandoSeparado[1] != null)
                    {
                        //Mostrar solicitud de registro
                        IrRegistrar(remoto, comandoSeparado[1]);
                        Info = "Mensaje recibido";
                        PropertyChanged?.Invoke(this, new(nameof(Info)));

                    }
                    else if (comandoSeparado[0] == "HEARTHBEAT" && comandoSeparado[1] != null)
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
                                    HistorialComputadoras.Add(pc);
                                    GuardarOC(HistorialComputadoras, historialFilename);
                                });
                                pc.EstadoConectado = true;
                            }
                            Info = "true";
                            PropertyChanged?.Invoke(this, new(nameof(Info)));
                            ComputadoraSeleccionada = pc;
                            EnviarMensajes("CONECTADO");
                        }
                    }
                }
                catch (Exception) { }
            }
        }

        private void TimerEstado_Tick(object? sender, EventArgs e)
        {
            foreach (var pc in Computadoras)
            {
                if (DateTime.Now - pc.UltimoLatido >= TimeSpan.FromSeconds(30) && pc.EstadoConectado)
                {
                    pc.EstadoConectado = false;
                    Info = "false";
                    PropertyChanged?.Invoke(this, new(nameof(Info)));
                }
            }
        }

        public void EnviarMensajes(string comando)
        {
            if (ComputadoraSeleccionada != null && !string.IsNullOrWhiteSpace(comando))
            {
                var pc = ComputadoraSeleccionada;
                if (comando == "REGISTROAPROBADO" || comando == "CONECTADO" || comando == "APAGAR" || comando == "REINICIAR" || comando == "CAMBIARID" || comando == "OLVIDAR")
                {
                    Info = $"a {comando.ToLower()} {pc.Nombre}";
                    PropertyChanged?.Invoke(this, new(nameof(Info)));

                    string mensaje = comando == "CAMBIARID" ? $"{comando}|{pc.Nombre}" : mensaje = comando;
                    byte[] buffer = Encoding.UTF8.GetBytes(mensaje);

                    IPEndPoint destino = new IPEndPoint(IPAddress.Parse(pc.Ip), pc.Puerto);
                    Server.Send(buffer, buffer.Length, destino);
                }
            }
        }


        private void GuardarOC(ObservableCollection<PcInfo> oc, string filename)
        {
            var computadoras = new List<PcInfo> { };
            foreach (var c in oc)
            {
                computadoras.Add(c);
            }
            string jsonString = JsonSerializer.Serialize(computadoras);
            File.WriteAllText(filename, jsonString);

        }

        private void AbrirOC(ObservableCollection<PcInfo> oc, string filename)
        {
            if (File.Exists(filename))
            {
                var jsonString = File.ReadAllText(filename);
                var observableCollection = JsonSerializer.Deserialize<ObservableCollection<PcInfo>>(jsonString);

                if (observableCollection != null)
                {
                    foreach (var c in observableCollection)
                    {
                        oc.Add(c);
                    }
                }
            }
        }
    }
}
