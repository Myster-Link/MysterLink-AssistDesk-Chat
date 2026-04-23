namespace MysterLink_AssistDesk_Core.Models
{
    public class OllamaResponse
    {
        public string model { get; set; }
        public string created_at { get; set; }
        public Message message { get; set; }
        public bool done { get; set; }

        public class Message
        {
            public string role { get; set; }
            public string content { get; set; }
        }
    }
}
