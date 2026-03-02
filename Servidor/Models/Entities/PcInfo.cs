using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Net;
using System.Text;

namespace Servidor.Models.Entities
{
    public class PcInfo: INotifyPropertyChanged
    {
        private bool estadoConectado;
        public string Nombre { get; set; } = null!;
        public string Ip { get; set; } = null!;
        public int Puerto { get; set; }
        public DateTime? HoraConexion { get; set; }
        public DateTime? UltimoLatido { get; set; }
        public string Identificador => $"{Nombre}@{Ip}:{Puerto}";

        public bool EstadoConectado
        {
            get { return estadoConectado; }
            set {
                if(estadoConectado != value)
                {
                    estadoConectado = value;
                    PropertyChanged?.Invoke(this, new(nameof(EstadoConectado)));
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
