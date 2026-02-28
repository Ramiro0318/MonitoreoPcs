using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace Cliente.Models.Entities
{
    public class ServerInfo
    {
        public string NombreAsignado { get; set; } = null!;
        public string Ip { get; set; } = null!;
        public int Puerto { get; set; }
    }

}
