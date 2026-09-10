using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SistemaChotaExpress.Data;
using SistemaChotaExpress.Models;

namespace SistemaChotaExpress.Services
{
    public interface IDocumentLookupService
    {
        Task<LookupResult> LookupDocumentAsync(string tipoDocumento, string numeroDocumento);
    }

    public class LookupResult
    {
        public bool Success { get; set; }
        public string NombreCompleto { get; set; } = string.Empty;
        public string Documento { get; set; } = string.Empty;
        public string TipoDocumento { get; set; } = string.Empty;
        public string Direccion { get; set; } = string.Empty;
        public string Mensaje { get; set; } = string.Empty;
    }

    public class DocumentLookupService : IDocumentLookupService
    {
        private readonly HttpClient _httpClient;
        private readonly AppDbContext _context;
        private readonly IConfiguration _configuration;

        public DocumentLookupService(AppDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
            _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(6) };
        }

        public async Task<LookupResult> LookupDocumentAsync(string tipoDocumento, string numeroDocumento)
        {
            if (string.IsNullOrWhiteSpace(numeroDocumento))
            {
                return new LookupResult { Success = false, Mensaje = "El número de documento está vacío." };
            }

            // Normalización
            tipoDocumento = tipoDocumento.ToUpper().Trim();
            numeroDocumento = numeroDocumento.Trim();

            // Validación de formatos peruanos
            if (tipoDocumento == "DNI" && numeroDocumento.Length != 8)
            {
                return new LookupResult { Success = false, Mensaje = "El DNI debe contener exactamente 8 dígitos numéricos." };
            }
            if (tipoDocumento == "RUC" && numeroDocumento.Length != 11)
            {
                return new LookupResult { Success = false, Mensaje = "El RUC debe contener exactamente 11 dígitos numéricos." };
            }
            if (tipoDocumento == "CE" && (numeroDocumento.Length < 8 || numeroDocumento.Length > 12))
            {
                return new LookupResult { Success = false, Mensaje = "El Carnet de Extranjería debe tener entre 8 y 12 caracteres." };
            }

            // 1. CONSULTAR EN TIEMPO REAL A API OFICIAL (apiperu.dev)
            string? tokenApiPeru = _configuration["DocumentLookup:ApiPeruToken"] 
                ?? "85f31ca800c9cc8b6c485aff467578a53fc5f9e29bfe6576c268558703407e60";

            if (!string.IsNullOrWhiteSpace(tokenApiPeru))
            {
                if (tipoDocumento == "DNI")
                {
                    try
                    {
                        var request = new HttpRequestMessage(HttpMethod.Get, $"https://apiperu.dev/api/dni/{numeroDocumento}");
                        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tokenApiPeru);

                        var response = await _httpClient.SendAsync(request);
                        if (response.IsSuccessStatusCode)
                        {
                            var content = await response.Content.ReadAsStringAsync();
                            using var doc = JsonDocument.Parse(content);
                            var root = doc.RootElement;

                            if (root.TryGetProperty("success", out var succ) && succ.GetBoolean() && root.TryGetProperty("data", out var dataEl))
                            {
                                string nombreCompleto = "";
                                if (dataEl.TryGetProperty("nombre_completo", out var ncProp) && !string.IsNullOrWhiteSpace(ncProp.GetString()))
                                {
                                    nombreCompleto = ncProp.GetString()!.Trim().ToUpper();
                                }
                                else
                                {
                                    string nombres = dataEl.TryGetProperty("nombres", out var nProp) ? (nProp.GetString() ?? "") : "";
                                    string apPaterno = dataEl.TryGetProperty("apellido_paterno", out var apProp) ? (apProp.GetString() ?? "") : "";
                                    string apMaterno = dataEl.TryGetProperty("apellido_materno", out var amProp) ? (amProp.GetString() ?? "") : "";
                                    nombreCompleto = $"{nombres} {apPaterno} {apMaterno}".Trim().ToUpper();
                                }

                                if (!string.IsNullOrWhiteSpace(nombreCompleto))
                                {
                                    // Actualizar en base de datos si ya existía con nombre incorrecto previo
                                    var existingPassenger = await _context.Pasajeros
                                        .FirstOrDefaultAsync(p => p.TipoDocumento == "DNI" && p.NumeroDocumento == numeroDocumento);

                                    if (existingPassenger != null)
                                    {
                                        if (existingPassenger.NombreCompleto != nombreCompleto)
                                        {
                                            existingPassenger.NombreCompleto = nombreCompleto;
                                            await _context.SaveChangesAsync();
                                        }
                                    }

                                    return new LookupResult
                                    {
                                        Success = true,
                                        TipoDocumento = "DNI",
                                        Documento = numeroDocumento,
                                        NombreCompleto = nombreCompleto,
                                        Mensaje = "Datos verificados en RENIEC en tiempo real."
                                    };
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[DocumentLookupService] Error consultando apiperu.dev DNI: {ex.Message}");
                    }
                }
                else if (tipoDocumento == "RUC")
                {
                    try
                    {
                        var request = new HttpRequestMessage(HttpMethod.Get, $"https://apiperu.dev/api/ruc/{numeroDocumento}");
                        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tokenApiPeru);

                        var response = await _httpClient.SendAsync(request);
                        if (response.IsSuccessStatusCode)
                        {
                            var content = await response.Content.ReadAsStringAsync();
                            using var doc = JsonDocument.Parse(content);
                            var root = doc.RootElement;

                            if (root.TryGetProperty("success", out var succ) && succ.GetBoolean() && root.TryGetProperty("data", out var dataEl))
                            {
                                string razonSocial = dataEl.TryGetProperty("nombre_o_razon_social", out var rzProp) ? (rzProp.GetString() ?? "") : "";
                                string direccion = dataEl.TryGetProperty("direccion_completa", out var dcProp) 
                                    ? (dcProp.GetString() ?? "") 
                                    : (dataEl.TryGetProperty("direccion", out var dProp) ? (dProp.GetString() ?? "") : "");

                                if (!string.IsNullOrWhiteSpace(razonSocial))
                                {
                                    return new LookupResult
                                    {
                                        Success = true,
                                        TipoDocumento = "RUC",
                                        Documento = numeroDocumento,
                                        NombreCompleto = razonSocial.ToUpper(),
                                        Direccion = direccion.ToUpper(),
                                        Mensaje = "Datos verificados en SUNAT en tiempo real."
                                    };
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[DocumentLookupService] Error consultando apiperu.dev RUC: {ex.Message}");
                    }
                }
            }

            // 2. FALLBACK A BASE DE DATOS LOCAL
            var pasajeroDb = await _context.Pasajeros
                .FirstOrDefaultAsync(p => p.TipoDocumento == tipoDocumento && p.NumeroDocumento == numeroDocumento);

            if (pasajeroDb != null && !string.IsNullOrWhiteSpace(pasajeroDb.NombreCompleto))
            {
                return new LookupResult
                {
                    Success = true,
                    TipoDocumento = pasajeroDb.TipoDocumento,
                    Documento = pasajeroDb.NumeroDocumento,
                    NombreCompleto = pasajeroDb.NombreCompleto,
                    Direccion = string.Empty,
                    Mensaje = "Datos obtenidos del historial de pasajeros local."
                };
            }

            // 3. SI NO SE ENCONTRÓ EN RENIEC/SUNAT NI EN DB, INFORMAR CLARAMENTE (SIN GENERAR NOMBRES FALSOS)
            return new LookupResult
            {
                Success = false,
                TipoDocumento = tipoDocumento,
                Documento = numeroDocumento,
                Mensaje = $"No se encontró registro oficial en {(tipoDocumento == "RUC" ? "SUNAT" : "RENIEC")} para el número {numeroDocumento}. Ingrese el nombre manualmente si es necesario."
            };
        }
    }
}
