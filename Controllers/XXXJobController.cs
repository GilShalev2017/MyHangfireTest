using Docker.DotNet.Models;
using HangfireTest.Models;
using HangfireTest.Repositories;
using HangfireTest.Services;
using Microsoft.AspNetCore.Mvc;
using NLog;

namespace HangfireTest.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class XXXJobController : ControllerBase
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger(); // NLog logger
        private readonly IXXXJobService _xxxJobService;
        public XXXJobController(IXXXJobService xxxJobService)
        {
            _xxxJobService = xxxJobService;
        }

        [HttpPost("schedule")]
        public async Task<IActionResult> ScheduleJob([FromBody] JobRequestEntity jobRequest)
        {
            //TODO validate request
            if (jobRequest == null)
            {
                Logger.Error("JobRequest is null.");
                return BadRequest("JobRequest cannot be null.");
            }
            
            PrintJobRequest(jobRequest);

            var jobResponse = await _xxxJobService.ScheduleJobAsync(jobRequest);

            if (jobResponse.Status == "Success")
            {
                return Ok(new { Message = "Added JobRequest Successfully", JobRequest = jobRequest });
            }

            return BadRequest(jobResponse.Errors);
        }
        private void PrintJobRequest(JobRequestEntity jobRequest)
        {
            string logMessage = $"Received JobRequest:\n" +
                                $"- Name: {jobRequest.Name}\n" +
                                $"- IsRealTime: {jobRequest.IsRealTime}\n" +
                                $"- IsRecurring: {jobRequest.IsRecurring}\n" +
                                $"- ExecutionTime: {jobRequest.ExecutionTime}\n" +
                                $"- CronExpression: {jobRequest.CronExpression}\n" +
                                $"- Channels: {string.Join(", ", jobRequest.Channels)}\n" +
                                $"- BraodcastStartTime: {jobRequest.BroadcastStartTime}\n" +
                                $"- BroadcastEndTime: {jobRequest.BroadcastEndTime}\n" +
                                $"- Keywords: {string.Join(", ", jobRequest.Keywords ?? new List<string>())}\n" +
                                $"- Operations: {string.Join(", ", jobRequest.Operations ?? new List<string>())}\n" +
                                $"- ExpectedAudioLanguage: {jobRequest.ExpectedAudioLanguage}\n" +
                                $"- TranslationLanguages: {string.Join(", ", jobRequest.TranslationLanguages ?? new List<string>())}";

            Logger.Debug(logMessage);
        }

        [HttpGet]
        public async Task<List<JobRequestEntity>> GetAllJobs()
        {
            var jobs = await _xxxJobService.GetAllJobsAsync();

            return jobs;
        }

        //[HttpGet("status/{jobId}")]
        //public async Task<IActionResult> GetJobStatus(string jobId)
        //{
        //    var status = await _xxxJobService.GetJobStatusAsync(jobId);

        //    return Ok(status);
        //}

        //[HttpGet("report/{jobId}")]
        //public async Task<IActionResult> GetJobReport(string jobId)
        //{
        //    var report = await _mediaInsightJobService.GetJobReportAsync(jobId);

        //    return Ok(report);
        //}

        //[HttpPost("bulkprocess")]
        //public async Task<IActionResult> ProcessExistingFiles([FromBody] JobRequest request)
        //{
        //    var result = await _mediaInsightJobService.ProcessExistingFilesAsync(request);
        //    return Ok(result);
        //}
    }
}