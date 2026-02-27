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
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Windows.Input;
using System.Windows.Media.Animation;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Servidor.ViewModels
{
    public class ServerViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        public event EventHandler? CanExecuteChanged;

        public ICommand RegistrarCommand { set; get; }
        public PcInfo ComputadoraSeleccionada { get; set; }
        public ObservableCollection<PcInfo> Computadoras { get; set; } = new();
        public ObservableCollection<PcInfo> HistorialComputadoras { get; set; } = new();

        IPAddress ip = IPAddress.Parse("127.0.0.1");
        int puerto = 60000;
        string mensaje = "";
        public string Info { set; get; } = "Error";
        UdpClient Server { get; set; }

        public ServerViewModel()
        {
            IPEndPoint endpoint = new IPEndPoint(ip, puerto);

            AbrirOC();
            RegistrarCommand = new RelayCommand<PcInfo>(Registrar);


            Server = new UdpClient(endpoint);
            Thread hiloEscuchar = new(RecibirMensajes);
            hiloEscuchar.IsBackground = true;
            hiloEscuchar.Start();

        }


        public void RecibirMensajes()
        {
            while (true)
            {
                IPEndPoint remoto = new IPEndPoint(IPAddress.None, 0);
                byte[] buffer = Server.Receive(ref remoto);

                string comando = Encoding.UTF8.GetString(buffer);
                string[] comandoSeparado = comando.Split('|');

                if (comandoSeparado[0] == "REGISTRO" && comandoSeparado[1] != null)
                {
                    //Mostrar solicitud de registro
                    //IrRegistrar
                    IrRegistrar(remoto, comandoSeparado[1]);
                    Info = "Mensaje recibido";
                    PropertyChanged?.Invoke(this, new(nameof(Info)));

                }
                else if (comandoSeparado[0] == "HEARTHBEAT" && comandoSeparado[1] != null)
                {

                }
            }
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

        public void Registrar(PcInfo pc)
        {
            if (pc != null)
            {
                pc.PrimeraConexion = DateTime.Now;
                //Confirmar registro
                EnviarMensajes("REGISTROAPROBADO", pc);
                Computadoras.Add(pc);
                GuardarOC();
                //Guardarla en la lista de historial
                if (!HistorialComputadoras.Contains(pc))
                {
                    HistorialComputadoras.Add(pc);
                }
            }
            pc = null;
        }

        public void EnviarMensajes(string comando, PcInfo pc)
        {
            if (comando == "REGISTROAPROBADO")
            {
                Server.Connect(pc.Ip, pc.Puerto);
                string mensaje = $"{comando}|{pc.Identificador}@{pc.Ip}:{pc.Puerto}";
                byte[] buffer = Encoding.UTF8.GetBytes(mensaje);

                IPEndPoint destino = new IPEndPoint(IPAddress.Parse(pc.Ip), pc.Puerto);
                Server.Send(buffer, buffer.Length);
            }

        }

        string computadorasFilename = "computadoras.json";
        string historialFilename = "computadoras.json";
        private void GuardarOC()
        {
            var computadoras = new List<PcInfo> { };
            foreach (var c in Computadoras)
            {
                computadoras.Add(c);
            }
            string jsonString = JsonSerializer.Serialize(computadoras);
            File.WriteAllText(computadorasFilename, jsonString);

        }

        private void AbrirOC()
        {
            if (File.Exists(computadorasFilename))
            {
                var jsonString = File.ReadAllText(computadorasFilename);
                var observableCollection = JsonSerializer.Deserialize<ObservableCollection<PcInfo>>(jsonString);

                if (observableCollection != null)
                {
                    //Preparar para file == computadoras.json
                    if (true)
                    {
                        foreach (var c in observableCollection)
                        {
                            Computadoras.Add(c);
                        }
                    }
                }
            }
        }
    }
}
