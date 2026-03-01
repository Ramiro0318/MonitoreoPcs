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
        public string Info { set; get; } = "Error";

        string computadorasFilename = "computadoras.json";
        string historialFilename = "historial.json";
        IPAddress ip = IPAddress.Parse("192.168.1.67");
        int puerto = 60000;
        string mensaje = "";
        UdpClient Server { get; set; }
        public int LatidosRecibidos { get; set; }

        public ServerViewModel()
        {
            IPEndPoint endpoint = new IPEndPoint(ip, puerto);

            AbrirOC(Computadoras, computadorasFilename);
            AbrirOC(HistorialComputadoras, historialFilename);
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

                    PcInfo pc = new PcInfo
                    {
                        Nombre = comandoSeparado[1],
                        Ip = remoto.Address.ToString(),
                        Puerto = remoto.Port,
                        EstadoConectado = true,
                        UltimoLatido = DateTime.Now
                    };
                    EnviarMensajes("CONECTADO", pc);
                }
                foreach (var pc in Computadoras) //No estoy seguro si lo mas eficiente es anidar otro ciclo para comprobar o hacer un timer dedicado que revise periodicamente
                {
                    if (DateTime.Now - pc.UltimoLatido >= TimeSpan.FromSeconds(30))
                    {
                        pc.EstadoConectado = false;
                        PropertyChanged?.Invoke(this, new(nameof(Computadoras)));
                    }
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
                //Confirmar registro
                EnviarMensajes("REGISTROAPROBADO", pc);
                if (!Computadoras.Any(x => x.Identificador == pc.Identificador))
                {
                    Computadoras.Add(pc);
                    GuardarOC(Computadoras, computadorasFilename);
                }
                pc.HoraConexion = DateTime.Now;

                //Guardarla en la lista de historial
                HistorialComputadoras.Add(pc);
                GuardarOC(HistorialComputadoras, historialFilename);
                //También aplicar el guardado para Historial
            }
            pc = new();
        }

        public void EnviarMensajes(string comando, PcInfo pc)
        {

            if ((comando == "REGISTROAPROBADO" || comando == "CONECTADO") && pc != null) //Verificar si pc es diferente de nulo si es necesario
            {
                //Creo que no es necesario el connect
                Server.Connect(pc.Ip, pc.Puerto);
                string mensaje = $"{comando}|{pc.Identificador}@{pc.Ip}:{pc.Puerto}";
                byte[] buffer = Encoding.UTF8.GetBytes(mensaje);

                IPEndPoint destino = new IPEndPoint(IPAddress.Parse(pc.Ip), pc.Puerto);  //De momento no se usa el endpoint
                Server.Send(buffer, buffer.Length);
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
