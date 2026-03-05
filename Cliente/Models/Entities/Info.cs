using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace Cliente.Models.Entities
{
    public class Info
    {
        public string NombreAsignado { get; set; } = null!;
        public string IpServidor { get; set; } = null!;
        public int PuertoServidor { get; set; }
    }

}
