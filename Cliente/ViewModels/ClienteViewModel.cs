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
using System.Windows.Threading;

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
        public string Info { set; get; } = "Error";
        public ServerInfo Registro { set; get; }
        public int LatidosEnviados { set; get; } //Esta propiedad no es necesaria, solo es para tener una referencia desde la vista

        public ICommand EnviarRegistroCommand { get; set; }
        private DispatcherTimer TimerBeat;

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

        public void EnviarRegistro()
        {
            if (IPAddress.IsValid(IpPorValidar) && !string.IsNullOrEmpty(Nombre))
            {
                Ip = IPAddress.Parse(IpPorValidar);


                //IPEndPoint endpoint = new(IPAddress.Any, 60001);
                //Cliente = new UdpClient(endpoint);

                IPEndPoint remoto = new IPEndPoint(Ip, 60000);

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
            TimerBeat = new DispatcherTimer();
            TimerBeat.Interval = TimeSpan.FromSeconds(5);
            TimerBeat.Tick += TimerBeat_Tick;
            TimerBeat.Start();
            latiendo = true;
        }

        private void TimerBeat_Tick(object? sender, EventArgs e)
        {
            IPEndPoint remoto = new IPEndPoint(IPAddress.Parse(Registro.Ip), Registro.Puerto);
            string comando = $"HEARTHBEAT|{Registro.NombreAsignado}";
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

        public void RecibirMensajes()
        {
            Info = "Escuchando mensajes";
            PropertyChanged?.Invoke(this, new(nameof(Info)));
            while (true)
            {
                //try
                //{
                IPEndPoint remoto = new(IPAddress.Any, 0);
                byte[] buffer = Cliente.Receive(ref remoto);

                string comando = Encoding.UTF8.GetString(buffer);
                string[] comandoSeparado = comando.Split('|');

                switch (comandoSeparado[0])
                {
                    case "CONECTADO":
                        Info = "CONECTADO!";
                        LatidosEnviados = 0;
                        PropertyChanged?.Invoke(this, new(nameof(Info)));
                        break;
                    case "REGISTROAPROBADO":
                        Info = "Registro aprobado... ";
                        PropertyChanged?.Invoke(this, new(nameof(Info)));
                        //Serializar la ip y puerto
                        GuardarRegistro();
                        if (!latiendo)
                        {
                            App.Current.Dispatcher.Invoke(() =>
                            {
                                EnviarHearthbeat();
                            });
                        }
                        break;
                    case "APAGAR":
                    case "REINICIAR":
                    case "CAMBIARID": break;
                }
                //}
                //catch (Exception) { }
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
            Registro = registro;
            PropertyChanged?.Invoke(this, new(nameof(Registro)));
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
