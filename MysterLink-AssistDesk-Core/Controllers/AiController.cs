using Microsoft.AspNetCore.Mvc;
using MysterLink_AssistDesk_Core.Models;

namespace MysterLink_AssistDesk_Core.Controllers
{
    public class AiController : Controller
    {
        private readonly HttpClient _http;

        public AiController(HttpClient http)
        {
            _http = http;
        }

        [HttpPost]
        public async Task<IActionResult> Chat(string message)
        {
            var payload = new
            {
                model = "llama3.1:1b",
                stream = false,
                messages = new[]
                {
                new { role = "system", content = "Eres un asistente tipo WhatsApp, directo y corto." },
                new { role = "user", content = message }
            }
            };

            var res = await _http.PostAsJsonAsync("http://localhost:11434/api/chat", payload);

            if (!res.IsSuccessStatusCode)
            {
                return Json(new { response = "Error conectando con el modelo." });
            }

            var json = await res.Content.ReadFromJsonAsync<OllamaResponse>();

            return Json(new
            {
                response = json?.message?.content
            });
        }
    }
}
