using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using Servidor.Models.Entities;
using Servidor.ViewModels;
using Servidor.Views;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.Tracing;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Timers;
using System.Windows.Input;
using System.Windows.Threading;

namespace Servidor.Services
{
    public class ServerService
    {

        public List<PcInfo> Computadoras { get; set; } = new();
        public List<PcInfo> HistorialConexiones { get; set; } = new();
        public List<ComandoInfo> HistorialComandos { get; set; } = new();


        private PcInfo? Clon { set; get; }
        private PcInfo? ComputadoraResponder { set; get; }
        private System.Timers.Timer TimerEstado;

        private IPAddress ip = IPAddress.Any;
        private int puerto = 60000;
        public int LatidosRecibidos { set; get; }

        UdpClient Server { set; get; }

        private string computadorasFilename = "computadoras.json";
        private string conexionesFilename = "conexiones.json";
        private string comandosFilename = "comandos.json";


        public event Action<string>? ErrorAlRegistrar, ComandoEnviado, ListaActualizada;
        public event Action<PcInfo>? RegistroCreado, RegistroCompletado, ComputadoraClonada, ComputadoraEditada, ComputadoraEliminada, ComputadoraEnlazada, EstadoPcActualizado;
        public void Iniciar()
        {
            AbrirOC(Computadoras, computadorasFilename);
            AbrirOC(HistorialConexiones, conexionesFilename);
            AbrirOC(HistorialComandos, comandosFilename);

            IPEndPoint endpoint = new IPEndPoint(ip, puerto);

            Server = new UdpClient(endpoint);
            Thread hiloEscuchar = new(RecibirMensajes);
            hiloEscuchar.IsBackground = true;
            hiloEscuchar.Start();


            TimerEstado = new System.Timers.Timer(TimeSpan.FromSeconds(1));

            TimerEstado.Elapsed += TimerEstado_Tick;
            TimerEstado.AutoReset = true;
            TimerEstado.Enabled = true;
        }

        private void RecibirSolicitudRegistro(IPEndPoint remoto, string identificador, string laboratorio, string mac)
        {
            if (Computadoras.Any(x => x.Nombre == identificador))
            {
                ErrorAlRegistrar?.Invoke("Una computadora se ha intentado registrar con un nombre ya existente.");
                return;
            }
            if (Computadoras.Any(x => x.MAC == mac))
            {
                ErrorAlRegistrar?.Invoke("Una computadora se ha vuelto a intentar registrar.");
                return;
            }
            PcInfo pc = new PcInfo
            {
                Nombre = identificador,
                Ip = remoto.Address.ToString(),
                Puerto = remoto.Port,
                Laboratorio = laboratorio,
                MAC = mac,
                EstadoEnlazado = false,
            };
            RegistroCreado?.Invoke(pc);//
        }


        public void RegistrarComputadora(PcInfo pc)
        {
            if (pc != null)
            {
                EnviarMensajes(Orden.REGISTROAPROBADO, pc);

                if (!Computadoras.Any(x => x.MAC == pc.MAC))
                {
                    Computadoras.Add(pc);
                    GuardarOC(Computadoras, computadorasFilename);
                }
                RegistroCompletado?.Invoke(pc);
            }

        }


        public void IrEditarComputadora(PcInfo pc)
        {
            Clon = new PcInfo
            {
                Nombre = pc.Nombre,
                Ip = pc.Ip,
                Puerto = pc.Puerto,
                Laboratorio = pc.Laboratorio,
                MAC = pc.MAC,
                HoraConexion = pc.HoraConexion,
                UltimoLatido = pc.UltimoLatido,
                EstadoEnlazado = pc.EstadoEnlazado

            };

            ComputadoraClonada?.Invoke(Clon);

        }


        public void EditarComputadora(PcInfo clon)
        {
            //aplicar la mac
            var pcOriginal = Computadoras.FirstOrDefault(x => x.MAC == clon.MAC);
            if (pcOriginal != null)
            {
                pcOriginal.Nombre = clon.Nombre;
                pcOriginal.Laboratorio = clon.Laboratorio;

                //Cambiarlo por la mac
                var registroHistorial = HistorialConexiones.Where(x => x.MAC == pcOriginal.MAC).ToList();
                registroHistorial.ForEach(x => x.Nombre = clon.Nombre);
                GuardarOC(Computadoras, computadorasFilename);
                GuardarOC(HistorialConexiones, conexionesFilename);

                EnviarMensajes(Orden.EDITARINFO, pcOriginal);
                ComputadoraEditada?.Invoke(pcOriginal);

            }

        }

        public void EliminarComputadora(PcInfo pc)
        {
            EnviarMensajes(Orden.OLVIDAR, pc);
            Computadoras.Remove(pc);
            GuardarOC(Computadoras, computadorasFilename);

            ComputadoraEliminada?.Invoke(pc);
        }

        private void GuardarOC<T>(List<T> oc, string filename)
        {
            string jsonString = JsonSerializer.Serialize(oc);
            File.WriteAllText(filename, jsonString);
                ListaActualizada?.Invoke(filename.Replace(".json", ""));
        }

        private void AbrirOC<T>(List<T> oc, string filename)
        {
            if (File.Exists(filename))
            {
                var jsonString = File.ReadAllText(filename);
                var list = JsonSerializer.Deserialize<List<T>>(jsonString);

                if (list != null)
                {
                    foreach (var o in list)
                    {
                        oc.Add(o);
                    }
                    ListaActualizada?.Invoke(filename.Replace(".json", ""));
                }
            }
        }

        public void LimpiarOC(string oc)
        {
            if (oc == "conexiones")
            {
                HistorialConexiones.Clear();
                GuardarOC(HistorialConexiones, conexionesFilename);
            }
            else if (oc == "comandos")
            {
                HistorialComandos.Clear();
                GuardarOC(HistorialComandos, comandosFilename);
            }
            ListaActualizada?.Invoke(oc);
        }


        public void RecibirMensajes()
        {
            while (true)
            {
                try
                {
                    IPEndPoint remoto = new IPEndPoint(IPAddress.Any, 0);
                    byte[] buffer = Server.Receive(ref remoto);

                    string comando = Encoding.UTF8.GetString(buffer);
                    string[] comandoSeparado = comando.Split('|');

                    if (comandoSeparado[0] == nameof(Orden.REGISTRO) && comandoSeparado.Length == 4)
                    {
                        RecibirSolicitudRegistro(remoto, comandoSeparado[1], comandoSeparado[2], comandoSeparado[3]);
                    }
                    else if (comandoSeparado[0] == nameof(Orden.HEARTHBEAT) && comandoSeparado.Length == 2)
                    {
                        LatidosRecibidos++;

                        var pc = Computadoras.FirstOrDefault(x => x.Nombre == comandoSeparado[1]);
                        if (pc != null)
                        {
                            pc.UltimoLatido = DateTime.Now;
                            if (pc.EstadoEnlazado == false)
                            {
                                pc.HoraConexion = DateTime.Now;
                                pc.EstadoEnlazado = true;
                                LatidosRecibidos = 0;

                                
                                HistorialConexiones.Add(pc);
                                GuardarOC(HistorialConexiones, conexionesFilename);
                                ComputadoraResponder = pc;

                                EstadoPcActualizado?.Invoke(pc);
                                //Invoke(ComputadoraResponder);
                            }
                            EnviarMensajes(Orden.ENLAZADO, pc);
                        }
                    }
                    else if (comandoSeparado[0] == nameof(Orden.INTERNET) && comandoSeparado.Length == 2)
                    {
                        var pc = Computadoras.FirstOrDefault(x => x.Nombre == comandoSeparado[1]);
                        if (pc != null)
                        {
                            pc.UltimoPing = DateTime.Now;
                            pc.EstadoInternet = true;
                        }
                    }
                }
                catch { }
            }
        }



        public void EnviarMensajes(Orden comando, PcInfo computadoraSeleccionada)
        {
            if (comando != Orden.REGISTRO && comando != Orden.HEARTHBEAT && comando != Orden.INTERNET)
            {
                var pc = comando == Orden.ENLAZADO ? ComputadoraResponder : computadoraSeleccionada;
                if (pc != null)
                {

                    if (comando != Orden.ENLAZADO)
                    {
                        HistorialComandos.Add(new ComandoInfo
                        {
                            Destino = pc.Identificador,
                            Comando = comando,
                            Fecha = DateTime.Now,
                            NuevoNombre = comando == Orden.EDITARINFO ? pc.Nombre : "",
                            Laboratorio = comando == Orden.EDITARINFO || comando == Orden.REGISTROAPROBADO ? pc.Laboratorio : "",
                            MAC = pc.MAC
                        });
                        GuardarOC(HistorialComandos, comandosFilename);
                    }

                    string mensaje;
                    if (comando == Orden.EDITARINFO)
                    {
                        mensaje = $"{comando}|{pc.Nombre}|{pc.Laboratorio}";
                    }
                    else if (comando == Orden.REGISTROAPROBADO)
                    {
                        mensaje = $"{comando}|{pc.Nombre}|{pc.Laboratorio}|{pc.MAC}";
                    }
                    else mensaje = comando.ToString();

                    byte[] buffer = Encoding.UTF8.GetBytes(mensaje);
                    IPEndPoint destino = new IPEndPoint(IPAddress.Parse(pc.Ip), pc.Puerto);
                    Server.Send(buffer, buffer.Length, destino);
                    ComandoEnviado?.Invoke($"a {comando.ToString()} {pc.Nombre}");
                }
            }
        }



        private void TimerEstado_Tick(object? sender, EventArgs e)
        {
            foreach (var pc in Computadoras.ToList())   //ElToList ya no es necesario
            {
                if (DateTime.Now - pc.UltimoLatido >= TimeSpan.FromSeconds(30) && pc.EstadoEnlazado)
                {
                    pc.EstadoEnlazado = false;
                    GuardarOC(Computadoras, computadorasFilename);
                    ComputadoraEnlazada?.Invoke(pc);
                }
                if (DateTime.Now - pc.UltimoPing >= TimeSpan.FromSeconds(30) && pc.EstadoInternet)
                {
                    pc.EstadoInternet = false;
                    GuardarOC(Computadoras, computadorasFilename);
                    ComputadoraEnlazada?.Invoke(pc);
                }
                if ((DateTime.Now - pc.UltimoLatido >= TimeSpan.FromHours(1) && DateTime.Now - pc.UltimoPing >= TimeSpan.FromHours(1)) && !pc.EstadoHistorico)
                {
                    pc.EstadoHistorico = true;
                    GuardarOC(Computadoras, computadorasFilename);
                }
            }
        }

    }
}
