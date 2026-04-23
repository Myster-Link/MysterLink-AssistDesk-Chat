namespace MysterLink_AssistDesk_Core.Models
{
    public class ChatMessage
    {
        public string FromUser { get; set; } = "";
        public string ToUser { get; set; } = "";
        public string Message { get; set; } = "";
        public DateTime Timestamp { get; set; }
    }
}
