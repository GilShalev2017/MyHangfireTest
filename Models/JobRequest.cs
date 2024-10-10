using HangfireTest.Utility;

namespace HangfireTest.Models
{
    public class JobRequest
    {
        public required string Name { get; set; }
        public required bool IsRealTime { get; set; }
        public bool IsRecurring { get; set; } = false;
        public string? ExecutionTime { get; set; }
        public string? CronExpression { get; set; } //Cron time for example: "14 15 * * *" <-when to invoke the processing every day! the format is:"mm hh * * *"
        public string? Id { get; set; }
        public List<string> Channels { get; set; } = new List<string>();
        // StartTime && EndTime define the time range to process
        public required string StartTime { get; set; }
        public required string EndTime { get; set; }
        public List<string>? Keywords { get; set; } = new List<string>();
        public required List<string> JobTypes { get; set; } = new List<string>();
        public string? ExpectedAudioLanguage { get; set; }
        public List<string>? TranslationLanguages { get; set; }
    }

    public enum InvocationType
    {
        RT,
        NoneRT,
        Both,
    }

}
