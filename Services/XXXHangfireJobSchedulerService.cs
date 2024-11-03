using ActIntelligenceService.Domain.Models;
using Hangfire;
using HangfireTest.Models;
using HangfireTest.Repositories;
using NLog;
using System.Diagnostics;
using System.Globalization;

namespace HangfireTest.Services
{
    public static class Operation
    {
        public const string DetectKeywords = "DetectKeywords";
        public const string CreateClosedCaptions = "CreateClosedCaptions";
        public const string TranslateTranscription = "TranslateTranscription";
        public const string VerifyAudioLanguage = "VerifyAudioLanguage";
        public const string DetectFaces = "DetectFaces";
        public const string DetectLogo = "DetectLogo";
    }

    public interface IXXXHangfireJobSchedulerService
    {
        Task ScheduleJobAsync(JobRequestEntity jobRequest);
    }
    public class XXXHangfireJobSchedulerService : IXXXHangfireJobSchedulerService
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger(); // NLog logger
        private static string inputFilesDirectory = @"C:\Development\HangfireTest\Media\Record";
        private readonly IXXXOperationsService _xxxOperationsService;
        private static List<FileSystemWatcher> watchers = new List<FileSystemWatcher>();
        private readonly IXXXJobRepository _jobRepository;

        public XXXHangfireJobSchedulerService(IXXXOperationsService xxxOperationsService, IXXXJobRepository jobRepository)
        {
            _xxxOperationsService = xxxOperationsService;
            _jobRepository = jobRepository;
        }
        public async Task ScheduleJobAsync(JobRequestEntity jobRequest)
        {
            await _jobRepository.CreateJobAsync(jobRequest);
           
            var invocationType = _xxxOperationsService.GetInvocationType(jobRequest);
            
            // Example with Hangfire
            EnqueueJob(jobRequest, invocationType);
        }
        private void EnqueueJob(JobRequestEntity jobRequest, InvocationType invocationType)
        {
            // Set time zone for recurring jobs
            var localTimeZone = TimeZoneInfo.Local;
            RecurringJobOptions recurringJobOptions = new() { TimeZone = localTimeZone };

            if (jobRequest.IsRecurring)
            {
                // Schedule daily jobs using Cron expressions
                if (invocationType == InvocationType.Batch)
                {
                    RecurringJob.AddOrUpdate(jobRequest.Name!, () => _xxxOperationsService.ProcessExistingFiles(jobRequest), jobRequest.CronExpression, recurringJobOptions);
                }
                else if (invocationType == InvocationType.RealTime)
                {
                    RecurringJob.AddOrUpdate(jobRequest.Name!, () => _xxxOperationsService.MonitorAndProcessNewFiles(jobRequest), jobRequest.CronExpression, recurringJobOptions);
                }
                else
                {
                    // Both RealTime and NonRT processing needed
                    RecurringJob.AddOrUpdate(jobRequest.Name!, () => _xxxOperationsService.MixJobProcessing(jobRequest), jobRequest.CronExpression, recurringJobOptions);
                }
            }
            else
            {
                // Immediate execution of jobs
                if (invocationType == InvocationType.Batch)
                {
                    BackgroundJob.Enqueue(() => _xxxOperationsService.ProcessExistingFiles(jobRequest));
                }
                else if (invocationType == InvocationType.RealTime)
                {
                    BackgroundJob.Enqueue(() => _xxxOperationsService.MonitorAndProcessNewFiles(jobRequest));
                }
                else
                {
                    // Both RealTime and NonRT processing for immediate execution
                    BackgroundJob.Enqueue(() => _xxxOperationsService.MixJobProcessing(jobRequest));
                }
            }
        }
    }
}
