using Servidor.ViewModels;
using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace Servidor.Views
{
    /// <summary>
    /// Lógica de interacción para Editar.xaml
    /// </summary>
    public partial class Editar : Window
    {
        public Editar()
        {
            InitializeComponent();
            this.DataContextChanged += Editar_DataContextChanged;
        }

        private void Editar_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (DataContext is ServerViewModel vm)
            {
                vm.PropertyChanged += (s, args) =>
                {
                    if (args.PropertyName == nameof(ServerViewModel.Clon) && vm.Clon == null)
                    {
                        this.Close();
                    }
                };
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button boton)
            {
                var ventana = Window.GetWindow(boton);
                ventana.Close();
            }
        }

    }
}
