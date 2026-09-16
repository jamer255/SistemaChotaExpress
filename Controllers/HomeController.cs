using Microsoft.AspNetCore.Mvc;
using SistemaChotaExpress.Data;
using SistemaChotaExpress.Models;
using System.Diagnostics;

namespace SistemaChotaExpress.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                if (User.IsInRole("Gerente"))
                {
                    return RedirectToAction("Dashboard", "Gerente");
                }
                return RedirectToAction("Inicio", "Venta");
            }
            return RedirectToAction("Login", "Login");
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            var exceptionFeature = HttpContext.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerPathFeature>();
            return View(new ErrorViewModel
            {
                RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier,
                ExceptionMessage = exceptionFeature?.Error?.Message,
                ExceptionPath = exceptionFeature?.Path
            });
        }
    }
}
