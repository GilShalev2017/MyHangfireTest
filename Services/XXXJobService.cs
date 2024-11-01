using HangfireTest.Models;
using HangfireTest.Repositories;
using System.Net;

namespace HangfireTest.Services
{
    public interface IXXXJobService
    {
        Task<JobResponse> ScheduleJobAsync(JobRequest jobRequest);
        Task<JobRequestEntity> GetJobStatusAsync(string jobId);
        Task RecoverUnfinishedJobsAsync();  // Recover jobs after system restart
        //Task ProcessExistingFilesAsync(BulkProcessRequest request);
        //Task<string> GetJobReportAsync(string jobId);
    }

    public class XXXJobService : IXXXJobService
    {
        private readonly IXXXJobRepository _jobRepository;
        private readonly IXXXHangfireJobSchedulerService _hangfireJobSchedulerService;
        private readonly ICustomJobSchedulerService _customJobSchedulerService;
        public XXXJobService(IXXXJobRepository jobRepository, 
                             IXXXHangfireJobSchedulerService hangfireJobSchedulerService,
                             ICustomJobSchedulerService customJobSchedulerService)
        {
            _jobRepository = jobRepository;
            _hangfireJobSchedulerService = hangfireJobSchedulerService;
            _customJobSchedulerService = customJobSchedulerService;
        }
        public async Task<JobResponse> ScheduleJobAsync(JobRequest jobRequest)
        {
            //TODO get the jobRequest id from the repo, fill it in the jobRequest
            //that is sent to the _hangfireJobSchedulerService
            await _jobRepository.CreateJobAsync(jobRequest);

            await _hangfireJobSchedulerService.ScheduleJobAsync(jobRequest);//, jobId);

            return new JobResponse { JobRequest = jobRequest, Status = "Success" };//, JobId = jobId };
        }
        public async Task<JobRequestEntity> GetJobStatusAsync(string jobId)
        {
            return await _jobRepository.GetJobStatusAsync(jobId);
        }
        public async Task RecoverUnfinishedJobsAsync()
        {
            var unfinishedJobs = await _jobRepository.GetUnfinishedJobsAsync();

            foreach (var job in unfinishedJobs)
            {
                //await _hangfireJobSchedulerService.ScheduleJobAsync(job);  // Reschedule unfinished jobs
            }
        }
    }
}
