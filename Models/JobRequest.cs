using HangfireTest.Utility;

namespace HangfireTest.Models
{
    //public class JobRequest
    //{
    //    public required string Name { get; set; }
    //    public required bool IsRealTime { get; set; }
    //    public bool IsRecurring { get; set; } = false;
    //    public string? ExecutionTime { get; set; }
    //    public string? CronExpression { get; set; } //Cron time for example: "14 15 * * *" <-when to invoke the processing every day! the format is:"mm hh * * *"
    //    public List<string> Channels { get; set; } = new List<string>();
    //    // BraodcastStartTime && BroadcastEndTime define the time range to process
    //    public required string BroadcastStartTime { get; set; }
    //    public required string BroadcastEndTime { get; set; }
    //    public List<string>? Keywords { get; set; } = new List<string>();
    //    public required List<string> Operations { get; set; } = new List<string>();
    //    public string? ExpectedAudioLanguage { get; set; }
    //    public List<string>? TranslationLanguages { get; set; }
    //    public string? Id { get; set; }
    //    public DateTime? NextScheduledTime { get; set; }
    //}

    public enum InvocationType
    {
        RealTime,//Streaming?
        Batch,
        Both,
    }

}
