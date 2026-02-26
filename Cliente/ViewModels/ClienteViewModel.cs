using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Cliente.ViewModels
{
    public class ClienteViewModel
    {

        bool registrada;
        private int puerto = 65000;
        public string Nombre { set; get; } = "Pc-local";
        public IPAddress Ip { set; get; } = IPAddress.Parse("127.0.0.1"); // IpServidor
        UdpClient Cliente { get; set; }

        public ClienteViewModel()
        {
            //IPEndPoint endpoint = new IPEndPoint(Ip, puerto);
            Cliente = new UdpClient();

            EnviarMensajes(Ip);
        }

        public void EnviarMensajes(IPAddress ipServer)
        {
            IPEndPoint remoto = new IPEndPoint(ipServer, puerto);
            Ip = ipServer;

            if (!registrada)
            {
                string comando = $"REGISTRO|{Nombre}";
                byte[] buffer = Encoding.UTF8.GetBytes(Nombre);

                Cliente.Send(buffer, buffer.Length, remoto);
            }
            if (registrada /*&&*/ )
            {
                string comando = $"HEARTHBEAT|{Nombre}";
                byte[] buffer = Encoding.UTF8.GetBytes(Nombre);
                Cliente.Send(buffer, buffer.Length, remoto);
                //Averiguar como enviar cada x segundos.
            }
            Thread hiloEscuchar = new(RecibirMensajes);
            hiloEscuchar.IsBackground = true;
            hiloEscuchar.Start();
        }

        public void RecibirMensajes() 
        {

            while (true)
            {
                IPEndPoint remoto = new(IPAddress.None, 0);
                byte[] buffer = Cliente.Receive(ref remoto);

                string comando = Encoding.UTF8.GetString(buffer);
                string[] comandoSeparado = comando.Split('|');

                switch (comandoSeparado[0])
                {
                    case "CONECTADO":break;
                    case "REGISTROAPROBADO":
                        //Hacer lógica para confirmar la conexión
                        //EnviarMensajes()
                    case "APAGAR":
                    case "REINICIAR":
                    case "CAMBIARID": break;
                }
            }

        }


    }
}
