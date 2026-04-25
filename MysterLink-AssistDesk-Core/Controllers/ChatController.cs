using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using System.Text;

namespace MysterLink_AssistDesk_Core.Controllers
{
    public class ChatController : Controller
    {
        private readonly HttpClient _httpClient;
        private readonly string _historyDirectory;

        public ChatController()
        {
            _httpClient = new HttpClient();
            // Carpeta en la raíz del proyecto para guardar los JSON de contexto
            _historyDirectory = Path.Combine(Directory.GetCurrentDirectory(), "ChatHistory");
            if (!Directory.Exists(_historyDirectory))
            {
                Directory.CreateDirectory(_historyDirectory);
            }
        }

        public IActionResult Index(string user)
        {
            if (string.IsNullOrEmpty(user))
                return RedirectToAction("Index", "Home");

            ViewBag.User = user;
            return View();
        }

        // Endpoint para cargar el historial cuando se abre el chat del bot
        [HttpGet]
        public IActionResult GetBotHistory(string user)
        {
            var history = LoadBotHistory(user);
            return Json(history);
        }

        // Endpoint que recibe el mensaje, actualiza contexto, llama a Ollama y responde
        [HttpPost]
        public async Task<IActionResult> AskBot([FromBody] BotRequest request)
        {
            if (string.IsNullOrEmpty(request.User) || string.IsNullOrEmpty(request.Message))
                return BadRequest("Datos inválidos");

            var history = LoadBotHistory(request.User);

            // Agregamos el mensaje actual al contexto
            history.Add(new ChatMessage { role = "user", content = request.Message });

            // Payload para la API de Ollama
            var ollamaReq = new
            {
                model = "llama3.2:1b", // Cambia a llama3.1 8b si lo prefieres después
                messages = history,
                stream = false
            };

            try
            {
                var content = new StringContent(JsonSerializer.Serialize(ollamaReq), Encoding.UTF8, "application/json");
                // Llamada a tu modelo local
                var response = await _httpClient.PostAsync("http://localhost:11434/api/chat", content);

                if (response.IsSuccessStatusCode)
                {
                    var jsonResponse = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(jsonResponse);
                    var botMessage = doc.RootElement.GetProperty("message").GetProperty("content").GetString();

                    // Agregamos la respuesta al historial y guardamos para la próxima vez
                    history.Add(new ChatMessage { role = "assistant", content = botMessage });
                    SaveBotHistory(request.User, history);

                    return Json(new { reply = botMessage });
                }
                else
                {
                    // Si falla, removemos el mensaje del usuario para no ensuciar el contexto futuro
                    history.RemoveAt(history.Count - 1);
                    return StatusCode(500, "Error comunicándose con el motor local de Ollama.");
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, "Excepción interna: " + ex.Message);
            }
        }

        // -- Métodos de Persistencia JSON --
        private List<ChatMessage> LoadBotHistory(string user)
        {
            var filePath = Path.Combine(_historyDirectory, $"bot_context_{user}.json");
            if (System.IO.File.Exists(filePath))
            {
                var json = System.IO.File.ReadAllText(filePath);
                return JsonSerializer.Deserialize<List<ChatMessage>>(json) ?? new List<ChatMessage>();
            }
            return new List<ChatMessage>();
        }

        private void SaveBotHistory(string user, List<ChatMessage> history)
        {
            var filePath = Path.Combine(_historyDirectory, $"bot_context_{user}.json");
            var json = JsonSerializer.Serialize(history, new JsonSerializerOptions { WriteIndented = true });
            System.IO.File.WriteAllText(filePath, json);
        }
    }

    // Modelos de datos para el API
    public class BotRequest
    {
        public string User { get; set; }
        public string Message { get; set; }
    }

    public class ChatMessage
    {
        public string role { get; set; }
        public string content { get; set; }
    }
}