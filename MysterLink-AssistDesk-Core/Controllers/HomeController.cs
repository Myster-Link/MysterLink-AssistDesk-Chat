using Microsoft.AspNetCore.Mvc;
using MysterLink_AssistDesk_Core.Models;
using System.Text.Json;

namespace MysterLink_AssistDesk_Core.Controllers
{
    public class HomeController : Controller
    {
        [HttpGet]
        public IActionResult Index() => View();

        [HttpPost]
        public IActionResult Index(string username, string password)
        {
            var path = Path.Combine(
                Directory.GetCurrentDirectory(), "wwwroot", "data", "users.json");

            if (!System.IO.File.Exists(path))
            {
                ViewBag.Error = "Archivo de usuarios no encontrado";
                return View();
            }

            var json = System.IO.File.ReadAllText(path);
            var users = JsonSerializer.Deserialize<List<User>>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? new();

            var valid = users.FirstOrDefault(u =>
                u.Username.Equals(username, StringComparison.OrdinalIgnoreCase) &&
                u.Password == password);

            if (valid is null)
            {
                ViewBag.Error = "Usuario o contraseña incorrectos";
                return View();
            }

            return Redirect($"/Chat/Index?user={valid.Username}");
        }
    }
}