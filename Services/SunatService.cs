using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SistemaChotaExpress.Models;

namespace SistemaChotaExpress.Services
{
    public class SunatEnvioResultado
    {
        public bool Exito { get; set; }
        public string Mensaje { get; set; } = string.Empty;
        public string? XmlPath { get; set; }
        public string? PdfUrl { get; set; }
        public string? CdrUrl { get; set; }
        public string? DigestValue { get; set; }
    }

    /// <summary>
    /// Servicio de facturación electrónica SUNAT UBL 2.1 y conector OSE (Nubefact).
    /// Si existe NubefactToken configurado, sube el comprobante directamente a SUNAT.
    /// Siempre genera y respalda el XML oficial en App_Data/Comprobantes/.
    /// </summary>
    public class SunatService
    {
        private readonly IConfiguration _config;
        private readonly ILogger<SunatService> _logger;
        private readonly IWebHostEnvironment _env;
        private readonly IHttpClientFactory _httpClientFactory;

        public SunatService(
            IConfiguration config,
            ILogger<SunatService> logger,
            IWebHostEnvironment env,
            IHttpClientFactory httpClientFactory)
        {
            _config = config;
            _logger = logger;
            _env = env;
            _httpClientFactory = httpClientFactory;
        }

        /// <summary>
        /// Procesa la venta: guarda el XML oficial y si hay token de Nubefact lo envía a SUNAT.
        /// </summary>
        public async Task<SunatEnvioResultado> ProcesarComprobanteAsync(Venta venta)
        {
            var resultado = new SunatEnvioResultado();

            // 1. Siempre generar y guardar el XML local oficial
            resultado.XmlPath = await GenerarYGuardarXmlAsync(venta);

            // 2. Verificar si hay credenciales de OSE (Nubefact) para envío automático
            string? nubefactUrl = _config["Sunat:NubefactUrl"];
            string? nubefactToken = _config["Sunat:NubefactToken"];

            if (!string.IsNullOrWhiteSpace(nubefactUrl) && !string.IsNullOrWhiteSpace(nubefactToken))
            {
                _logger.LogInformation("Enviando comprobante #{VentaId} a SUNAT vía Nubefact...", venta.Id_Venta);
                var envio = await EnviarANubefactAsync(venta, nubefactUrl, nubefactToken);
                resultado.Exito = envio.Exito;
                resultado.Mensaje = envio.Mensaje;
                resultado.PdfUrl = envio.PdfUrl;
                resultado.CdrUrl = envio.CdrUrl;
                resultado.DigestValue = envio.DigestValue;
            }
            else
            {
                resultado.Exito = true;
                resultado.Mensaje = "XML UBL 2.1 generado y guardado localmente. Configure 'NubefactToken' en appsettings.json para transmisión automática en vivo a SUNAT.";
                _logger.LogInformation("XML para venta {VentaId} guardado localmente. Pendiente token SUNAT/Nubefact.", venta.Id_Venta);
            }

            return resultado;
        }

        /// <summary>
        /// Genera el XML UBL 2.1 de la venta y lo guarda en App_Data/Comprobantes/.
        /// </summary>
        public async Task<string?> GenerarYGuardarXmlAsync(Venta venta)
        {
            try
            {
                string xmlContent = GenerarXmlUbl21(venta);

                string outputPath = _config["Sunat:XmlOutputPath"]
                    ?? Path.Combine(_env.ContentRootPath, "App_Data", "Comprobantes");

                if (!Directory.Exists(outputPath))
                    Directory.CreateDirectory(outputPath);

                bool esFactura = venta.TipoComprobante == "Factura";
                string serie = esFactura ? "F001" : "B001";
                string tipoDoc = esFactura ? "01" : "03";
                string correlativo = venta.Id_Venta.ToString("D8");
                string ruc = "20600308883";

                string fileName = $"{ruc}-{tipoDoc}-{serie}-{correlativo}.xml";
                string filePath = Path.Combine(outputPath, fileName);

                await File.WriteAllTextAsync(filePath, xmlContent, Encoding.UTF8);
                _logger.LogInformation("XML SUNAT guardado: {FilePath}", filePath);

                return filePath;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al guardar XML SUNAT para venta {VentaId}", venta.Id_Venta);
                return null;
            }
        }

        /// <summary>
        /// Envía el comprobante a la API de Nubefact para que firme y envíe a SUNAT.
        /// </summary>
        private async Task<SunatEnvioResultado> EnviarANubefactAsync(Venta venta, string url, string token)
        {
            var resultado = new SunatEnvioResultado();
            try
            {
                bool esFactura = venta.TipoComprobante == "Factura";
                decimal subtotal = Math.Round(venta.PrecioPagado / 1.18m, 2);
                decimal igv = venta.PrecioPagado - subtotal;

                string clienteTipoDoc = esFactura ? "6" : (venta.ObjetoPasajero?.TipoDocumento == "DNI" ? "1" : "4");
                string clienteDoc = esFactura
                    ? (venta.RucEmpresa ?? "20000000001")
                    : (venta.ObjetoPasajero?.NumeroDocumento ?? "00000000");
                string clienteNombre = esFactura
                    ? (venta.RazonSocialEmpresa ?? "EMPRESA CLIENTE")
                    : (venta.ObjetoPasajero?.NombreCompleto ?? "CLIENTE GENERAL");
                string clienteDireccion = esFactura
                    ? (venta.DireccionEmpresa ?? "CHOTA - CAJAMARCA")
                    : "-";

                string descripcion = $"SERVICIO DE TRANSPORTE TERRESTRE DE PASAJEROS: " +
                    $"{(venta.ObjetoViaje?.ObjetoRuta?.Origen ?? "CHOTA").ToUpper()} A " +
                    $"{(venta.ObjetoViaje?.ObjetoRuta?.Destino ?? "CHICLAYO").ToUpper()} | " +
                    $"ASIENTO #{venta.NumeroAsiento} | " +
                    $"PASAJERO: {(venta.ObjetoPasajero?.NombreCompleto ?? "PASAJERO").ToUpper()}";

                var payload = new Dictionary<string, object>
                {
                    ["operacion"] = "generar_comprobante",
                    ["tipo_de_comprobante"] = esFactura ? 1 : 2, // 1 = Factura, 2 = Boleta
                    ["serie"] = esFactura ? "F001" : "B001",
                    ["numero"] = venta.Id_Venta,
                    ["sunat_transaction"] = 1,
                    ["cliente_tipo_de_documento"] = clienteTipoDoc,
                    ["cliente_numero_de_documento"] = clienteDoc,
                    ["cliente_denominacion"] = clienteNombre,
                    ["cliente_direccion"] = clienteDireccion,
                    ["cliente_email"] = venta.ObjetoPasajero?.Correo ?? "",
                    ["fecha_de_emision"] = venta.FechaVenta.ToString("dd-MM-yyyy"),
                    ["moneda"] = 1, // 1 = Soles
                    ["tipo_de_cambio"] = "",
                    ["porcentaje_de_igv"] = 18.0,
                    ["total_gravada"] = subtotal,
                    ["total_inafecta"] = 0.0,
                    ["total_exonerada"] = 0.0,
                    ["total_igv"] = igv,
                    ["total"] = venta.PrecioPagado,
                    ["detraccion"] = false,
                    ["enviar_automaticamente_a_la_sunat"] = true,
                    ["enviar_automaticamente_al_cliente"] = false,
                    ["formato_de_pdf"] = "a4",
                    ["items"] = new List<object>
                    {
                        new Dictionary<string, object>
                        {
                            ["unidad_de_medida"] = "ZZ",
                            ["codigo"] = "78111802",
                            ["descripcion"] = descripcion,
                            ["cantidad"] = 1,
                            ["valor_unitario"] = subtotal,
                            ["precio_unitario"] = venta.PrecioPagado,
                            ["descuento"] = "",
                            ["subtotal"] = subtotal,
                            ["tipo_de_igv"] = 1, // Gravado - Operación Onerosa
                            ["igv"] = igv,
                            ["total"] = venta.PrecioPagado,
                            ["anticipo_regularizacion"] = false
                        }
                    }
                };

                var client = _httpClientFactory.CreateClient();
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                client.Timeout = TimeSpan.FromSeconds(15);

                string jsonString = JsonSerializer.Serialize(payload);
                var content = new StringContent(jsonString, Encoding.UTF8, "application/json");

                var response = await client.PostAsync(url, content);
                string responseBody = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    using var doc = JsonDocument.Parse(responseBody);
                    var root = doc.RootElement;

                    resultado.Exito = true;
                    resultado.Mensaje = root.TryGetProperty("sunat_description", out var desc)
                        ? desc.GetString() ?? "Comprobante aceptado por SUNAT"
                        : "Comprobante emitido con éxito";
                    resultado.PdfUrl = root.TryGetProperty("enlace_del_pdf", out var pdf) ? pdf.GetString() : null;
                    resultado.CdrUrl = root.TryGetProperty("enlace_del_cdr", out var cdr) ? cdr.GetString() : null;
                    resultado.DigestValue = root.TryGetProperty("codigo_hash", out var h) ? h.GetString() : null;

                    _logger.LogInformation("Nubefact SUNAT éxito venta {VentaId}: {Mensaje}", venta.Id_Venta, resultado.Mensaje);
                }
                else
                {
                    resultado.Exito = false;
                    resultado.Mensaje = $"Error de respuesta Nubefact ({response.StatusCode}): {responseBody}";
                    _logger.LogWarning("Nubefact error venta {VentaId}: {Body}", venta.Id_Venta, responseBody);
                }
            }
            catch (Exception ex)
            {
                resultado.Exito = false;
                resultado.Mensaje = $"Excepción al contactar Nubefact: {ex.Message}";
                _logger.LogError(ex, "Error al enviar venta {VentaId} a Nubefact", venta.Id_Venta);
            }

            return resultado;
        }

        /// <summary>
        /// Genera el contenido XML UBL 2.1 SUNAT para una venta.
        /// </summary>
        public string GenerarXmlUbl21(Venta venta)
        {
            bool esFactura = venta.TipoComprobante == "Factura";
            string serie = esFactura ? "F001" : "B001";
            string tipoDocSunat = esFactura ? "01" : "03";
            string numeroCorrelativo = venta.Id_Venta.ToString("D8");
            string idComprobante = $"{serie}-{numeroCorrelativo}";
            string rucEmisor = "20600308883";
            string razonSocialEmisor = "TOURS CHOTA EXPRESS S.A.C.";

            decimal subtotal = Math.Round(venta.PrecioPagado / 1.18m, 2);
            decimal igv = venta.PrecioPagado - subtotal;

            string docReceptor = esFactura
                ? (venta.RucEmpresa ?? "20000000001")
                : (venta.ObjetoPasajero?.NumeroDocumento ?? "00000000");
            string tipoDocReceptor = esFactura ? "6" : (venta.ObjetoPasajero?.TipoDocumento == "DNI" ? "1" : "4");
            string razonReceptor = esFactura
                ? (venta.RazonSocialEmpresa ?? "EMPRESA CLIENTE")
                : (venta.ObjetoPasajero?.NombreCompleto ?? "CLIENTE GENERAL");

            string descripcionServicio = $"SERVICIO DE TRANSPORTE DE PASAJEROS: " +
                $"{(venta.ObjetoViaje?.ObjetoRuta?.Origen ?? "ORIGEN")} A " +
                $"{(venta.ObjetoViaje?.ObjetoRuta?.Destino ?? "DESTINO")} - " +
                $"ASIENTO {venta.NumeroAsiento} - " +
                $"BUS {(venta.ObjetoViaje?.ObjetoBus?.Placa ?? venta.ObjetoViaje?.PlacaVehiculo ?? "S/P")}";

            XNamespace cac = "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2";
            XNamespace cbc = "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2";
            XNamespace ext = "urn:oasis:names:specification:ubl:schema:xsd:CommonExtensionComponents-2";

            var xmlDoc = new XDocument(
                new XDeclaration("1.0", "UTF-8", "yes"),
                new XElement("Invoice",
                    new XAttribute(XNamespace.Xmlns + "cac", cac.NamespaceName),
                    new XAttribute(XNamespace.Xmlns + "cbc", cbc.NamespaceName),
                    new XAttribute(XNamespace.Xmlns + "ext", ext.NamespaceName),
                    new XElement(ext + "UBLExtensions",
                        new XElement(ext + "UBLExtension",
                            new XElement(ext + "ExtensionContent",
                                new XElement("Signature", "SIGNATURE_HASH_TOURS_CHOTA_EXPRESS")
                            )
                        )
                    ),
                    new XElement(cbc + "UBLVersionID", "2.1"),
                    new XElement(cbc + "CustomizationID", "2.0"),
                    new XElement(cbc + "ID", idComprobante),
                    new XElement(cbc + "IssueDate", venta.FechaVenta.ToString("yyyy-MM-dd")),
                    new XElement(cbc + "IssueTime", venta.FechaVenta.ToString("HH:mm:ss")),
                    new XElement(cbc + "InvoiceTypeCode", new XAttribute("listID", "0101"), tipoDocSunat),
                    new XElement(cbc + "DocumentCurrencyCode", "PEN"),

                    // Emisor
                    new XElement(cac + "AccountingSupplierParty",
                        new XElement(cac + "Party",
                            new XElement(cac + "PartyIdentification",
                                new XElement(cbc + "ID", new XAttribute("schemeID", "6"), rucEmisor)
                            ),
                            new XElement(cac + "PartyLegalEntity",
                                new XElement(cbc + "RegistrationName", razonSocialEmisor)
                            )
                        )
                    ),

                    // Receptor
                    new XElement(cac + "AccountingCustomerParty",
                        new XElement(cac + "Party",
                            new XElement(cac + "PartyIdentification",
                                new XElement(cbc + "ID", new XAttribute("schemeID", tipoDocReceptor), docReceptor)
                            ),
                            new XElement(cac + "PartyLegalEntity",
                                new XElement(cbc + "RegistrationName", razonReceptor)
                            )
                        )
                    ),

                    // Impuestos
                    new XElement(cac + "TaxTotal",
                        new XElement(cbc + "TaxAmount", new XAttribute("currencyID", "PEN"), igv.ToString("0.00")),
                        new XElement(cac + "TaxSubtotal",
                            new XElement(cbc + "TaxableAmount", new XAttribute("currencyID", "PEN"), subtotal.ToString("0.00")),
                            new XElement(cbc + "TaxAmount", new XAttribute("currencyID", "PEN"), igv.ToString("0.00")),
                            new XElement(cac + "TaxCategory",
                                new XElement(cac + "TaxScheme",
                                    new XElement(cbc + "ID", "1000"),
                                    new XElement(cbc + "Name", "IGV"),
                                    new XElement(cbc + "TaxTypeCode", "VAT")
                                )
                            )
                        )
                    ),

                    // Totales monetarios
                    new XElement(cac + "LegalMonetaryTotal",
                        new XElement(cbc + "LineExtensionAmount", new XAttribute("currencyID", "PEN"), subtotal.ToString("0.00")),
                        new XElement(cbc + "TaxInclusiveAmount", new XAttribute("currencyID", "PEN"), venta.PrecioPagado.ToString("0.00")),
                        new XElement(cbc + "PayableAmount", new XAttribute("currencyID", "PEN"), venta.PrecioPagado.ToString("0.00"))
                    ),

                    // Línea de detalle del pasaje
                    new XElement(cac + "InvoiceLine",
                        new XElement(cbc + "ID", "1"),
                        new XElement(cbc + "InvoicedQuantity", new XAttribute("unitCode", "ZZ"), "1"),
                        new XElement(cbc + "LineExtensionAmount", new XAttribute("currencyID", "PEN"), subtotal.ToString("0.00")),
                        new XElement(cac + "Item",
                            new XElement(cbc + "Description", descripcionServicio),
                            new XElement(cac + "SellersItemIdentification",
                                new XElement(cbc + "ID", "78111802")
                            )
                        ),
                        new XElement(cac + "Price",
                            new XElement(cbc + "PriceAmount", new XAttribute("currencyID", "PEN"), subtotal.ToString("0.00"))
                        )
                    )
                )
            );

            using var stream = new MemoryStream();
            xmlDoc.Save(stream);
            stream.Position = 0;
            return Encoding.UTF8.GetString(stream.ToArray());
        }
    }
}
