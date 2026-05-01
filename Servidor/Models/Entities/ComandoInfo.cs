using Servidor.ViewModels;
using System;
using System.Collections.Generic;
using System.Text;

namespace Servidor.Models.Entities
{
    public class ComandoInfo
    {
        public string Destino { get; set; } = null!;
        public Orden Comando { get; set; }
        public DateTime Fecha { get; set; }
        public string? NuevoNombre { get; set; }
        
    }
}
