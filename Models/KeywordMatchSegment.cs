namespace HangfireTest.Models
{
    public class KeywordMatchSegment
    {
        public required string Timestamp { get; set; }
        public List<KeywordMatch> Results { get; set; } = new List<KeywordMatch>();
    }
}
