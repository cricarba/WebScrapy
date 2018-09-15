using System;
using System.Collections.Generic;
using System.Linq;
using CriCarBa.Contenedor;
using CriCarBa.Dominio;
using CriCarBa.ServiciosAplicacion.Definicion;
using IronWebScraper;

namespace SpiderSoccer
{
    class Program
    {
        static void Main(string[] args)
        {
            var scraper = new FutbolScrapper();
            scraper.RateLimitPerHost = TimeSpan.FromSeconds(3);
            scraper.Start();
        }
    }

    class FutbolScrapper : WebScraper
    {
        private List<Estadio> _Estadios;
        private List<Pais> _Paises;
        private List<Ciudad> _Ciudades;
        private List<Jugador> _jugadores;
        private List<Equipo> _Equipos;
        private List<Torneo> _torneos;
        private List<Partido> _partidos;


        public override void Init()
        {
            this.LoggingLevel = WebScraper.LogLevel.Critical;
            //for (int i = 2006; i < 2018; i++)
            //{
            //    this.Request($"http://www.resultados-futbol.com/apertura_colombia{i}", Parse);
            //}
            for (int i = 2006; i < 2019; i++)
            {
                this.Request($"http://www.resultados-futbol.com/Santa-Fe/{i}", PartidosEquipo);
                Console.WriteLine($"http://www.resultados-futbol.com/Santa-Fe/{i}");
            }

            //this.Request($" http://www.resultados-futbol.com/partido/Santa-Fe/Alianza-Petrolera/2018", DetallePartido);

        }


        public FutbolScrapper()
        {
            FachadaContenedor.RegisterContainer(ContenedorPrueba.ContenedorAplicacion);
            _torneos = FachadaContenedor.Resolver<ITorneoServicioAplicacion>().ObtenerTodos().ToList();
            _Estadios = FachadaContenedor.Resolver<IEstadioServicioAplicacion>().ObtenerTodos().ToList();
            _Paises = FachadaContenedor.Resolver<IPaisServicioAplicacion>().ObtenerTodos().ToList();
            _Ciudades = FachadaContenedor.Resolver<ICiudadServicioAplicacion>().ObtenerTodos().ToList();
            _jugadores = FachadaContenedor.Resolver<IJugadorServicioAplicacion>().ObtenerTodos().ToList();
            _Equipos = FachadaContenedor.Resolver<IEquipoServicioAplicacion>().ObtenerTodos().ToList();
            _partidos = FachadaContenedor.Resolver<IPartidoServicioAplicacion>().ObtenerTodos().ToList();
        }

        public void PartidosEquipo(Response response)
        {

            var torneos = response.Css(".liga");
            foreach (var torneo in torneos)
            {
                var torneoNode = torneo.Css(".title");
                if (torneoNode.Any())
                {
                    var nombreTorneo = torneoNode[0].ChildNodes[2].InnerTextClean;
                    Console.WriteLine(nombreTorneo);
                    var nuevoTorneo = CrearTorneo(nombreTorneo);
                    var partidosTorneo = torneo.Css(".nonplayingnow");
                    var filtro = new ClasesPartido()
                    {
                        Fecha = ".time",
                        EquipoLocal = ".team-home",
                        NodoLocal = 1,
                        EquipoVisitante = ".team-away",
                        NodoVisitante = 0,
                        Resultado = ".score.bold",
                        NodoResultado = 0,
                        EsEquipo = true

                    };
                    CrearPartidos(partidosTorneo, filtro, nuevoTorneo.Id);
                }
            }
        }
        public override void Parse(Response response)
        {
            var torneoNodo = response.Css(".titular-data");
            var nombreTorneo = torneoNodo[0].ChildNodes[5].InnerTextClean;
            var torneo = CrearTorneo(nombreTorneo);
            var partidos = response.Css(".vevent");
            var filtro = new ClasesPartido()
            {
                Fecha = ".fecha",
                EquipoLocal = ".equipo1",
                NodoLocal = 2,
                EquipoVisitante = ".equipo2",
                NodoVisitante = 2,
                Resultado = ".rstd",
                NodoResultado = 9,
                EsEquipo = false
            };
            CrearPartidos(partidos, filtro, torneo.Id);


        }

        private string CrearPartidos(HtmlNode[] partidos, ClasesPartido filtroNodos, Guid idTorneo)
        {
            var fechaPartido = string.Empty;
            foreach (var partido in partidos)
            {
                var equipoLocal = string.Empty;
                var equipoVisitante = string.Empty;
                var resultado = string.Empty;
                var estadio = string.Empty;
                var partidoNuevo = new Partido();
                partidoNuevo.TorneoId = idTorneo;
                HtmlNode[] fechaNodo = partido.Css(filtroNodos.Fecha);
                if (fechaNodo.Any() && fechaNodo[0].ChildNodes.Any())
                {
                    fechaPartido = fechaNodo[0].ChildNodes[0].InnerTextClean;
                    var fechaDividida = fechaPartido.Split(' ');
                    var dia = 0;
                    int.TryParse(fechaDividida[0], out dia);
                    var año = 0;
                    int.TryParse(fechaDividida[2], out año);
                    var mes = fechaDividida[1];
                    var enumMes = Meses.Ene;
                    Enum.TryParse(mes, out enumMes);
                    DateTime fechaPartidoReal = new DateTime((año + 2000), (int)enumMes, dia);
                    partidoNuevo.Fecha = fechaPartidoReal;
                }

                var equipoLocalNodo = partido.Css(filtroNodos.EquipoLocal);
                if (equipoLocalNodo.Any() && equipoLocalNodo[0].ChildNodes.Length >= filtroNodos.NodoLocal)
                {
                    equipoLocal = equipoLocalNodo[0].ChildNodes[filtroNodos.NodoLocal].InnerTextClean;
                    var existeEquipo = _Equipos.FirstOrDefault(x => x.Nombre.ToLower() == equipoLocal.ToLower());
                    if (existeEquipo == null)
                    {
                        existeEquipo = FachadaContenedor.Resolver<IEquipoServicioAplicacion>()
                            .Guardar(new Equipo() { Activo = true, Nombre = equipoLocal, });
                        _Equipos.Add(existeEquipo);
                    }
                    partidoNuevo.EquipoLocalId = existeEquipo.Id;

                }
                var equipoVisitanteNodo = partido.Css(filtroNodos.EquipoVisitante);
                if (equipoVisitanteNodo.Any() && equipoVisitanteNodo[0].ChildNodes.Length >= filtroNodos.NodoVisitante)
                {
                    equipoVisitante = equipoVisitanteNodo[0].ChildNodes[filtroNodos.NodoVisitante].InnerTextClean;

                    var existeEquipo = _Equipos.FirstOrDefault(x => x.Nombre.ToLower() == equipoVisitante.ToLower());
                    if (existeEquipo == null)
                    {
                        existeEquipo = FachadaContenedor.Resolver<IEquipoServicioAplicacion>()
                            .Guardar(new Equipo() { Activo = true, Nombre = equipoVisitante, });
                        _Equipos.Add(existeEquipo);
                    }
                    partidoNuevo.EquipoVisitanteId = existeEquipo.Id;
                }

                var resultadoNodo = partido.Css(filtroNodos.Resultado);
                if (resultadoNodo.Any() && resultadoNodo[0].ChildNodes.Any())
                {
                    resultado = resultadoNodo[0].ChildNodes[filtroNodos.NodoResultado].InnerTextClean;
                    if (!string.IsNullOrEmpty(resultado))
                    {
                        var divisionResultado = resultado.Split('-');
                        if (divisionResultado.Any() && divisionResultado.Length > 1)
                        {
                            var golesLocal = 0;
                            int.TryParse(divisionResultado[0], out golesLocal);
                            partidoNuevo.GolesLocal = golesLocal;
                            var golesVisitante = 0;
                            int.TryParse(divisionResultado[1], out golesVisitante);
                            partidoNuevo.GolesVisitante = golesVisitante;
                        }
                    }

                    if (!filtroNodos.EsEquipo)
                    {
                        estadio = resultadoNodo[0].ChildNodes[5].InnerTextClean;
                        var existeEstadio = _Estadios.FirstOrDefault(x => x.Nombre.ToLower() == estadio.ToLower());
                        if (existeEstadio == null)
                        {
                            var estadioNuevo = FachadaContenedor.Resolver<IEstadioServicioAplicacion>().Guardar(new Estadio() { Nombre = estadio });
                            _Estadios.Add(estadioNuevo);
                        }
                        partidoNuevo.EstadioId = existeEstadio.Id;
                    }
                    var detallePartido = filtroNodos.EsEquipo ? resultadoNodo[0].ChildNodes[filtroNodos.NodoResultado] : resultadoNodo.CSS(".url").First();
                    var linkDetallePartido = detallePartido.Attributes["href"];
                    partidoNuevo.Hora = linkDetallePartido;
                    CrearPartido(partidoNuevo);
                    this.Request(linkDetallePartido, DetallePartido);
                }

            }

            return fechaPartido;
        }

        public void DetallePartido(Response response)
        {
            var nominaPartido = new NominaPartido() { JugadoresLocal = new List<Guid>(), JugadoresVisitante = new List<Guid>(), SuplentesLocal = new List<Guid>(), SuplentesVisitante = new List<Guid>() };

            var equipoLocal = response.Css(".team.team1");
            if (equipoLocal.Any())
            {
                var alineacionLocal = equipoLocal.CSS(".aligns-list");
                var titularesLocal = alineacionLocal[0].ChildNodes;

                foreach (var jugador in titularesLocal)
                {
                    if (jugador.ChildNodes.Length > 4)
                    {
                        var datosJugador = jugador.ChildNodes[5];
                         var linkJugador = datosJugador.ChildNodes[0].Attributes["href"];
                        var jugadorBd = CrearJugador(linkJugador);
                        nominaPartido.JugadoresLocal.Add(jugadorBd.Id);
                        this.Request(linkJugador, DetalleJugador);
                    }
                }

                var suplentesLocal = alineacionLocal[1].ChildNodes;
                foreach (var jugador in suplentesLocal)
                {
                    if (jugador.ChildNodes.Length > 4)
                    {
                        var datosJugador = jugador.ChildNodes[5];
                        var linkJugador = datosJugador.ChildNodes[0].Attributes["href"];
                        var jugadorBd = CrearJugador(linkJugador);
                        nominaPartido.SuplentesLocal.Add(jugadorBd.Id);
                        this.Request(linkJugador, DetalleJugador);

                    }
                }
            }
            var equipoVisitante = response.Css(".team.team2");
            if (equipoVisitante.Any())
            {
                var alineacionVisitante = equipoVisitante.CSS(".aligns-list");
                var titularesVisitante = alineacionVisitante[0].ChildNodes;
                foreach (var jugador in titularesVisitante)
                {
                    if (jugador.ChildNodes.Length > 4)
                    {
                        var datosJugador = jugador.ChildNodes[5];
                        var nombre = datosJugador.TextContentClean;
                        var linkJugador = datosJugador.ChildNodes[0].Attributes["href"];
                        var jugadorBd = CrearJugador(linkJugador);
                        nominaPartido.JugadoresVisitante.Add(jugadorBd.Id);
                        this.Request(linkJugador, DetalleJugador);
                    }
                }

                var suplentesVisitante = alineacionVisitante[1].ChildNodes;
                foreach (var jugador in suplentesVisitante)
                {
                    if (jugador.ChildNodes.Length > 4)
                    {
                        var datosJugador = jugador.ChildNodes[5];
                        var nombre = datosJugador.TextContentClean;
                        var linkJugador = datosJugador.ChildNodes[0].Attributes["href"];
                        var jugadorBd = CrearJugador(linkJugador);
                        nominaPartido.SuplentesVisitante.Add(jugadorBd.Id);
                        this.Request(linkJugador, DetalleJugador);

                    }
                }
            }
            if (equipoLocal.Any() || equipoVisitante.Any())
            {
                var partido = _partidos.Where(x => x.Hora == response.FinalUrl).FirstOrDefault();
                nominaPartido.PartidoId = partido.Id;
                FachadaContenedor.Resolver<INominaPartidoServicioAplicacion>().Guardar(nominaPartido);
            }
        }

        public void DetalleJugador(Response response)
        {
            var ficha = response.Css("#pinfo");
            if (ficha.Any() && ficha[0].ChildNodes.Length >= 3 && ficha[0].ChildNodes[3].ChildNodes.Length >= 1)
            {
                var datosJugador = ficha[0].ChildNodes[3].ChildNodes[1];
                if (datosJugador.ChildNodes.Length >= 47)
                {
                    var fechaNacimiento = datosJugador.ChildNodes[15].InnerTextClean;
                    var paisNacimiento = datosJugador.ChildNodes[23].InnerTextClean;
                    var ciudadNacimiento = datosJugador.ChildNodes[19].InnerTextClean;
                    var nombre = datosJugador.ChildNodes[43].InnerTextClean;
                    var apellidos = datosJugador.ChildNodes[47].InnerTextClean;
                    var pais = CrearPais(paisNacimiento);
                    var ciudad = CrearCiudad(ciudadNacimiento, pais.Id);
                    var existeJugador = _jugadores.FirstOrDefault(x => x.Apodo.ToLower() == response.FinalUrl.ToLower());
                    if (existeJugador == null)
                    {

                        FachadaContenedor.Resolver<IJugadorServicioAplicacion>()
                            .Guardar(new Jugador()
                            {
                                Nombre = nombre,
                                Apellido = apellidos,
                                Nacionalidad = pais.Id,
                                FechaNacimiento = Convert.ToDateTime(fechaNacimiento),
                                Apodo = response.FinalUrl
                            });

                    }
                    else
                    {
                        existeJugador.Nombre = nombre;
                        existeJugador.Apellido = apellidos;
                        existeJugador.Nacionalidad = pais.Id;
                        existeJugador.FechaNacimiento = Convert.ToDateTime(fechaNacimiento);
                        FachadaContenedor.Resolver<IJugadorServicioAplicacion>().Guardar(existeJugador);
                    }

                }
            }
        }

        private void CrearPartido(Partido partido)
        {
            var existePartido = _partidos.Where(x => x.Hora == partido.Hora).FirstOrDefault();
            if (existePartido == null)
                _partidos.Add(FachadaContenedor.Resolver<IPartidoServicioAplicacion>().Guardar(partido));
        }
        private Torneo CrearTorneo(string nombreTorneo)
        {
            var torneoNuevo = _torneos.Where(x => x.Nombre.ToUpper() == nombreTorneo.ToUpper()).FirstOrDefault();
            if (torneoNuevo == null)
            {
                torneoNuevo = FachadaContenedor.Resolver<ITorneoServicioAplicacion>().Guardar(new Torneo() { Nombre = nombreTorneo });
            }
            return torneoNuevo;
        }

        public Pais CrearPais(string nombre)
        {
            var existePais = _Paises.FirstOrDefault(x => x.Nombre.ToLower() == nombre.ToLower());
            if (existePais == null)
            {
                var paisNuevo = FachadaContenedor.Resolver<IPaisServicioAplicacion>().Guardar(new Pais() { Nombre = nombre });
                _Paises.Add(paisNuevo);
                return paisNuevo;
            }
            return existePais;
        }

        public Ciudad CrearCiudad(string nombre, Guid idPais)
        {
            var existeCiudad = _Ciudades.FirstOrDefault(x => x.Nombre.ToLower() == nombre.ToLower());
            if (existeCiudad == null)
            {
                var ciudadNueva = FachadaContenedor.Resolver<ICiudadServicioAplicacion>().Guardar(new Ciudad() { Nombre = nombre, PaisId = idPais });
                _Ciudades.Add(ciudadNueva);
                return ciudadNueva;
            }
            return existeCiudad;
        }

        public Jugador CrearJugador(string url)
        {
            var jugadorNuevo = _jugadores.FirstOrDefault(x => x.Apodo.ToLower() == url.ToLower());
            if (jugadorNuevo == null)
            {
                jugadorNuevo =
                  FachadaContenedor.Resolver<IJugadorServicioAplicacion>()
                      .Guardar(new Jugador()
                      {
                          Apodo = url
                      });
                _jugadores.Add(jugadorNuevo);
            }
            return jugadorNuevo;
        }
    }

    class ClasesPartido
    {
        public string Fecha { get; set; }
        public string EquipoLocal { get; set; }
        public string EquipoVisitante { get; set; }
        public int NodoLocal { get; internal set; }
        public int NodoVisitante { get; internal set; }
        public string Resultado { get; internal set; }
        public int NodoResultado { get; internal set; }
        public bool EsEquipo { get; internal set; }
    }

    enum Meses
    {
        Ene = 1,
        Feb = 2,
        Mar = 3,
        Abr = 4,
        May = 5,
        Jun = 6,
        Jul = 7,
        Ago = 8,
        Sep = 9,
        Oct = 10,
        Nov = 11,
        Dic = 12
    }
}
