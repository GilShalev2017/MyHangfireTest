namespace HangfireTest.Models
{
    public class NotificationMessage
    {
        public string Keyword { get; set; }
        public int StartInSeconds { get; set; }
        public int EndInSeconds { get; set; }
    }
}
