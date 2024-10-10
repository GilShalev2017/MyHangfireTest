namespace HangfireTest.Models
{
    public class JobMetadata
    {
        public int Id { get; set; }
        public string FilePath { get; set; }
        public string Status { get; set; } /// e.g., "processing", "completed", "failed"
        public string TimeStamp { get; set; }
        public JobMetadata()
        {

        }
    }
    public class JobBody
    {
        public int Id { get; set; }
        public string FilePath { get; set; }
        public string JobType { get; set; } //e.g., "FaceDetection", "STT", "KeywordDetection"
        public string StartTime { get; set; }
        public string EndTime { get; set; }
        public JobBody()
        {

        }

    }
}
