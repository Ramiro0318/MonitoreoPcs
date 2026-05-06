using System;
using System.Collections.Generic;
using System.Text;

namespace Servidor.Services
{
    public interface IWindowService
    {
        void MostrarNotificacionRegistro(object viewModel);
        void MostrarVentanaEditar(object viewModel);
    }
}
