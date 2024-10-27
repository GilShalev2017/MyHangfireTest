using System.Collections.Concurrent;

namespace HangfireTest.Utility
{
    public interface IJobQueue
    {
        void Enqueue(string filePath);
        Task ProcessJobsAsync();
    }

    public class JobQueue : IJobQueue
    {
        private readonly ConcurrentQueue<string> queue = new ConcurrentQueue<string>();

        public void Enqueue(string filePath)
        {
            queue.Enqueue(filePath);
        }

        public async Task ProcessJobsAsync()
        {
            while (queue.TryDequeue(out var filePath))
            {
                await Task.Run(() => ProcessFile(filePath)); // Process each file asynchronously
            }
        }

        private void ProcessFile(string filePath)
        {
            // Perform face/logo detection, STT, keyword detection, etc.
            //var faceJob = new FaceDetectionJob(filePath);
            //var sttJob = new SpeechToTextJob(filePath);
            //var keywordJob = new KeywordDetectionJob(sttJob.Result);
            //var translationJob = new TranslationJob(sttJob.Result);

            //faceJob.Execute();
            //sttJob.Execute();
            //keywordJob.Execute();
            //translationJob.Execute();

            //SaveJobStatus(filePath, "Completed"); // Save job status to MongoDB
        }

        private void SaveJobStatus(string filePath, string status)
        {
            // Store job status in MongoDB
        }
    }

}
