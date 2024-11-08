using HangfireTest.Models;
using HangfireTest.Repositories;
using System.Net;
using System.Threading.Tasks;

namespace HangfireTest.Services
{
    public interface IXXXJobService
    {
        Task<JobResponse> ScheduleJobAsync(JobRequestEntity jobRequest);
        Task <List<JobRequestEntity>> GetAllJobsAsync();

        //Task<JobRequestEntity> GetJobStatusAsync(string jobId);
        //Task RecoverUnfinishedJobsAsync();  // Recover jobs after system restart
        //Task ProcessExistingFilesAsync(BulkProcessRequest request);
        //Task<string> GetJobReportAsync(string jobId);
    }

    public class XXXJobService : IXXXJobService
    {
        private readonly IXXXHangfireJobSchedulerService _hangfireJobSchedulerService;
        private readonly ICustomJobSchedulerService _customJobSchedulerService;
        public XXXJobService(IXXXHangfireJobSchedulerService hangfireJobSchedulerService,
                             ICustomJobSchedulerService customJobSchedulerService)
        {
           
            _hangfireJobSchedulerService = hangfireJobSchedulerService;
            _customJobSchedulerService = customJobSchedulerService;
        }
        public async Task<JobResponse> ScheduleJobAsync(JobRequestEntity jobRequest)
        {
            //await _hangfireJobSchedulerService.ScheduleJobAsync(jobRequest);
            await _customJobSchedulerService.ScheduleJobAsync(jobRequest);
            return new JobResponse { JobRequest = jobRequest, Status = "Success" };
        }
        public async Task<List<JobRequestEntity>> GetAllJobsAsync()
        {
            return await _customJobSchedulerService.GetAllJobsAsync();
        }

        //public async Task<JobRequestEntity> GetJobStatusAsync(string jobId)
        //{
        //    return await _jobRepository.GetJobStatusAsync(jobId);
        //}
        //public async Task RecoverUnfinishedJobsAsync()
        //{
        //    var unfinishedJobs = await _jobRepository.GetUnfinishedJobsAsync();

        //    foreach (var job in unfinishedJobs)
        //    {
        //        //await _hangfireJobSchedulerService.ScheduleJobAsync(job);  // Reschedule unfinished jobs
        //    }
        //}
    }
}
