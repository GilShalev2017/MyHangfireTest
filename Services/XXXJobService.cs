using HangfireTest.Models;
using HangfireTest.Repositories;
using System.Net;

namespace HangfireTest.Services
{
    public interface IXXXJobService
    {
        Task<JobResponse> ScheduleJobAsync(JobRequest jobRequest);
        Task<XXXInsightJobEntity> GetJobStatusAsync(string jobId);
        Task RecoverUnfinishedJobsAsync();  // Recover jobs after system restart
        //Task ProcessExistingFilesAsync(BulkProcessRequest request);
        //Task<string> GetJobReportAsync(string jobId);
    }

    public class XXXJobService : IXXXJobService
    {
        private readonly IXXXJobRepository _mediaInsightJobRepository;
        private readonly IXXXJobScheduler _mediaInsightJobScheduler;
        public XXXJobService(IXXXJobRepository mediaInsightJobRepository, IXXXJobScheduler mediaInsightJobScheduler)
        {
            _mediaInsightJobRepository = mediaInsightJobRepository;
            _mediaInsightJobScheduler = mediaInsightJobScheduler;
        }
        public async Task<JobResponse> ScheduleJobAsync(JobRequest jobRequest)
        {
            //TODO get the jobRequest id from the repo, fill it in the jobRequest
            //that is sent to the _mediaInsightJobScheduler
            await _mediaInsightJobRepository.CreateJobAsync(jobRequest);

            await _mediaInsightJobScheduler.ScheduleJobAsync(jobRequest);//, jobId);

            return new JobResponse { JobRequest = jobRequest, Status = "Success" };//, JobId = jobId };
        }
        public async Task<XXXInsightJobEntity> GetJobStatusAsync(string jobId)
        {
            return await _mediaInsightJobRepository.GetJobStatusAsync(jobId);
        }
        public async Task RecoverUnfinishedJobsAsync()
        {
            var unfinishedJobs = await _mediaInsightJobRepository.GetUnfinishedJobsAsync();

            foreach (var job in unfinishedJobs)
            {
                await _mediaInsightJobScheduler.ScheduleJobAsync(job);  // Reschedule unfinished jobs
            }
        }
    }
}
