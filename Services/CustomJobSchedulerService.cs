using GraphQL;
using HangfireTest.Models;
using HangfireTest.Repositories;
using MongoDB.Driver;

namespace HangfireTest.Services
{
    public interface ICustomJobSchedulerService
    {
        Task ScheduleJobAsync(JobRequest jobRequest);
    }
    public class CustomJobSchedulerService: ICustomJobSchedulerService
    {
        private readonly IXXXJobRepository _jobsRepository;
        private readonly IXXXOperationsService _xxxOperationsService;
        private readonly Timer _timer;

        public CustomJobSchedulerService(IXXXJobRepository jobsRepository, IXXXOperationsService operationsService)
        {
            _jobsRepository = jobsRepository;
            _xxxOperationsService = operationsService;
          //  _timer = new Timer(CheckAndRunJobs, null, TimeSpan.Zero, TimeSpan.FromSeconds(30)); // adjust interval as needed
        }

        private async void CheckAndRunJobs(object state)
        {
            //var jobs = await _jobsRepository.GetAllPendingJobsAsync();

            //foreach (var job in jobs)
            //{
            //    await ExecuteJob(job);
            //    await UpdateJobStatus(job, "Completed");

            //    // Handle recurring jobs
            //    if (job.IsRecurring)
            //    {
            //        ScheduleNextOccurrence(job);
            //    }
            //}
        }

        private async Task ExecuteJob(JobRequest jobRequest)
        {
            // Call the appropriate operation
            //await _xxxOperationsService.PerformOperationAsync(jobRequest.Operations);
        }

        private async Task UpdateJobStatus(JobRequest job, string status)
        {
            //var filter = Builders<JobRequest>.Filter.Eq(j => j.Id, job.Id);
            //var update = Builders<JobRequest>.Update.Set(j => j.Status, status);
            //await _jobsCollection.UpdateOneAsync(filter, update);
        }

        private void ScheduleNextOccurrence(JobRequest job)
        {
            // Calculate the next scheduled time based on job's recurrence pattern
            //job.ScheduledTime = job.GetNextScheduledTime();
            //_jobsCollection.ReplaceOne(j => j.Id == job.Id, job);
        }

        public Task ScheduleJobAsync(JobRequest jobRequest)
        {
            throw new NotImplementedException();
        }
    }
}
