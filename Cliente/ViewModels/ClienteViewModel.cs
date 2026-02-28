using Cliente.Models.Entities;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Windows.Input;

namespace Cliente.ViewModels
{
    public class ClienteViewModel : INotifyPropertyChanged
    {

        public event PropertyChangedEventHandler? PropertyChanged;

        private int puerto = 60000;
        private bool latiendo = false;

        public string IpPorValidar { get; set; } //Esta propiedad es para poder aplicar un IsValid para validar la ip
        public string Nombre { set; get; } = "Pc-local";

        public IPAddress Ip { set; get; } = IPAddress.Parse("127.0.0.1"); // IpServidor
        UdpClient Cliente { get; set; }
        public bool Registrada { set; get; }
        public string Info { set; get; } = "Error";
        public ServerInfo Registro { set; get; }

        public ICommand EnviarRegistroCommand { get; set; }
        public ClienteViewModel()
        {
            IPEndPoint endpoint = new IPEndPoint(Ip, 60001);
            //Deserializar el registro
            AbrirRegistro();
            EnviarRegistroCommand = new RelayCommand(EnviarRegistro);
            Cliente = new UdpClient(endpoint);

            if (Registro != null)
            {
                //Empieza a escuchar
                Thread hiloEscuchar = new(RecibirMensajes);
                hiloEscuchar.IsBackground = true;
                hiloEscuchar.Start();
            }
        }

        public void EnviarRegistro()
        {
            if (IPAddress.IsValid(IpPorValidar) && !string.IsNullOrEmpty(Nombre))
            {
                Ip = IPAddress.Parse(IpPorValidar);
                IPEndPoint remoto = new IPEndPoint(Ip, puerto);

                string comando = $"REGISTRO|{Nombre}";
                byte[] buffer = Encoding.UTF8.GetBytes(comando);
                Cliente.Send(buffer, buffer.Length, remoto);
                Info = "Solicitud de registro enviada";

                //Empieza a escuchar
                Thread hiloEscuchar = new(RecibirMensajes);
                hiloEscuchar.IsBackground = true;
                hiloEscuchar.Start();

            }
            PropertyChanged?.Invoke(this, new(nameof(Info)));
        }


        public void EnviarHearthbeat()
        {
            latiendo = true;
            while (latiendo)
            {
                IPEndPoint remoto = new IPEndPoint(Ip, puerto);
                string comando = $"HEARTHBEAT|{Nombre}";
                byte[] buffer = Encoding.UTF8.GetBytes(Nombre);
                Cliente.Send(buffer, buffer.Length, remoto);
                //Averiguar como enviar cada x segundos.
            }

        }

        public void RecibirMensajes()
        {
            while (true)
            {
                IPEndPoint remoto = new(IPAddress.Any, 0);
                Info = "Escuchando mensajes";
                PropertyChanged?.Invoke(this, new(nameof(Info)));
                byte[] buffer = Cliente.Receive(ref remoto);

                string comando = Encoding.UTF8.GetString(buffer);
                string[] comandoSeparado = comando.Split('|');

                switch (comandoSeparado[0])
                {
                    case "CONECTADO":
                        if (!latiendo)
                        {
                            EnviarHearthbeat();
                        }
                        break;
                    case "REGISTROAPROBADO":
                        Info = "Registro aprobado... ";
                        PropertyChanged?.Invoke(this, new(nameof(Info)));
                        //Serializar la ip y puerto
                        GuardarRegistro();
                        if (!latiendo)
                        {
                            EnviarHearthbeat();
                        }
                        break;
                    case "APAGAR":
                    case "REINICIAR":
                    case "CAMBIARID": break;
                }
            }

        }

        string filename = "registro.json";
        private void GuardarRegistro()
        {
            var registro = new ServerInfo
            {
                NombreAsignado = Nombre,
                Ip = Ip.ToString(),
                Puerto = puerto
            };
            string jsonString = JsonSerializer.Serialize(registro);
            File.WriteAllText(filename, jsonString);

        }

        private ServerInfo AbrirRegistro()
        {
            if (File.Exists(filename))
            {
                var jsonString = File.ReadAllText(filename);
                var registro = JsonSerializer.Deserialize<ServerInfo>(jsonString);
                if (registro != null)
                {
                    Registro = registro;
                }
            }
            return Registro;
        }
    }
}
