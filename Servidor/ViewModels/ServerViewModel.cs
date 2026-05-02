using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using Servidor.Models.Entities;
using Servidor.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Printing;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Servidor.ViewModels
{
    public enum Orden { REGISTROAPROBADO, REGISTRO, ENLAZADO, APAGAR, REINICIAR, EDITARINFO, OLVIDAR, HEARTHBEAT, INTERNET }
    public enum Pagina { Computadoras, Laboratorios, Historial, Historico }
    public class ServerViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;


        public Pagina Pagina { get; set; }
        public ICommand RegistrarCommand { set; get; }
        public ICommand RechazarCommand { set; get; }
        public ICommand EnviarComandoCommand { set; get; }
        public ICommand IrEditarCommand { set; get; }
        public ICommand EditarCommand { set; get; }
        public ICommand EliminarCommand { set; get; }
        public ICommand LimpiarCommand { set; get; }
        public ICommand NavegarCommand { set; get; }
        public ICommand FiltrarCommand {  set; get; }

        public string? Info { set; get; }
        public PcInfo? ComputadoraSeleccionada { set; get; }
        public string LaboratorioSeleccionado { set; get; } = null!;

        public PcInfo? Clon { set; get; }

        public ObservableCollection<string> Laboratorios { get; set; } = new ObservableCollection<string> { "Laboratorio 1", "Laboratorio 2", "Laboratorio 3", "Laboratorio 4", "Laboratorio 5" };
        public ObservableCollection<PcInfo> Computadoras { set; get; } = new();
        public ObservableCollection<PcInfo> HistorialConexiones { set; get; } = new();
        public ObservableCollection<ComandoInfo> HistorialComandos { set; get; } = new();


        public ServerService Service { set; get; } = new();



        private string? identificador;

        public ServerViewModel()
        {

            Service.ErrorAlRegistrar += Service_ErrorAlRegistrar;
            Service.RegistroCreado += Service_RegistroCreado;
            Service.RegistroCompletado += Service_RegistroCompletado;
            Service.ComputadoraClonada += Service_ComputadoraClonada;
            Service.ComputadoraEditada += Service_ComputadoraEditada;
            Service.ComputadoraEliminada += Service_ComputadoraEliminada;
            Service.ComputadoraEnlazada += Service_ComputadoraEnlazada;
            Service.ComandoEnviado += Service_ComandoEnviado;
            Service.ListaActualizada += Service_ListaActualizada;
            Service.EstadoPcActualizado += Service_EstadoPcActualizado;



            RegistrarCommand = new RelayCommand<PcInfo>(Registrar);
            RechazarCommand = new RelayCommand(Rechazar);
            IrEditarCommand = new RelayCommand<PcInfo>(IrEditar);
            EditarCommand = new RelayCommand<PcInfo>(Editar);
            EliminarCommand = new RelayCommand(Eliminar);
            LimpiarCommand = new RelayCommand<string>(LimpiarOC);
            NavegarCommand = new RelayCommand<Pagina>(Navegar);
            FiltrarCommand = new RelayCommand(Filtrar);

            Service.Iniciar();
        }

        private void Service_EstadoPcActualizado(PcInfo obj)
        {
            App.Current.Dispatcher.BeginInvoke(() =>
            {
                PropertyChanged?.Invoke(this, new(nameof(Computadoras)));
            });
        }

        private void Service_ComandoEnviado(string mensaje)
        {
            App.Current.Dispatcher.BeginInvoke(() =>
            {
                Info = mensaje;
                PropertyChanged?.Invoke(this, new(nameof(Info)));
            });
        }

        private void Service_RegistroCreado(PcInfo pc)
        {
            App.Current.Dispatcher.BeginInvoke(() =>
            {
                ComputadoraSeleccionada = pc;
                PropertyChanged?.Invoke(this, new(nameof(ComputadoraSeleccionada)));
            });
        }

        private void Service_ErrorAlRegistrar(string error)
        {
            App.Current.Dispatcher.BeginInvoke(() =>
            {
                Info = error;
                PropertyChanged?.Invoke(this, new(nameof(Info)));
                PropertyChanged?.Invoke(this, new(nameof(Computadoras)));
            });
        }

        private void Registrar(PcInfo pc)
        {
            ComputadoraSeleccionada = pc;
            Service.RegistrarComputadora(ComputadoraSeleccionada);
        }

        private void Service_RegistroCompletado(PcInfo pc)
        {
            App.Current.Dispatcher.BeginInvoke(() =>
            {
                ComputadoraSeleccionada = null;
                PropertyChanged?.Invoke(this, new(nameof(ComputadoraSeleccionada)));
            });
        }

        private void Rechazar()
        {
            App.Current.Dispatcher.BeginInvoke(() =>
            {
                ComputadoraSeleccionada = null;
                Clon = null;
                PropertyChanged?.Invoke(this, new(nameof(ComputadoraSeleccionada)));
                PropertyChanged?.Invoke(this, new(nameof(Clon)));   //???
            });
        }

        public void IrEditar(PcInfo pc)
        {
            if (pc != null && pc.EstadoEnlazado)
            {
                ComputadoraSeleccionada = pc;
                identificador = pc.Identificador;
                Info = "";
                PropertyChanged?.Invoke(this, new(nameof(Info)));
                Service.IrEditarComputadora(pc);
            }
        }

        private void Service_ComputadoraClonada(PcInfo clon)
        {
            App.Current.Dispatcher.BeginInvoke(() =>
            {
                Clon = clon;
                PropertyChanged?.Invoke(this, new(nameof(Clon)));
            });
        }

        private void Editar(PcInfo clon)
        {

            if (clon == null || string.IsNullOrWhiteSpace(clon.Nombre))
            {
                Info = "Indique un nombre";
                PropertyChanged?.Invoke(this, new(nameof(Info)));
                return;
            }
            if (!string.IsNullOrEmpty(identificador))
            {
                Service.EditarComputadora(clon);
            }
            Clon = null;
            PropertyChanged?.Invoke(this, new(nameof(Clon)));
        }

        private void Service_ComputadoraEditada(PcInfo clon)
        {
            App.Current.Dispatcher.BeginInvoke(() =>
            {
                //ver si quitar
                ComputadoraSeleccionada = clon;
                PropertyChanged?.Invoke(this, new(nameof(ComputadoraSeleccionada)));
            });
        }


        private void Eliminar()
        {
            if (ComputadoraSeleccionada != null)
            {
                var pcOlviar = ComputadoraSeleccionada;
                Service.EliminarComputadora(pcOlviar);
            }
        }

        private void Service_ComputadoraEliminada(PcInfo obj)
        {
            App.Current.Dispatcher.BeginInvoke(() =>
            {
                PropertyChanged?.Invoke(this, new(nameof(Computadoras)));
            });
        }

        private void LimpiarOC(string oc)
        {
            Service.LimpiarOC(oc);
        }

        private void Service_ComputadoraEnlazada(PcInfo pc)
        {
            App.Current.Dispatcher.Invoke(() =>
            {
                PropertyChanged?.Invoke(this, new(nameof(Computadoras)));
            });
        }

        private void Navegar(Pagina pagina)
        {
            Pagina = pagina;
            if (pagina == Pagina.Computadoras)
            {
                CargarObservableCollections("computadoras");
            }
            else if (pagina == Pagina.Laboratorios)
            {
                CargarObservableCollections("");
            }
            else if (pagina == Pagina.Historial)
            {
                CargarObservableCollections("conexiones");
                CargarObservableCollections("comandos");

            }
            else if (pagina == Pagina.Historico)
            {
                CargarObservableCollections("");
            }
            
            PropertyChanged?.Invoke(this, new(nameof(Pagina)));
        }




        private void Service_ListaActualizada(string oc)
        {
            CargarObservableCollections(oc);
        }

        private void CargarObservableCollections(string oc) 
        {
            App.Current.Dispatcher.Invoke(() =>
            {
                if (oc == "computadoras" && Pagina != Pagina.Historial)
                {
                    Computadoras.Clear();
                    foreach (var pc in Service.Computadoras)
                        Computadoras.Add(pc);
                }
                else if (oc == "conexiones" && Pagina == Pagina.Historial)
                {
                    HistorialConexiones.Clear();
                    foreach (var h in Service.HistorialConexiones)
                        HistorialConexiones.Add(h);
                }
                else if (oc == "comandos" && Pagina == Pagina.Historial)
                {
                    HistorialComandos.Clear();
                    foreach (var c in Service.HistorialComandos)
                        HistorialComandos.Add(c);
                }

            });
        }

        private void Filtrar()
        {
            Computadoras.Clear();
            foreach (var pc in Service.Computadoras.Where(x => x.Laboratorio == LaboratorioSeleccionado))
            {
                Computadoras.Add(pc);
            }
            
        }
    }
}
