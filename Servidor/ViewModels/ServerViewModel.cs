using Servidor.Models.Entities;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Windows.Input;
using System.Windows.Media.Animation;

namespace Servidor.ViewModels
{
    public class ServerViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        public event EventHandler? CanExecuteChanged;
        public PcInfo ComputadoraSeleccionada { get; set; }
        public ObservableCollection<PcInfo> Computadoras { get; set; }
        public ObservableCollection<PcInfo> HistorialComputadoras { get; set; }

        IPAddress ip = IPAddress.Parse("127.0.0.1");
        int puerto = 60000;
        string mensaje = "";
        UdpClient Server { get; set; }

        public ServerViewModel()
        {
            IPEndPoint endpoint = new IPEndPoint(ip, puerto);

            UdpClient server = new UdpClient(endpoint);
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
                    //Registrar(remoto, comandoSeparado[1]);


                }
                else if (comandoSeparado[0] == "HEARTHBEAT" && comandoSeparado[1] != null)
                {

                }
            }

            //switch (comandoSeparado[0])
            //{
            //    case "CONECTADO":
            //        ;
            //        break;
            //    case "REGISTROAPROBADO":
            //    case "APAGAR":
            //    case "REINICIAR":
            //    case "CAMBIARID":
            //    default:
            //        mensaje = "Comando desconocido";
            //        break;
            //}
        }

        private void IrRegistrar(IPEndPoint remoto, string identificador)
        {
            PcInfo pc = new PcInfo
            {
                Nombre = identificador,
                Ip = remoto.Address,
                Puerto = remoto.Port,
                EstadoConectado = false
            };

            ComputadoraSeleccionada = pc;
            //Cambiar de vista o mostrar modal de registro con botones para aceptar o rechazar
        }

        public void Registrar(PcInfo pc)
        {
            if (pc != null)
            {
                //Confirmar registro
                EnviarMensajes("REGISTROAPROBADO", pc);
                Computadoras.Add(pc);
                //Guardarla en la lista de historial
                if(!HistorialComputadoras.Contains(pc))
                {
                    HistorialComputadoras.Add(pc);
                }
            }
        }

        public void EnviarMensajes(string comando, PcInfo pc)
        {
            if (comando == "REGISTROAPROBADO")
            {
                string mensaje = $"{comando}|{pc.Identificador}@{pc.Ip}:{pc.Puerto}";
                byte[] buffer = Encoding.UTF8.GetBytes(mensaje);

                IPEndPoint destino = new IPEndPoint(pc.Ip, pc.Puerto);
                Server.Send(buffer, buffer.Length, destino);
            }

        }
    }
}
