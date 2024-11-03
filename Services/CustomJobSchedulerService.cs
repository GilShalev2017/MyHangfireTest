using GraphQL;
using HangfireTest.Models;
using HangfireTest.Repositories;
using MongoDB.Driver;
using NLog;
using System.Collections.Concurrent;
using System.Globalization;

namespace HangfireTest.Services
{
    public interface ICustomJobSchedulerService
    {
        Task ScheduleJobAsync(JobRequestEntity jobRequest);
    }
    public class CustomJobSchedulerService : ICustomJobSchedulerService
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger(); // NLog logger
        private readonly IXXXJobRepository _jobsRepository;
        private readonly IXXXOperationsService _xxxOperationsService;
        private readonly Timer _timer;
        private readonly ConcurrentDictionary<string, string> _scheduledJobs; // Track job status by ID

        public CustomJobSchedulerService(IXXXJobRepository jobsRepository, IXXXOperationsService operationsService)
        {
            _jobsRepository = jobsRepository;
            _xxxOperationsService = operationsService;
            _timer = new Timer(CheckAndRunJobsAsync, null, TimeSpan.Zero, TimeSpan.FromSeconds(30));
            _scheduledJobs = new ConcurrentDictionary<string, string>();
            RecoverInProgressJobs().GetAwaiter().GetResult(); // Recover any interrupted jobs
        }

        private async void CheckAndRunJobsAsync(object? state)
        {
            var jobs = await _jobsRepository.GetJobsByStatusAsync("Pending");

            foreach (var job in jobs)
            {
                if (_scheduledJobs.ContainsKey(job.Id!))
                {
                    // Skip if the job is already scheduled or running
                    continue;
                }

                TimeSpan? delay = job.ScheduledTime - DateTime.Now; // Calculate delay as a TimeSpan

                // Mark as "Pending" and start execution (immediate or delayed)
                _scheduledJobs.TryAdd(job.Id!, "Pending");

                _ = Task.Run(async () =>
                {
                    if (delay > TimeSpan.Zero)
                    {
                        // Wait until the scheduled time if delay is positive
                        await Task.Delay((TimeSpan)delay);
                    }

                    await ExecuteJobAsync(job); // Execute job at precise time

                    _scheduledJobs.TryRemove(job.Id!, out _); // Remove after completion
                });
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
                Logger.Error($"ExecuteJobAsync failed: {ex.Message} stack: {ex.StackTrace}");
                await UpdateJobStatus(job, "Failed");
            }
        }
        private async Task RecoverInProgressJobs()
        {
            var inProgressJobs = await _jobsRepository.GetJobsByStatusAsync("In Progress");
            foreach (var job in inProgressJobs)
            {
                _scheduledJobs.TryAdd(job.Id!, "In Progress");
                await ExecuteJobAsync(job); // Resume or restart the job
                _scheduledJobs.TryRemove(job.Id!, out _);
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
            CheckAndRunJobsAsync(null);
        }

    }
}
