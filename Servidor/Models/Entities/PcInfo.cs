using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Net;
using System.Text;

namespace Servidor.Models.Entities
{
    public class PcInfo : INotifyPropertyChanged
    {
        private bool estadoEnlazado;
        private bool estadoInternet;
        private string nombre = null!;
        public string Nombre
        {
            get
            { return nombre; }
            set
            {
                if (nombre != value)
                {
                    nombre = value;
                    PropertyChanged?.Invoke(this, new(nameof(Nombre)));
                    PropertyChanged?.Invoke(this, new(nameof(Identificador)));
                }
            }
        }
        public string Ip { get; set; } = null!;
        //Agregar propiedad para la ip que el cliente tiene registrado del servidor.
        //public string IpServidor { get; set; } = null!;
        public int Puerto { get; set; }
        public DateTime? HoraConexion { get; set; }
        public DateTime? UltimoLatido { get; set; }
        public DateTime? UltimoPing { get; set; }
        public string Identificador => $"{Nombre}@{Ip}:{Puerto}";

        public bool EstadoEnlazado
        {
            get { return estadoEnlazado; }
            set
            {
                if (estadoEnlazado != value)
                {
                    estadoEnlazado = value;
                    PropertyChanged?.Invoke(this, new(nameof(EstadoEnlazado)));
                }
            }
        }

        public bool EstadoInternet
        {
            get { return estadoInternet; }
            set
            {
                if (estadoInternet != value)
                {
                    estadoInternet = value;
                    PropertyChanged?.Invoke(this, new(nameof(EstadoInternet)));
                }
            }
        }

        public string Laboratorio { set; get; } = null!;
        public string? MAC { set; get; }

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
