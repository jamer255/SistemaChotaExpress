using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SistemaChotaExpress.Services;

namespace SistemaChotaExpress.Controllers
{
    [Authorize]
    public class DocumentoController : Controller
    {
        private readonly IDocumentLookupService _documentLookupService;

        public DocumentoController(IDocumentLookupService documentLookupService)
        {
            _documentLookupService = documentLookupService;
        }

        [HttpGet]
        public async Task<IActionResult> Consultar(string? tipo, string numero)
        {
            if (string.IsNullOrWhiteSpace(numero))
            {
                return Json(new LookupResult { Success = false, Mensaje = "Número de documento requerido." });
            }

            numero = numero.Trim();
            if (string.IsNullOrWhiteSpace(tipo))
            {
                tipo = (numero.Length == 11) ? "RUC" : "DNI";
            }

            var result = await _documentLookupService.LookupDocumentAsync(tipo, numero);
            return Json(result);
        }
    }
}
