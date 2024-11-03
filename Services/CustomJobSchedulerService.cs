using GraphQL;
using HangfireTest.Models;
using HangfireTest.Repositories;
using MongoDB.Driver;

namespace HangfireTest.Services
{
    public interface ICustomJobSchedulerService
    {
        Task ScheduleJobAsync(JobRequestEntity jobRequest);
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
            _timer = new Timer(CheckAndRunJobsAsync, null, TimeSpan.Zero, TimeSpan.FromSeconds(30)); // adjust interval as needed
        }

        private async void CheckAndRunJobsAsync(object? state)
        {
            var jobs = await _jobsRepository.GetAllPendingJobsAsync();

            foreach (var job in jobs)
            {
                await ExecuteJobAsync(job);
            }
        }
        private async Task ExecuteJobAsync(JobRequestEntity job)
        {
            try
            {
                await UpdateJobStatus(job, "In Progress");

                await _xxxOperationsService.ExecuteJobAsync(job);

                await UpdateJobStatus(job, "Completed");

                if (job.IsRecurring)
                {
                    ScheduleNextOccurrence(job);
                }
            }
            catch (Exception ex)
            {
                await UpdateJobStatus(job, "Failed");
            }
        }

        private async Task UpdateJobStatus(JobRequestEntity job, string status)
        {
            await _jobsRepository.UpdateJobStatusAsync(job, status);
        }

        private void ScheduleNextOccurrence(JobRequestEntity job)
        {
            // Calculate the next scheduled time based on job's recurrence pattern
            //job.ScheduledTime = job.GetNextScheduledTime();
            //_jobsCollection.ReplaceOne(j => j.Id == job.Id, job);
        }

        public async Task ScheduleJobAsync(JobRequestEntity jobRequest)
        {
            await _jobsRepository.CreateJobAsync(jobRequest);
        }
        
    }
}
