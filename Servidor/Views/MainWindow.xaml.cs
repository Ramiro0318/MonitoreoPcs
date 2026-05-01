using Servidor.ViewModels;
using Servidor.Views;
using System.ComponentModel;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Servidor
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            if (DataContext is ServerViewModel vm)
            {
                vm.PropertyChanged += Vm_PropertyChanged;
            }
        }

        private bool enEdicion = false;
        private void Vm_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            var vm = (ServerViewModel)sender!;
            if (e.PropertyName == nameof(ServerViewModel.Clon))
            {
                if (vm.Clon != null)
                {
                    enEdicion = true;
                    var ventanaEditar = new Servidor.Views.Editar
                    {
                        DataContext = vm, // Compartimos el ViewModel
                        Owner = this,
                        WindowStartupLocation = WindowStartupLocation.CenterOwner
                    };
                    ventanaEditar.ShowDialog();
                    enEdicion = false;
                }
                return;
            }
            if (e.PropertyName == nameof(ServerViewModel.ComputadoraSeleccionada))
            {
                if (vm.ComputadoraSeleccionada != null && !enEdicion)
                {

                    var ventana = new NotificacionRegistro
                    {
                        DataContext = vm,
                        Owner = this,
                        WindowStartupLocation = WindowStartupLocation.CenterOwner
                    };
                    ventana.ShowDialog();
                }
            }

        }
    }
}