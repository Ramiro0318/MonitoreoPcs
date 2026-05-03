using Cliente.Models.Entities;
using Cliente.Services;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Printing;
using System.Text;
using System.Text.Json;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace Cliente.ViewModels
{
    public enum Orden { REGISTROAPROBADO, REGISTRO, ENLAZADO, APAGAR, REINICIAR, EDITARINFO, OLVIDAR, HEARTHBEAT, INTERNET }
    public enum Pagina { Registro, Conectado, Advertencia }
    public class ClienteViewModel : INotifyPropertyChanged
    {
        public ICommand EnviarRegistroCommand { set; get; }
        public ICommand CancelarComandoCommand { set; get; }
        public Pagina Pagina { get; set; }
        public string IpPorValidar { get; set; }
        public string Nombre { set; get; } = null!;
        public string Laboratorio { set; get; } = null!;
        public string Info { set; get; }
        public string? Accion { set; get; }
        public bool Internet { set; get; }
        public Info? Registro { set; get; }
        public sbyte Segundos { set; get; }
        private bool reinicio = false;

        DispatcherTimer UITimer { get; set; }

        public ObservableCollection<string> Laboratorios { get; set; } = new ObservableCollection<string> { "Laboratorio 1", "Laboratorio 2", "Laboratorio 3", "Laboratorio 4", "Laboratorio 5" };
        public ClienteService Service { get; set; } = new();

        public ClienteViewModel()
        {
            Service.RegistroAbierto += Service_RegistroAbierto;
            Service.InformacionActualizada += Service_InformacionActualizada;
            Service.PaginaCambiada += Service_PaginaCambiada;
            Service.RegistroGuardado += Service_RegistroGuardado;
            Service.RegistroActualizado += Service_RegistroActualizado;
            Service.RegistroEliminado += Service_RegistroEliminado;
            Service.EstadoInternetCambiado += Service_EstadoInternetCambiado;
            Service.ShutdownIniciado += Service_ShutdownIniciado;

            EnviarRegistroCommand = new RelayCommand(Enviar);
            CancelarComandoCommand = new RelayCommand(Cancelar);

            UITimer = new();
            UITimer.Tick += UITimer_Tick;
            UITimer.Interval = TimeSpan.FromSeconds(1);

            Service.Iniciar();
        }


        public void Enviar()
        {
            Service.EnviarRegistro(IpPorValidar, Nombre, Laboratorio);
        }

        private void Service_RegistroAbierto(Info registro)
        {
            App.Current.Dispatcher.Invoke(() =>
            {
                Registro = registro;
                PropertyChanged?.Invoke(this, new(nameof(Registro)));
            });
        }

        private void Service_InformacionActualizada(string info)
        {
            App.Current.Dispatcher.Invoke(() =>
            {
                Info = info;
                PropertyChanged?.Invoke(this, new(nameof(Info)));
            });
        }

        private void Service_RegistroGuardado(Info registro)
        {
            App.Current.Dispatcher.Invoke(() =>
            {
                Registro = registro;
                PropertyChanged?.Invoke(this, new(nameof(Registro)));
            });
        }

        private void Service_RegistroActualizado(Info registro, string info)
        {
            App.Current.Dispatcher.Invoke(() =>
            {
                Registro = registro;
                Info = info;
                PropertyChanged?.Invoke(this, new(nameof(Registro)));
                PropertyChanged?.Invoke(this, new(nameof(Info)));
            });
        }

        private void Service_RegistroEliminado(string info)
        {
            App.Current.Dispatcher.Invoke(() =>
            {
                Info = info;
                Registro = null;
                PropertyChanged?.Invoke(this, new(nameof(Info)));
                PropertyChanged?.Invoke(this, new(nameof(Registro)));
            });
        }

        private void Service_PaginaCambiada(Pagina pagina)
        {
            App.Current.Dispatcher.Invoke(() =>
            {
                Pagina = pagina;
                PropertyChanged?.Invoke(this, new(nameof(Pagina)));
            });
        }

        private void Service_EstadoInternetCambiado(bool internet)
        {
            App.Current.Dispatcher.Invoke(() =>
            {
                Internet = internet;
                PropertyChanged?.Invoke(this, new(nameof(Internet)));
            });
        }
        private void Service_ShutdownIniciado(string accion, bool reinicio)
        {
            App.Current.Dispatcher.Invoke(() =>
            {
                Accion = accion;
                this.reinicio = reinicio;
                Segundos = 60;
                PropertyChanged?.Invoke(this, new(nameof(Accion)));

                UITimer.Start();
            });
        }
        private void UITimer_Tick(object? sender, EventArgs e)
        {
            Segundos--;

            PropertyChanged?.Invoke(this, new(nameof(Segundos)));
            if (Segundos <= 0)
            {
                UITimer.Stop();
                Service.Apagar(reinicio);
                Segundos = 60;
            }
        }

        private void Cancelar()
        {
            UITimer.Stop();
            Service.CancelarComando();
        }


        public event PropertyChangedEventHandler? PropertyChanged;

    }
}
