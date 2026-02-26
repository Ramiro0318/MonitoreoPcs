using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace Servidor.Models.Entities
{
    public class PcInfo
    {
        public string Nombre { get; set; } = null!;
        public IPAddress Ip { get; set; } = null!;
        public int Puerto { get; set; }
        public bool EstadoConectado { get; set; }

        public string Identificador => $"{Nombre}@{Ip}:{Puerto}";
    }
}
