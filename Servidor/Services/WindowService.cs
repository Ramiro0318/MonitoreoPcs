using Servidor.ViewModels;
using Servidor.Views;
using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;

namespace Servidor.Services
{
    public class WindowService : IWindowService
    {
        public void MostrarNotificacionRegistro(object viewModel)
        {
            var ventana = new NotificacionRegistro
            {
                DataContext = viewModel,
                Owner = Application.Current.MainWindow
            };
            if (viewModel is ServerViewModel vm)
            {
                vm.VentanaCerrada = new Action(ventana.Close);
            }
            ventana.ShowDialog();
        }

        public void MostrarVentanaEditar(object viewModel)
        {
            var ventana = new Editar { DataContext = viewModel };
            ventana.ShowDialog();
        }
    }
}
