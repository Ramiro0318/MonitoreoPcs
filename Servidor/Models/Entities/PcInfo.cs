using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Net;
using System.Text;

namespace Servidor.Models.Entities
{
    public class PcInfo: INotifyPropertyChanged
    {
        private bool estadoEnlazado;
        private bool estadoInternet;
        public string Nombre { get; set; } = null!;
        public string Ip { get; set; } = null!;
        public int Puerto { get; set; }
        public DateTime? HoraConexion { get; set; }
        public DateTime? UltimoLatido { get; set; }
        public DateTime? UltimoPing { get; set; }
        public string Identificador => $"{Nombre}@{Ip}:{Puerto}";

        public bool EstadoEnlazado
        {
            get { return estadoEnlazado; }
            set {
                if(estadoEnlazado != value)
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
                    PropertyChanged?.Invoke(this, new(nameof(estadoInternet)));
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
