using ActIntelligenceService.Domain.Models;
using Hangfire;
using HangfireTest.Models;
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
        Task ScheduleJobAsync(JobRequest jobRequest);
    }
    public class XXXHangfireJobSchedulerService : IXXXHangfireJobSchedulerService
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger(); // NLog logger
        private static string inputFilesDirectory = @"C:\Development\HangfireTest\Media\Record";
        private readonly IXXXOperationsService _xxxOperationsService;
        private static List<FileSystemWatcher> watchers = new List<FileSystemWatcher>();

        public XXXHangfireJobSchedulerService(IXXXOperationsService xxxOperationsService)
        {
            _xxxOperationsService = xxxOperationsService;
        }
        public Task ScheduleJobAsync(JobRequest jobRequest)
        {
            // Example with Hangfire
            var invocationType = GetInvocationType(jobRequest);

            EnqueueJob(jobRequest, invocationType);

            return Task.CompletedTask;
        }

        [JobDisplayName("Job Execution")]
        public Task ExecuteJob(JobRequest jobRequest, string jobId)
        {
            // Execute job here by invoking the right processors
            return Task.CompletedTask;
        }
        private InvocationType GetInvocationType(JobRequest jobRequest)
        {
            // Parse BraodcastStartTime and BroadcastEndTime into DateTime objects
            DateTime startTime = DateTime.ParseExact(jobRequest.BroadcastStartTime, "yyyy_MM_dd_HH_mm_ss", null);
            DateTime endTime = DateTime.ParseExact(jobRequest.BroadcastEndTime, "yyyy_MM_dd_HH_mm_ss", null);
            DateTime currentTime = DateTime.Now;

            InvocationType invocationType = InvocationType.RealTime;

            // Determine the type of job based on the time range and real-time flag
            if (endTime < currentTime)
            {
                // Time range is in the past, so use NotRTPRocessing
                invocationType = InvocationType.Batch;
            }
            else if (jobRequest.IsRealTime)
            {
                if (startTime < currentTime && endTime >= currentTime)
                {
                    // Part of the time range is in the past and part is in the future
                    invocationType = InvocationType.Both;  // Process past & future
                }
                else if (startTime > currentTime)
                {
                    // Entire time range is in the future, use RTProcessing
                    invocationType = InvocationType.RealTime;
                }
            }
            else
            {
                // If the request isn't real-time and the time range is in the future, use NotRTPRocessing
                invocationType = InvocationType.Batch;
            }

            return invocationType;
        }
        private void EnqueueJob(JobRequest jobRequest, InvocationType invocationType)
        {
            // Set time zone for recurring jobs
            var localTimeZone = TimeZoneInfo.Local;
            RecurringJobOptions recurringJobOptions = new() { TimeZone = localTimeZone };

            if (jobRequest.IsRecurring)
            {
                // Schedule daily jobs using Cron expressions
                if (invocationType == InvocationType.Batch)
                {
                    RecurringJob.AddOrUpdate(jobRequest.Name!, () => ProcessExistingFiles(jobRequest), jobRequest.CronExpression, recurringJobOptions);
                }
                else if (invocationType == InvocationType.RealTime)
                {
                    RecurringJob.AddOrUpdate(jobRequest.Name!, () => MonitorAndProcessNewFiles(jobRequest), jobRequest.CronExpression, recurringJobOptions);
                }
                else
                {
                    // Both RealTime and NonRT processing needed
                    RecurringJob.AddOrUpdate(jobRequest.Name!, () => MixJobProcessing(jobRequest), jobRequest.CronExpression, recurringJobOptions);
                }
            }
            else
            {
                // Immediate execution of jobs
                if (invocationType == InvocationType.Batch)
                {
                    BackgroundJob.Enqueue(() => ProcessExistingFiles(jobRequest));
                }
                else if (invocationType == InvocationType.RealTime)
                {
                    BackgroundJob.Enqueue(() => MonitorAndProcessNewFiles(jobRequest));
                }
                else
                {
                    // Both RealTime and NonRT processing for immediate execution
                    BackgroundJob.Enqueue(() => MixJobProcessing(jobRequest));
                }
            }
        }
        public async Task ProcessExistingFiles(JobRequest jobRequest)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();

            Logger.Debug($"Executing JOB ID: {jobRequest.Id}");

            // Loop through each channel
            await Task.WhenAll(jobRequest.Channels.Select(async channel =>
            {
                Logger.Debug("Transcribing Channel " + channel);

                var filesInRange = GetFilesForTimeRange(jobRequest, channel);

                // Loop through each file in range
                await Task.WhenAll(filesInRange.Select(async file =>
                {
                    if (jobRequest.Operations.Contains(Operation.DetectFaces))
                    {
                        await _xxxOperationsService.DetectFacesAsync(file);
                    }

                    if (jobRequest.Operations.Contains(Operation.DetectLogo))
                    {
                        await _xxxOperationsService.DetectLogosAsync(file);
                    }

                    if (jobRequest.Operations.Contains(Operation.CreateClosedCaptions) ||
                       jobRequest.Operations.Contains(Operation.DetectKeywords) ||
                       jobRequest.Operations.Contains(Operation.TranslateTranscription) ||
                       jobRequest.Operations.Contains(Operation.VerifyAudioLanguage)
                    )
                    {
                        var mp3File = file.Replace(".mp4", ".mp3");

                        var sttJsonFile = file.Replace(".mp4", ".json");

                        if (!File.Exists(mp3File))
                        {
                            mp3File = await _xxxOperationsService.ExtractAudioAsync(file);// Convert .mp4 to .mp3

                            if (!_xxxOperationsService.IsExtractionSucceeded(mp3File)) // it fails if result .mp3 is less than 5MB
                                return;
                        }

                        if (!File.Exists(sttJsonFile)) // Transcribe only if the json file doesn't exist
                        {
                            InsightResult insightResult = await _xxxOperationsService.TranscribeFileAsync(mp3File, sttJsonFile);

                            if (jobRequest.Operations.Contains(Operation.DetectKeywords))
                            {
                                await _xxxOperationsService.DetectKeywordsAsync(jobRequest, insightResult, channel, sttJsonFile);
                            }

                            if (jobRequest.Operations.Contains(Operation.TranslateTranscription))
                            {
                                await _xxxOperationsService.TranslateTranscriptionAsync(jobRequest, channel, insightResult, sttJsonFile);
                            }

                            if (jobRequest.Operations.Contains(Operation.CreateClosedCaptions))
                            {
                                var outputFolderPath = Path.Combine(new FileInfo(sttJsonFile).DirectoryName!, "closed_captions"); //TODO should be an inner folder with the LngID name
                                if (!Directory.Exists(outputFolderPath))
                                {
                                    Directory.CreateDirectory(outputFolderPath);
                                }
                                await _xxxOperationsService.SaveTranscriptionAsClosedCaptionsAsync(insightResult, mp3File, outputFolderPath!, channel);
                            }
                        }
                    }
                }));

            }));

            stopwatch.Stop();

            var elapsed = stopwatch.ElapsedMilliseconds;

            var elapsedMinutes = stopwatch.ElapsedMilliseconds / 60000.0;

            Logger.Debug($"Finished Executing JOB ID: {jobRequest.Id}, Elapsed Time: {elapsedMinutes} minutes");
        }
        public Task MonitorAndProcessNewFiles(JobRequest jobRequest)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            Logger.Debug($"Starting real-time processing for Rule ID: {jobRequest.Id}");

            // Convert the endTime from string to DateTime and add 6 minutes
            DateTime endTime = DateTime.ParseExact(jobRequest.BroadcastEndTime, "yyyy_MM_dd_HH_mm_ss", CultureInfo.InvariantCulture).AddMinutes(6);

            foreach (var channel in jobRequest.Channels)
            {
                string channelPath = Path.Combine(inputFilesDirectory, channel);

                FileSystemWatcher watcher = new()
                {
                    Path = channelPath,
                    Filter = "*.mp4",
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.CreationTime,
                    IncludeSubdirectories = true
                };

                watcher.Created += async (sender, e) =>
                {
                    Logger.Debug($"New file detected: {e.FullPath} at {DateTime.Now}");
                    await OnNewFileCreated(e.FullPath, jobRequest, channel);
                };

                watcher.EnableRaisingEvents = true;

                watchers.Add(watcher);
            }

            stopwatch.Stop();
            var elapsedMinutes = stopwatch.ElapsedMilliseconds / 60000.0;
            Logger.Debug($"Finished real-time processing for Rule ID: {jobRequest.Id}, Elapsed Time: {elapsedMinutes} minutes");

            return Task.CompletedTask;
        }
        private async Task MixJobProcessing(JobRequest jobRequest)
        {
            await ProcessExistingFiles(jobRequest);

            await MonitorAndProcessNewFiles(jobRequest);//, CancellationToken.None);
        }
        private async Task OnNewFileCreated(string filePath, JobRequest jobRequest, string channel)
        {
            try
            {
                Logger.Debug($"[OnNewFileCreated] File detected: {filePath} at {DateTime.Now}");

                // Wait until the file is ready for processing
                Logger.Debug($"[OnNewFileCreated] Waiting for file to be ready: {filePath}");

                await WaitForFileReady(filePath);

                Logger.Debug($"[OnNewFileCreated] File is ready: {filePath}");

                await ProcessFileAsync(filePath, jobRequest, channel);
            }
            catch (Exception ex)
            {
                Logger.Debug($"[OnNewFileCreated] Error processing file {filePath}: {ex.Message}");
            }
        }
        private async Task WaitForFileReady(string filePath)
        {
            int retries = 10;

            int delay = 1000;

            for (int i = 0; i < retries; i++)
            {
                if (FileIsReady(filePath))
                {
                    return;
                }

                Logger.Debug($"File {filePath} is not ready. Waiting for {delay}ms before retrying.");

                await Task.Delay(delay);
            }

            throw new Exception($"File {filePath} is still not ready after multiple attempts.");
        }
        private bool FileIsReady(string filePath)
        {
            try
            {
                using FileStream fs = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.None);

                return true;
            }
            catch (IOException)
            {
                return false;
            }
        }
        private async Task ProcessFileAsync(string mp4FilePath, JobRequest jobRequest, string channel)
        {
            try
            {
                var stopwatch = Stopwatch.StartNew();

                var mp3File = await _xxxOperationsService.ExtractAudioAsync(mp4FilePath);

                Logger.Debug($"Audio extraction took: {stopwatch.Elapsed.TotalSeconds} seconds");

                var sttFile = mp3File.Replace(".mp3", ".json");

                InsightResult insightResult = await _xxxOperationsService.TranscribeFileAsync(mp3File, sttFile);

                Logger.Debug($"Transcription took: {stopwatch.Elapsed.TotalSeconds} seconds");
              
                //parallelized invocation of RunKeywordsDetection and an RunTranslation
                if (jobRequest.Operations.Contains(Operation.DetectKeywords) &&
                    jobRequest.Operations.Contains(Operation.TranslateTranscription) &&
                    jobRequest.Operations.Contains(Operation.CreateClosedCaptions) )
                {
                    var outputFolderPath = Path.Combine(new FileInfo(sttFile).DirectoryName!, "closed_captions"); //TODO should be an inner folder with the LngID name
                    if (!Directory.Exists(outputFolderPath))
                    {
                        Directory.CreateDirectory(outputFolderPath);
                    }

                    await Task.WhenAll(
                        _xxxOperationsService.DetectKeywordsAsync(jobRequest, insightResult, channel, sttFile),
                        _xxxOperationsService.TranslateTranscriptionAsync(jobRequest, channel, insightResult, sttFile),
                        _xxxOperationsService.SaveTranscriptionAsClosedCaptionsAsync(insightResult, mp3File, outputFolderPath!, channel)
                    );
                }
                else if (jobRequest.Operations.Contains(Operation.DetectKeywords))
                {
                    await _xxxOperationsService.DetectKeywordsAsync(jobRequest, insightResult, channel, sttFile);
                }
                else if (jobRequest.Operations.Contains(Operation.TranslateTranscription))
                {
                    await _xxxOperationsService.TranslateTranscriptionAsync(jobRequest, channel, insightResult, sttFile);
                }
                else if (jobRequest.Operations.Contains(Operation.CreateClosedCaptions))
                {
                    var outputFolderPath = Path.Combine(new FileInfo(sttFile).DirectoryName!, "closed_captions"); //TODO should be an inner folder with the LngID name
                    if (!Directory.Exists(outputFolderPath))
                    {
                        Directory.CreateDirectory(outputFolderPath);
                    }
                    await _xxxOperationsService.SaveTranscriptionAsClosedCaptionsAsync(insightResult, mp3File, outputFolderPath!, channel);
                }

                stopwatch.Stop();
                Logger.Debug($"Total processing time for {mp4FilePath}: {stopwatch.Elapsed.TotalSeconds} seconds");
            }
            catch (Exception ex)
            {
                Logger.Debug($"[ProcessFileAsync] Error processing {mp4FilePath}: {ex.Message}");
                Logger.Debug($"[ProcessFileAsync] Stack Trace: {ex.StackTrace}");
            }
        }
        private IEnumerable<string> GetFilesForTimeRange(JobRequest jobRequest, string channel)
        {
            var channelFolder = Path.Combine(inputFilesDirectory, channel);
            var startDateString = DateTime.ParseExact(jobRequest.BroadcastStartTime, "yyyy_MM_dd_HH_mm_ss", null).ToString("yyyy_MM_dd");
            var channelWithDateFolder = Path.Combine(channelFolder, startDateString);

            var allFiles = Directory.GetFiles(channelWithDateFolder, "*.mp4");
            //foreach ( var file in allFiles)
            //{
            //    var res = FileMatchesTimeRange(file, rule.BraodcastStartTime, rule.BroadcastEndTime);
            //}
            return allFiles.Where(file => FileMatchesTimeRange(file, jobRequest.BroadcastStartTime, jobRequest.BroadcastEndTime));
        }
        private bool FileMatchesTimeRange(string filePath, string startTime, string endTime)
        {
            // Extract the timestamp portion from the file name by skipping the channel name part
            string fileName = Path.GetFileNameWithoutExtension(filePath);

            // Assuming the channel name ends right before the timestamp, separated by an underscore
            // Example: channel01_2024_10_07_00_05_00, so we remove the first part
            var parts = fileName.Split('_');

            // The timestamp starts after the channel name (parts from index 1 onwards)
            if (parts.Length < 6) return false; // Make sure the file name has the expected format

            // Recreate the timestamp from parts[1] to parts[6]
            string fileTimestampStr = $"{parts[1]}_{parts[2]}_{parts[3]}_{parts[4]}_{parts[5]}_{parts[6]}";

            // Define the format that matches the file timestamp
            string format = "yyyy_MM_dd_HH_mm_ss";

            // Attempt to parse the file timestamp into a DateTime object
            if (DateTime.TryParseExact(fileTimestampStr, format, null, System.Globalization.DateTimeStyles.None, out DateTime fileTimestamp))
            {
                // Convert startTime and endTime strings into DateTime objects
                if (DateTime.TryParseExact(startTime, format, null, System.Globalization.DateTimeStyles.None, out DateTime startDateTime) &&
                    DateTime.TryParseExact(endTime, format, null, System.Globalization.DateTimeStyles.None, out DateTime endDateTime))
                {
                    // Handle cases where the time range spans multiple days
                    if (startDateTime <= endDateTime)
                    {
                        // Check if the file timestamp is within the specified time range
                        return fileTimestamp >= startDateTime && fileTimestamp <= endDateTime;
                    }
                    else
                    {
                        // If the end time is before the start time, it means the range spans multiple days
                        return fileTimestamp >= startDateTime || fileTimestamp <= endDateTime;
                    }
                }
            }

            // Return false if the file timestamp doesn't match or couldn't be parsed
            return false;
        }

    }
}
