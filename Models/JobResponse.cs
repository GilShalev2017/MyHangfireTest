using HangfireTest.Utility;

namespace HangfireTest.Models
{
    public class JobResponse
    {
        public string? JobId { get; set; }
        public required JobRequest JobRequest { get; set; }
        public string? Status { get; set; }
        public List<string>? Errors { get; set; }
    }

}
