using Microsoft.AspNetCore.SignalR;
using MysterLink_AssistDesk_Core.Models;
using System.Text.Json;

namespace MysterLink_AssistDesk_Core
{
    public class ChatHub : Hub
    {
        // username → connectionId  (un solo tab activo por usuario)
        private static readonly Dictionary<string, string> _connected = new();
        private static readonly object _lock = new();

        // ─── Rutas ────────────────────────────────────────────────────────────
        private static string MessagesDir =>
            Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "data", "messages");

        private static string ConvFile(string a, string b)
        {
            // nombre canónico: siempre alfabético para evitar duplicados
            var pair = new[] { a, b };
            Array.Sort(pair, StringComparer.OrdinalIgnoreCase);
            Directory.CreateDirectory(MessagesDir);
            return Path.Combine(MessagesDir, $"{pair[0]}_{pair[1]}.json");
        }

        // ─── Persistencia ─────────────────────────────────────────────────────
        private static List<ChatMessage> LoadConv(string a, string b)
        {
            var path = ConvFile(a, b);
            if (!File.Exists(path)) return new();
            try
            {
                var json = File.ReadAllText(path);
                return JsonSerializer.Deserialize<List<ChatMessage>>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                    ?? new();
            }
            catch { return new(); }
        }

        private static void SaveMsg(ChatMessage msg)
        {
            lock (_lock)
            {
                var list = LoadConv(msg.FromUser, msg.ToUser);
                list.Add(msg);
                File.WriteAllText(ConvFile(msg.FromUser, msg.ToUser),
                    JsonSerializer.Serialize(list));
            }
        }

        /// Devuelve todos los usuarios con los que `username` tiene historial
        private static List<string> PastContacts(string username)
        {
            if (!Directory.Exists(MessagesDir)) return new();

            var contacts = new List<string>();
            foreach (var file in Directory.GetFiles(MessagesDir, "*.json"))
            {
                var name = Path.GetFileNameWithoutExtension(file);
                var parts = name.Split('_', 2);
                if (parts.Length != 2) continue;

                if (parts[0].Equals(username, StringComparison.OrdinalIgnoreCase))
                    contacts.Add(parts[1]);
                else if (parts[1].Equals(username, StringComparison.OrdinalIgnoreCase))
                    contacts.Add(parts[0]);
            }
            return contacts;
        }

        // ─── Conexión / desconexión ───────────────────────────────────────────
        public override async Task OnConnectedAsync()
        {
            var username = Context.GetHttpContext()!
                                  .Request.Query["user"]
                                  .ToString();

            if (string.IsNullOrWhiteSpace(username))
            {
                Context.Abort();
                return;
            }

            lock (_lock) { _connected[username] = Context.ConnectionId; }

            // Notificar a todos los contactos online que este usuario se conectó
            await BroadcastToAffected(username);

            // Enviar al usuario recién conectado su lista de contactos
            await SendContactList(username);

            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? ex)
        {
            var username = _connected
                .FirstOrDefault(kv => kv.Value == Context.ConnectionId).Key;

            if (username != null)
            {
                lock (_lock) { _connected.Remove(username); }
                await BroadcastToAffected(username);
            }

            await base.OnDisconnectedAsync(ex);
        }

        // ─── Hub methods (llamados desde el cliente) ──────────────────────────

        /// Carga el historial de la conversación con otro usuario
        public async Task GetHistory(string withUser)
        {
            var me = GetMyUsername();
            if (me is null) return;

            var history = LoadConv(me, withUser);
            await Clients.Caller.SendAsync("LoadHistory", history);
        }

        /// Envía mensaje privado por nombre de usuario (no por connectionId)
        public async Task SendPrivateMessage(string toUsername, string message)
        {
            var fromUser = GetMyUsername();
            if (fromUser is null || string.IsNullOrWhiteSpace(message)) return;

            var msg = new ChatMessage
            {
                FromUser = fromUser,
                ToUser = toUsername,
                Message = message.Trim(),
                Timestamp = DateTime.UtcNow
            };

            SaveMsg(msg);

            // Enviar al destinatario si está online
            if (_connected.TryGetValue(toUsername, out var toConnId))
                await Clients.Client(toConnId).SendAsync("ReceiveMessage", msg);

            // Enviar al remitente (confirmación)
            await Clients.Caller.SendAsync("ReceiveMessage", msg);

            // Si el destinatario está online y aún no tiene al remitente en su lista,
            // actualizamos su lista de contactos
            if (_connected.TryGetValue(toUsername, out _))
                await SendContactList(toUsername);
        }

        // ─── Helpers privados ─────────────────────────────────────────────────
        private string? GetMyUsername() =>
            _connected.FirstOrDefault(kv => kv.Value == Context.ConnectionId).Key;

        /// Construye la lista de contactos (online + historial) para un usuario
        private List<UserStatus> BuildContactList(string username)
        {
            var result = new List<UserStatus>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // 1. Usuarios conectados ahora (excepto yo)
            foreach (var u in _connected.Keys)
            {
                if (u.Equals(username, StringComparison.OrdinalIgnoreCase)) continue;
                result.Add(new UserStatus { Username = u, IsOnline = true });
                seen.Add(u);
            }

            // 2. Contactos con historial que no están online
            foreach (var contact in PastContacts(username))
            {
                if (!seen.Contains(contact))
                {
                    result.Add(new UserStatus { Username = contact, IsOnline = false });
                    seen.Add(contact);
                }
            }

            return result;
        }

        /// Envía la lista de contactos actualizada al usuario indicado
        private async Task SendContactList(string username)
        {
            if (!_connected.TryGetValue(username, out var connId)) return;
            var list = BuildContactList(username);
            await Clients.Client(connId).SendAsync("UserList", list);
        }

        /// Actualiza la lista de todos los usuarios que comparten historial con `username`
        private async Task BroadcastToAffected(string username)
        {
            var affected = new HashSet<string>(PastContacts(username),
                                               StringComparer.OrdinalIgnoreCase);

            // También los que están actualmente conectados
            foreach (var u in _connected.Keys)
                affected.Add(u);

            affected.Remove(username); // no a mí mismo

            foreach (var u in affected)
                await SendContactList(u);
        }
    }
}