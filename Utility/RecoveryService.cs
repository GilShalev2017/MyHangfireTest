using Hangfire.Common;
using MongoDB.Driver;

namespace HangfireTest.Utility
{
    public class RecoveryService
    {
        private readonly IMongoCollection<Job> jobCollection;

        public RecoveryService(IMongoCollection<Job> jobCollection)
        {
            this.jobCollection = jobCollection;
        }

        public async Task RecoverUnfinishedJobsAsync()
        {
            //var unfinishedJobs = await jobCollection.Find(j => j.Status == "processing").ToListAsync();
            //foreach (var job in unfinishedJobs)
            //{
            //    // Requeue jobs that were in progress before shutdown
            //    Console.WriteLine($"Requeueing job {job.Id}");
            //    await ProcessJobAsync(job);
            //}
        }

        private async Task ProcessJobAsync(Job job)
        {
            // Logic to reprocess the job
        }
    }

}
