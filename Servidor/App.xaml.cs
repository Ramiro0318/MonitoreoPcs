using Servidor.Services;
using Servidor.ViewModels;
using System.Configuration;
using System.Data;
using System.Windows;

namespace Servidor
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private void OnStartup(object sender, StartupEventArgs e)
        {
            // 1. Instanciar el servicio de lógica
            ServerService serverService = new ServerService();

            // 2. Instanciar el servicio de UI (Ventanas)
            IWindowService windowService = new WindowService();

            // 3. Instanciar el ViewModel y pasarle ambos servicios
            var vm = new ServerViewModel(serverService, windowService);

            // 4. Crear la ventana y le asignamos el VM manualmente
            var mainWindow = new MainWindow();
            mainWindow.DataContext = vm;

            mainWindow.Show();
        }
    }

}
