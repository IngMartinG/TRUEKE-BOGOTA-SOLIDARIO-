using Microsoft.Extensions.DependencyInjection;
using TruekeBogotaSolidario.Datos.Common;
using TruekeBogotaSolidario.Datos.Entidades;
using TruekeBogotaSolidario.Negocio.Comun;
using TruekeBogotaSolidario.Negocio.Dtos;
using TruekeBogotaSolidario.Negocio.Servicios;
using TruekeBogotaSolidario.Pruebas.Infraestructura;

namespace TruekeBogotaSolidario.Pruebas.Unitarias;

/// <summary>Planes, impulso, facturación, reembolsos, PQR, ingresos, condición del producto y escala nacional.</summary>
public class MonetizacionTests
{
    private static CrearPublicacionRequest Pub(string titulo = "Bicicleta", ModoDto modo = ModoDto.Trueke, CondicionDto? condicion = CondicionDto.Usado,
        string? detalle = null, string? municipio = null, decimal? precio = null)
        => new()
        {
            Titulo = titulo, Descripcion = "Buen estado", CategoriaId = 1, Modo = modo, Condicion = condicion, DetalleCondicion = detalle,
            MunicipioCodigo = municipio, Localidad = "Centro", PrecioReferenciaCop = modo == ModoDto.Compra ? precio ?? 50_000 : null
        };

    private static Task<PublicacionDto> CrearAsync(EntornoNegocio e, Guid actor, CrearPublicacionRequest r)
        => e.EnScopeAsync<IPublicacionService, PublicacionDto>(s => s.CrearAsync(actor, r));

    // ------------------------------------------------------------------ Condición del producto
    [Fact]
    public async Task La_condicion_es_obligatoria_y_reparado_exige_describir_que_se_reparo()
    {
        using var e = new EntornoNegocio();
        var ana = (await e.RegistrarAsync("ana")).Usuario.Id;

        await Assert.ThrowsAsync<ReglaDeNegocioException>(() => CrearAsync(e, ana, Pub(condicion: null)));
        var sinDetalle = await Assert.ThrowsAsync<ReglaDeNegocioException>(() => CrearAsync(e, ana, Pub(condicion: CondicionDto.Reparado)));
        Assert.Contains("Describe el estado", sinDetalle.Message);

        var p = await CrearAsync(e, ana, Pub(condicion: CondicionDto.Reparado, detalle: "Se cambió la pantalla en servicio técnico autorizado"));
        Assert.Equal("Reparado", p.Condicion);
        Assert.Equal("Se cambió la pantalla en servicio técnico autorizado", p.DetalleCondicion);

        var nuevo = await CrearAsync(e, ana, Pub(titulo: "Licuadora sin abrir", condicion: CondicionDto.Nuevo));
        Assert.Equal("Nuevo", nuevo.Condicion);
        Assert.Null(nuevo.DetalleCondicion);

        var filtrado = await e.EnScopeAsync<IPublicacionService, PaginaDto<PublicacionDto>>(s =>
            s.ListarAsync(null, new FiltroPublicacionesRequest { Condicion = CondicionDto.Nuevo }));
        Assert.Single(filtrado.Items);
        Assert.Equal(nuevo.Id, filtrado.Items[0].Id);
    }

    // ------------------------------------------------------------------ Escala nacional
    [Fact]
    public async Task Las_publicaciones_pueden_ser_de_cualquier_municipio_de_Colombia_y_se_filtran_por_departamento()
    {
        using var e = new EntornoNegocio();
        var ana = (await e.RegistrarAsync("ana")).Usuario.Id;

        var bogota = await CrearAsync(e, ana, Pub(titulo: "En Bogotá"));                     // sin municipio: el del perfil (Bogotá)
        var medellin = await CrearAsync(e, ana, Pub(titulo: "En Medellín", municipio: "05001"));
        var envigado = await CrearAsync(e, ana, Pub(titulo: "En Envigado", municipio: "05266"));
        Assert.Equal("11001", bogota.MunicipioCodigo);
        Assert.Equal("Bogotá, D.C.", bogota.Municipio);
        Assert.Equal("Medellín, Antioquia", medellin.Municipio);
        Assert.Equal("05", medellin.DepartamentoCodigo);

        await Assert.ThrowsAsync<ReglaDeNegocioException>(() => CrearAsync(e, ana, Pub(municipio: "99999")));

        var antioquia = await e.EnScopeAsync<IPublicacionService, PaginaDto<PublicacionDto>>(s =>
            s.ListarAsync(null, new FiltroPublicacionesRequest { DepartamentoCodigo = "05" }));
        Assert.Equal(new[] { medellin.Id, envigado.Id }.OrderBy(x => x), antioquia.Items.Select(p => p.Id).OrderBy(x => x));

        var soloMedellin = await e.EnScopeAsync<IPublicacionService, PaginaDto<PublicacionDto>>(s =>
            s.ListarAsync(null, new FiltroPublicacionesRequest { MunicipioCodigo = "05001" }));
        Assert.Equal(medellin.Id, Assert.Single(soloMedellin.Items).Id);
    }

    [Fact]
    public void El_catalogo_DIVIPOLA_tiene_todos_los_municipios_y_busca_sin_tildes()
    {
        Assert.Equal(33, Divipola.Departamentos.Count);
        Assert.Equal(1122, Divipola.Municipios.Count());
        var ubicaciones = new UbicacionService();
        Assert.Equal("San José de Cúcuta", ubicaciones.Buscar("cucuta", 5)[0].Nombre);
        Assert.Equal("11001", ubicaciones.Buscar("bogota", 5)[0].Codigo);
        Assert.All(ubicaciones.MunicipiosDe("76"), m => Assert.StartsWith("76", m.Codigo));
        Assert.Throws<NoEncontradoException>(() => ubicaciones.MunicipiosDe("00"));
    }

    // ------------------------------------------------------------------ Impulsar con Eco-Puntos
    [Fact]
    public async Task Impulsar_cobra_Eco_Puntos_sube_la_publicacion_y_solo_se_puede_cada_24_horas()
    {
        using var e = new EntornoNegocio();
        var ana = (await e.RegistrarAsync("ana")).Usuario.Id;
        var vieja = await CrearAsync(e, ana, Pub(titulo: "Vieja"));
        await CrearAsync(e, ana, Pub(titulo: "Nueva"));

        // con 10 puntos de bienvenida no alcanza (cuesta 20)
        await Assert.ThrowsAsync<ReglaDeNegocioException>(() => e.EnScopeAsync<IPublicacionService, PublicacionDto>(s => s.ImpulsarAsync(ana, vieja.Id)));

        await e.PagarAsync(ana, new IniciarPagoRequest { Concepto = ConceptoPagoDto.Recarga, MontoRecargaCop = 2_000 }); // +20 puntos
        var impulsada = await e.EnScopeAsync<IPublicacionService, PublicacionDto>(s => s.ImpulsarAsync(ana, vieja.Id));
        Assert.NotNull(impulsada.ProximoImpulsoUtc);
        var resumen = await e.EnScopeAsync<IEcoPuntosService, EcoPuntosResumenDto>(s => s.ResumenAsync(ana));
        Assert.Equal(PoliticaEcoPuntos.PuntosBienvenida + 20 - PoliticaEcoPuntos.PuntosImpulsar, resumen.Saldo);

        var primera = await e.EnScopeAsync<IPublicacionService, PaginaDto<PublicacionDto>>(s => s.ListarAsync(null, new FiltroPublicacionesRequest()));
        Assert.Equal("Vieja", primera.Items[0].Titulo);

        await e.PagarAsync(ana, new IniciarPagoRequest { Concepto = ConceptoPagoDto.Recarga, MontoRecargaCop = 5_000 });
        var otraVez = await Assert.ThrowsAsync<ReglaDeNegocioException>(() => e.EnScopeAsync<IPublicacionService, PublicacionDto>(s => s.ImpulsarAsync(ana, vieja.Id)));
        Assert.Contains("24 horas", otraVez.Message);
    }

    // ------------------------------------------------------------------ Plan Empresa
    [Fact]
    public async Task El_plan_Empresa_da_destacados_gratis_descuento_perfil_comercial_y_mas_publicaciones()
    {
        using var e = new EntornoNegocio();
        var tienda = (await e.RegistrarAsync("tienda")).Usuario.Id;
        await e.PagarAsync(tienda, new IniciarPagoRequest { Concepto = ConceptoPagoDto.Empresa });

        var resumen = await e.EnScopeAsync<IEcoPuntosService, EcoPuntosResumenDto>(s => s.ResumenAsync(tienda));
        Assert.Equal("Empresa", resumen.TipoCuenta);
        Assert.Equal(PoliticaEcoPuntos.DestacadosGratisEmpresa, resumen.DestacadosGratisRestantes);
        Assert.Equal(PoliticaEcoPuntos.MaxPublicacionesEmpresa, resumen.MaxPublicacionesActivas);

        var cotizacion = await e.EnScopeAsync<IPagoService, CotizacionDto?>(s => s.CotizarAsync(tienda, ConceptoPagoDto.Destacar));
        Assert.Equal(PoliticaEcoPuntos.DescuentoEmpresaPorcentaje, cotizacion!.DescuentoPorcentaje);
        Assert.Equal(4_800, cotizacion.TotalCop);
        Assert.Equal(766.39m, cotizacion.IvaIncluidoCop); // 4.800 incluye IVA del 19 %

        var pub = await CrearAsync(e, tienda, Pub());
        await e.EnScopeAsync<IPagoService>(s => s.DestacarGratisAsync(tienda, pub.Id));
        var perfil = await e.EnScopeAsync<IAuthService, UsuarioDto>(s => s.ActualizarPerfilEmpresaAsync(tienda,
            new PerfilEmpresaRequest { NombreComercial = "Muebles La Esquina", Nit = "900.123.456" }));
        Assert.Equal("900123456-8", perfil.Nit); // DV calculado
        var publica = await e.EnScopeAsync<IPublicacionService, PublicacionDto>(s => s.ObtenerAsync(null, pub.Id));
        Assert.True(publica.Destacada);
        Assert.Equal("Muebles La Esquina", publica.Propietario.NombreComercial);
    }

    [Fact]
    public async Task Sin_plan_Empresa_no_se_puede_crear_perfil_comercial()
    {
        using var e = new EntornoNegocio();
        var ana = (await e.RegistrarAsync("ana")).Usuario.Id;
        await Assert.ThrowsAsync<ReglaDeNegocioException>(() => e.EnScopeAsync<IAuthService, UsuarioDto>(s =>
            s.ActualizarPerfilEmpresaAsync(ana, new PerfilEmpresaRequest { NombreComercial = "Mi tienda", Nit = "900123456" })));
    }

    // ------------------------------------------------------------------ Estatuto del Consumidor art. 53
    [Fact]
    public async Task Mas_de_5_ventas_activas_exigen_identidad_verificada()
    {
        using var e = new EntornoNegocio();
        var ana = (await e.RegistrarAsync("ana")).Usuario.Id;
        for (var i = 0; i < PoliticaEcoPuntos.MaxVentasActivasSinIdentificar; i++)
            await CrearAsync(e, ana, Pub(titulo: $"Venta {i}", modo: ModoDto.Compra));

        var ex = await Assert.ThrowsAsync<ReglaDeNegocioException>(() => CrearAsync(e, ana, Pub(titulo: "Venta extra", modo: ModoDto.Compra)));
        Assert.Contains("verificar tu identidad", ex.Message);
        await CrearAsync(e, ana, Pub(titulo: "Trueke sí", modo: ModoDto.Trueke)); // trueke y donación no cuentan

        await e.VerificarIdentidadAsync(ana);
        await CrearAsync(e, ana, Pub(titulo: "Venta extra", modo: ModoDto.Compra));
    }

    // ------------------------------------------------------------------ Facturación electrónica
    [Fact]
    public async Task Cada_pago_aprobado_genera_su_factura_con_IVA_discriminado_y_el_equipo_registra_la_emision()
    {
        using var e = new EntornoNegocio();
        var ana = (await e.RegistrarAsync("ana")).Usuario.Id;
        await e.EnScopeAsync<IFacturacionService, DatosFacturacionDto>(s => s.ActualizarDatosAsync(ana, new DatosFacturacionRequest
        {
            TipoDocumento = TipoDocumentoFiscalDto.CC, Documento = "1.020.304.050", Nombre = "Ana Prueba", Correo = "facturas@ana.co", MunicipioCodigo = "05001"
        }));
        await e.PagarAsync(ana, new IniciarPagoRequest { Concepto = ConceptoPagoDto.Premium });
        await e.PagarAsync(ana, new IniciarPagoRequest { Concepto = ConceptoPagoDto.Recarga, MontoRecargaCop = 3_000 }, aprobado: false); // rechazado: sin factura

        var factura = Assert.Single(await e.EnScopeAsync<IFacturacionService, IReadOnlyList<FacturaDto>>(s => s.ListarMiasAsync(ana)));
        Assert.Equal(PoliticaEcoPuntos.PrecioPremiumCop, factura.TotalCop);
        Assert.Equal(12_605.04m, factura.BaseCop);
        Assert.Equal(2_394.96m, factura.IvaCop);
        Assert.Equal("1020304050", factura.CompradorDocumento);
        Assert.Equal("Pendiente", factura.Estado);

        var admin = (await e.RegistrarAsync("admin")).Usuario.Id;
        await e.HacerModeradorAsync(admin, RolUsuarioEnum.Administrador);
        var cola = await e.EnScopeAsync<IAdministracionService, PaginaDto<FacturaAdminDto>>(s => s.ListarFacturasAsync(admin, EstadoFacturaDto.Pendiente, 1, 20));
        Assert.Equal("facturas@ana.co", Assert.Single(cola.Items).CompradorCorreo);
        var csv = await e.EnScopeAsync<IAdministracionService, string>(s => s.ExportarFacturasCsvAsync(admin, EstadoFacturaDto.Pendiente));
        Assert.Contains("Ana Prueba", csv);

        await Assert.ThrowsAsync<ReglaDeNegocioException>(() => e.EnScopeAsync<IAdministracionService, FacturaAdminDto>(s =>
            s.MarcarFacturaEmitidaAsync(admin, factura.Id, "FE-1", "corto")));
        await e.EnScopeAsync<IAdministracionService, FacturaAdminDto>(s => s.MarcarFacturaEmitidaAsync(admin, factura.Id, "FE-1024", new string('a', 96)));
        var emitida = Assert.Single(await e.EnScopeAsync<IFacturacionService, IReadOnlyList<FacturaDto>>(s => s.ListarMiasAsync(ana)));
        Assert.Equal("Emitida", emitida.Estado);
        Assert.Equal("FE-1024", emitida.NumeroDian);
    }

    [Fact]
    public async Task Sin_datos_de_facturacion_se_factura_a_consumidor_final()
    {
        using var e = new EntornoNegocio();
        var ana = (await e.RegistrarAsync("ana")).Usuario.Id;
        await e.PagarAsync(ana, new IniciarPagoRequest { Concepto = ConceptoPagoDto.Recarga, MontoRecargaCop = 2_000 });
        var f = Assert.Single(await e.EnScopeAsync<IFacturacionService, IReadOnlyList<FacturaDto>>(s => s.ListarMiasAsync(ana)));
        Assert.Equal(Factura.DocumentoConsumidorFinal, f.CompradorDocumento);
        Assert.Equal("Consumidor final", f.CompradorNombre);
    }

    [Fact]
    public async Task Reembolsar_un_plan_revierte_el_beneficio_y_anula_la_factura_no_emitida()
    {
        using var e = new EntornoNegocio();
        var ana = (await e.RegistrarAsync("ana")).Usuario.Id;
        var referencia = await e.PagarAsync(ana, new IniciarPagoRequest { Concepto = ConceptoPagoDto.Premium });
        Assert.Equal("Premium", (await e.EnScopeAsync<IEcoPuntosService, EcoPuntosResumenDto>(s => s.ResumenAsync(ana))).TipoCuenta);

        var super = (await e.RegistrarAsync("super")).Usuario.Id;
        await e.HacerModeradorAsync(super);
        await e.EnScopeAsync<IAdministracionService, PagoAdminDto>(s => s.MarcarReembolsadoAsync(super, referencia, "Retracto aceptado, reembolso Wompi #123"));

        var resumen = await e.EnScopeAsync<IEcoPuntosService, EcoPuntosResumenDto>(s => s.ResumenAsync(ana));
        Assert.Equal("Individual", resumen.TipoCuenta);
        Assert.Equal(0, resumen.DestacadosGratisRestantes);
        Assert.Equal("Anulada", Assert.Single(await e.EnScopeAsync<IFacturacionService, IReadOnlyList<FacturaDto>>(s => s.ListarMiasAsync(ana))).Estado);
    }

    [Fact]
    public async Task Reembolsar_una_recarga_retira_los_puntos_acreditados()
    {
        using var e = new EntornoNegocio();
        var ana = (await e.RegistrarAsync("ana")).Usuario.Id;
        var referencia = await e.PagarAsync(ana, new IniciarPagoRequest { Concepto = ConceptoPagoDto.Recarga, MontoRecargaCop = 5_000 }); // +50
        var super = (await e.RegistrarAsync("super")).Usuario.Id;
        await e.HacerModeradorAsync(super);
        await e.EnScopeAsync<IAdministracionService, PagoAdminDto>(s => s.MarcarReembolsadoAsync(super, referencia, "Reversión"));
        Assert.Equal(PoliticaEcoPuntos.PuntosBienvenida, (await e.EnScopeAsync<IEcoPuntosService, EcoPuntosResumenDto>(s => s.ResumenAsync(ana))).Saldo);
    }

    // ------------------------------------------------------------------ PQR
    [Fact]
    public async Task El_retracto_se_radica_sobre_un_pago_propio_aprobado_y_el_equipo_responde()
    {
        using var e = new EntornoNegocio();
        var ana = (await e.RegistrarAsync("ana")).Usuario.Id;
        var beto = (await e.RegistrarAsync("beto")).Usuario.Id;
        var referencia = await e.PagarAsync(ana, new IniciarPagoRequest { Concepto = ConceptoPagoDto.Premium });

        CrearPqrRequest Retracto(string? pago) => new() { Tipo = TipoPqrDto.Retracto, Asunto = "Quiero retractarme", Descripcion = "Compré el plan por error, solicito el retracto.", PagoReferencia = pago };
        await Assert.ThrowsAsync<ReglaDeNegocioException>(() => e.EnScopeAsync<IPqrService, PqrDto>(s => s.CrearAsync(ana, Retracto(null))));
        await Assert.ThrowsAsync<ReglaDeNegocioException>(() => e.EnScopeAsync<IPqrService, PqrDto>(s => s.CrearAsync(beto, Retracto(referencia)))); // pago ajeno

        var pqr = await e.EnScopeAsync<IPqrService, PqrDto>(s => s.CrearAsync(ana, Retracto(referencia)));
        Assert.StartsWith("PQR-", pqr.Radicado);
        Assert.True(pqr.FechaLimiteUtc > pqr.FechaUtc.AddDays(14)); // 15 días hábiles
        await Assert.ThrowsAsync<ReglaDeNegocioException>(() => e.EnScopeAsync<IPqrService, PqrDto>(s => s.CrearAsync(ana, Retracto(referencia)))); // ya hay una abierta

        var admin = (await e.RegistrarAsync("admin")).Usuario.Id;
        await e.HacerModeradorAsync(admin, RolUsuarioEnum.Administrador);
        var abiertas = await e.EnScopeAsync<IAdministracionService, PaginaDto<PqrAdminDto>>(s => s.ListarPqrAsync(admin, EstadoPqrDto.Abierta, 1, 20));
        Assert.Equal(pqr.Id, Assert.Single(abiertas.Items).Id);
        await e.EnScopeAsync<IAdministracionService, PqrAdminDto>(s => s.ResponderPqrAsync(admin, pqr.Id, "Aprobamos tu retracto; el reembolso llegará en 5 días."));
        var mias = await e.EnScopeAsync<IPqrService, IReadOnlyList<PqrDto>>(s => s.ListarMiasAsync(ana));
        Assert.Equal("Respondida", Assert.Single(mias).Estado);
    }

    [Fact]
    public void Los_dias_habiles_no_cuentan_fines_de_semana()
    {
        var viernes = new DateTime(2026, 10, 2, 12, 0, 0, DateTimeKind.Utc);
        Assert.Equal(new DateTime(2026, 10, 5, 12, 0, 0, DateTimeKind.Utc), Pqr.SumarDiasHabiles(viernes, 1)); // lunes
        Assert.Equal(new DateTime(2026, 10, 23, 12, 0, 0, DateTimeKind.Utc), Pqr.SumarDiasHabiles(viernes, 15));
    }

    // ------------------------------------------------------------------ Ingresos
    [Fact]
    public async Task El_tablero_de_ingresos_suma_lo_cobrado_resta_reembolsos_y_solo_lo_ve_el_SuperUsuario()
    {
        using var e = new EntornoNegocio();
        var ana = (await e.RegistrarAsync("ana")).Usuario.Id;
        await e.PagarAsync(ana, new IniciarPagoRequest { Concepto = ConceptoPagoDto.Premium });
        var recarga = await e.PagarAsync(ana, new IniciarPagoRequest { Concepto = ConceptoPagoDto.Recarga, MontoRecargaCop = 10_000 });

        var admin = (await e.RegistrarAsync("admin")).Usuario.Id;
        await e.HacerModeradorAsync(admin, RolUsuarioEnum.Administrador);
        await Assert.ThrowsAsync<AccesoDenegadoException>(() => e.EnScopeAsync<IAdministracionService, IngresosDto>(s => s.ObtenerIngresosAsync(admin, new RangoFechasRequest())));

        var super = (await e.RegistrarAsync("super")).Usuario.Id;
        await e.HacerModeradorAsync(super);
        await e.EnScopeAsync<IAdministracionService, PagoAdminDto>(s => s.MarcarReembolsadoAsync(super, recarga, "Reversión"));
        var ingresos = await e.EnScopeAsync<IAdministracionService, IngresosDto>(s => s.ObtenerIngresosAsync(super, new RangoFechasRequest()));
        Assert.Equal(25_000, ingresos.AprobadoCop);
        Assert.Equal(10_000, ingresos.ReembolsadoCop);
        Assert.Equal(15_000, ingresos.NetoCop);
        Assert.Equal(1, ingresos.PremiumVigentes);
        Assert.Equal(PoliticaEcoPuntos.PrecioPremiumCop, ingresos.IngresoRecurrenteMensualCop);

        var csv = await e.EnScopeAsync<IAdministracionService, string>(s => s.ExportarIngresosCsvAsync(super, new RangoFechasRequest()));
        Assert.Equal(3, csv.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length); // encabezado + 2 pagos
    }

    // ------------------------------------------------------------------ Vistas y estadísticas
    [Fact]
    public async Task Las_vistas_se_cuentan_una_vez_por_visitante_y_la_serie_diaria_es_para_planes_pagos()
    {
        using var e = new EntornoNegocio();
        var ana = (await e.RegistrarAsync("ana")).Usuario.Id;
        var beto = (await e.RegistrarAsync("beto")).Usuario.Id;
        var pub = await CrearAsync(e, ana, Pub());

        foreach (var visitante in new[] { "ip:a", "ip:a", "ip:b", $"u:{beto:N}" })
            await e.EnScopeAsync<IPublicacionService, PublicacionDto>(s => s.ObtenerAsync(null, pub.Id, visitante));
        await e.EnScopeAsync<IPublicacionService, PublicacionDto>(s => s.ObtenerAsync(ana, pub.Id, $"u:{ana:N}")); // el dueño no cuenta
        var registro = e.Proveedor.GetRequiredService<IRegistroVistas>();
        using (var scope = e.Proveedor.CreateScope())
            await scope.ServiceProvider.GetRequiredService<Datos.Repositorios.IEstadisticaRepository>().SumarVistasAsync(registro.Extraer());

        var stats = await e.EnScopeAsync<IPublicacionService, EstadisticasPublicacionDto>(s => s.ObtenerEstadisticasAsync(ana, pub.Id));
        Assert.Equal(3, stats.VistasTotales);
        Assert.False(stats.SerieDisponible);
        Assert.Empty(stats.Serie);
        Assert.Equal(3, (await e.EnScopeAsync<IPublicacionService, PublicacionDto>(s => s.ObtenerAsync(ana, pub.Id))).Vistas);
        Assert.Null((await e.EnScopeAsync<IPublicacionService, PublicacionDto>(s => s.ObtenerAsync(beto, pub.Id))).Vistas); // no se le muestra a terceros

        await Assert.ThrowsAsync<NoEncontradoException>(() => e.EnScopeAsync<IPublicacionService, EstadisticasPublicacionDto>(s => s.ObtenerEstadisticasAsync(beto, pub.Id)));

        await e.PagarAsync(ana, new IniciarPagoRequest { Concepto = ConceptoPagoDto.Premium });
        var conPlan = await e.EnScopeAsync<IPublicacionService, EstadisticasPublicacionDto>(s => s.ObtenerEstadisticasAsync(ana, pub.Id));
        Assert.True(conPlan.SerieDisponible);
        Assert.Equal(30, conPlan.Serie.Count);
        Assert.Equal(3, conPlan.Serie[^1].Vistas);
    }

    [Fact]
    public async Task La_vitrina_muestra_solo_destacadas_vigentes()
    {
        using var e = new EntornoNegocio();
        var ana = (await e.RegistrarAsync("ana")).Usuario.Id;
        var destacada = await CrearAsync(e, ana, Pub(titulo: "Destacada", municipio: "76001"));
        await CrearAsync(e, ana, Pub(titulo: "Normal"));
        await e.PagarAsync(ana, new IniciarPagoRequest { Concepto = ConceptoPagoDto.Destacar, PublicacionId = destacada.Id });

        var vitrina = await e.EnScopeAsync<IPublicacionService, IReadOnlyList<PublicacionDto>>(s => s.ListarDestacadasAsync(null, new DestacadasRequest()));
        Assert.Equal(destacada.Id, Assert.Single(vitrina).Id);
        Assert.Empty(await e.EnScopeAsync<IPublicacionService, IReadOnlyList<PublicacionDto>>(s => s.ListarDestacadasAsync(null, new DestacadasRequest { DepartamentoCodigo = "05" })));
    }

    // ------------------------------------------------------------------ Documentos fiscales
    [Theory]
    [InlineData("900123456", "900123456-8")]
    [InlineData("900.123.456-8", "900123456-8")]
    [InlineData("860002964", "860002964-4")]
    public void El_NIT_se_normaliza_con_su_digito_de_verificacion(string entrada, string esperado)
        => Assert.Equal(esperado, DocumentosFiscales.NormalizarNit(entrada));

    [Fact]
    public void Un_NIT_con_digito_de_verificacion_incorrecto_se_rechaza()
        => Assert.Throws<ReglaDeNegocioException>(() => DocumentosFiscales.NormalizarNit("900123456-1"));
}
