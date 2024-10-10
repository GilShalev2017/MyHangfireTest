using IntelligenceServiceTest;
using System.Text.Json;
using System.Text.Json.Serialization;
using Hangfire;
using Hangfire.Mongo;
using Hangfire.Mongo.Migration.Strategies;
using HangfireTest.Models;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Diagnostics;
using ActIntelligenceService.Domain.Models;
using ActIntelligenceService.Providers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ActIntelligenceService.Domain.Services;
using SharpCompress.Common;
using System.Text;
using FFMpegCore;
using System.Runtime.CompilerServices;
using System.Net;
using HangfireTest.Utility;
using System.Globalization;

public class Program
{
    // Directory to store files
    static string inputFilesDirectory = @"C:\Development\HangfireTest\Media\Record";

    private static readonly Dictionary<string, string> languageIds = new();
    public static async Task Main(string[] args)
    {
        var app = CreateApplicationWithHangfire(args);

        InitProvidersEnvironment();

        app.MapPost("/run-jobs", async ([FromBody] JobRequest jobRequest) =>
        {
            PrintJobRequest(jobRequest);

            ProcessJob(jobRequest);

            return Results.Ok(new { Message = "Added JobRequest Successfully", JobRequest = jobRequest });
        });

        app.Run();
    }
    private static WebApplication CreateApplicationWithHangfire(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        var mongoStorageOptions = new MongoStorageOptions
        {
            MigrationOptions = new MongoMigrationOptions
            {
                MigrationStrategy = new MigrateMongoMigrationStrategy(),
            },

            CheckQueuedJobsStrategy = CheckQueuedJobsStrategy.TailNotificationsCollection, // Use polling instead of change streams

            SlidingInvisibilityTimeout = TimeSpan.FromMinutes(1) // Default is 5 minutes (300 seconds)
        };

        string mongoDatabaseName = "hangfire_db";

        //string mongoConnectionString = "mongodb://localhost:27017,localhost:27018/?replicaSet=rs0"; //when using replicas

        string mongoConnectionString = "mongodb://localhost:27017"; //no replicase used

        GlobalConfiguration.Configuration.UseMongoStorage(mongoConnectionString, mongoDatabaseName, mongoStorageOptions);

        GlobalConfiguration.Configuration.UseMongoStorage(mongoConnectionString, mongoDatabaseName, mongoStorageOptions);

        builder.Services.AddHangfire(config => config.UseMongoStorage(mongoConnectionString, mongoDatabaseName));

        builder.Services.AddHangfireServer();

        var app = builder.Build();

        app.UseHangfireDashboard();

        return app;
    }
    private static async void InitProvidersEnvironment()
    {
        Startup.AssemblyInit(null);
        Startup.languageSvc.InitAsync();
        Startup.aiProviderSvc.InitAsync().Wait();
        List<LanguageDm>? list = await Startup.languageSvc.GetAllAsync();
        if (list != null)
        {
            foreach (LanguageDm languageDm in list)
            {
                languageIds.Add(languageDm.DisplayName, languageDm.EnglishName!);
            }
        }
    }
    private static void PrintJobRequest(JobRequest jobRequest)
    {
        string logMessage = $"Processing media rule:\n" +
                            $"- Name: {jobRequest.Name}\n" +
                            $"- IsRecurring: {jobRequest.IsRecurring}\n" +
                            $"- Channels: {string.Join(", ", jobRequest.Channels)}\n" +
                            $"- StartTime: {jobRequest.StartTime}\n" +
                            $"- EndTime: {jobRequest.EndTime}\n" +
                            $"- Keywords: {string.Join(", ", jobRequest.Keywords ?? new List<string>())}";

        Console.WriteLine(logMessage);
    }
    public static void ProcessJob(JobRequest jobRequest)
    {
        // Parse StartTime and EndTime into DateTime objects
        DateTime startTime = DateTime.ParseExact(jobRequest.StartTime, "yyyy_MM_dd_HH_mm_ss", null);
        DateTime endTime = DateTime.ParseExact(jobRequest.EndTime, "yyyy_MM_dd_HH_mm_ss", null);
        DateTime currentTime = DateTime.Now;

        InvocationType invocationType = InvocationType.RT;

        // Determine the type of job based on the time range and real-time flag
        if (endTime < currentTime)
        {
            // Time range is in the past, so use NotRTPRocessing
            invocationType = InvocationType.NoneRT;
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
                invocationType = InvocationType.RT;
            }
        }
        else
        {
            // If the request isn't real-time and the time range is in the future, use NotRTPRocessing
            invocationType = InvocationType.NoneRT;
        }

        // Set time zone for recurring jobs
        var localTimeZone = TimeZoneInfo.Local;
        RecurringJobOptions recurringJobOptions = new() { TimeZone = localTimeZone };

        if (jobRequest.IsRecurring)
        {
            // Schedule daily jobs using Cron expressions
            if (invocationType == InvocationType.NoneRT)
            {
                RecurringJob.AddOrUpdate(jobRequest.Name!, () => ProcessExistingFiles(jobRequest), jobRequest.CronExpression, recurringJobOptions);
            }
            else if (invocationType == InvocationType.RT)
            {
                RecurringJob.AddOrUpdate(jobRequest.Name!, () => MonitorAndProcessNewFiles(jobRequest), jobRequest.CronExpression, recurringJobOptions);
            }
            else
            {
                // Both RT and NonRT processing needed
                RecurringJob.AddOrUpdate(jobRequest.Name!, () => MixJobProcessing(jobRequest), jobRequest.CronExpression, recurringJobOptions);
            }
        }
        else
        {
            // Immediate execution of jobs
            if (invocationType == InvocationType.NoneRT)
            {
                BackgroundJob.Enqueue(() => ProcessExistingFiles(jobRequest));
            }
            else if (invocationType == InvocationType.RT)
            {
                BackgroundJob.Enqueue(() => MonitorAndProcessNewFiles(jobRequest));
            }
            else
            {
                // Both RT and NonRT processing for immediate execution
                BackgroundJob.Enqueue(() => MixJobProcessing(jobRequest));
            }
        }
    }
    public static async Task ProcessExistingFiles(JobRequest jobRequest)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();

        Console.WriteLine($"Executing Rule ID: {jobRequest.Id}");

        // Loop through each channel
        await Task.WhenAll(jobRequest.Channels.Select(async channel =>
        {
            Console.WriteLine("Transcribing Channel " + channel);

            var filesInRange = GetFilesForTimeRange(jobRequest, channel);

            // Loop through each file in range
            await Task.WhenAll(filesInRange.Select(async file =>
            {
                if (jobRequest.JobTypes.Contains(JobType.FaceDetection))
                {
                    await RunFaceDetection();
                }

                if (jobRequest.JobTypes.Contains(JobType.LogoDetection))
                {
                    await RunLogoDetection();
                }

                if (jobRequest.JobTypes.Contains(JobType.CreateClosedCaptions) ||
                   jobRequest.JobTypes.Contains(JobType.KeywordsDetection) ||
                   jobRequest.JobTypes.Contains(JobType.Translation) ||
                   jobRequest.JobTypes.Contains(JobType.VerifyAudioLanguage)
                )
                {
                    var mp3File = file.Replace(".mp4", ".mp3");

                    var sttJsonFile = file.Replace(".mp4", ".json");

                    if (!File.Exists(mp3File))
                    {
                        // Convert .mp4 to .mp3
                        mp3File = await ExtractAudioAsync(file);

                        if (!IsExtractionSucceeded(mp3File)) // it fails if result .mp3 is less than 5MB
                            return;
                    }

                    if (!File.Exists(sttJsonFile)) // Transcribe only if the json file doesn't exist
                    {
                        InsightResult insightResult = await TranscribeFileAsync(mp3File);

                        await SaveTranscriptionResultAsync(insightResult, sttJsonFile);

                        if (jobRequest.JobTypes.Contains(JobType.KeywordsDetection))
                        {
                            await RunKeywordsDetection(jobRequest, insightResult, channel, sttJsonFile);
                        }

                        if (jobRequest.JobTypes.Contains(JobType.Translation))
                        {
                            await RunTranslation(jobRequest, channel, insightResult);
                        }
                    }
                }
            }));

        }));

        stopwatch.Stop();

        var elapsed = stopwatch.ElapsedMilliseconds;

        var elapsedMinutes = stopwatch.ElapsedMilliseconds / 60000.0;

        Console.WriteLine($"Finished Executing Rule ID: {jobRequest.Id}, Elapsed Time: {elapsedMinutes} minutes");
    }
    public static async Task MonitorAndProcessNewFiles(JobRequest jobRequest)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        Console.WriteLine($"Starting real-time processing for Rule ID: {jobRequest.Id}");

        // Convert the endTime from string to DateTime and add 6 minutes
        DateTime endTime = DateTime.ParseExact(jobRequest.EndTime, "yyyy_MM_dd_HH_mm_ss", CultureInfo.InvariantCulture).AddMinutes(6);

        // Create a list of tasks to process each channel in parallel
        var channelTasks = new List<Task>();

        // Use FileSystemWatcher to listen for new files being created in the channel directories
        foreach (var channel in jobRequest.Channels)
        {
            var task = Task.Run(async () =>
            {
                string channelPath = Path.Combine(inputFilesDirectory, channel);

                using var watcher = new FileSystemWatcher(channelPath)
                {
                    Filter = "*.mp4", // Only watch for new .mp4 files
                    IncludeSubdirectories = true // Monitor subdirectories as well
                };

                watcher.Created += async (sender, e) =>
                {
                    Console.WriteLine($"New file detected: {e.FullPath}");
                    string file = e.FullPath;

                    if (jobRequest.JobTypes.Contains(JobType.FaceDetection))
                    {
                        await RunFaceDetection();
                    }

                    if (jobRequest.JobTypes.Contains(JobType.LogoDetection))
                    {
                        await RunLogoDetection();
                    }

                    if (jobRequest.JobTypes.Contains(JobType.CreateClosedCaptions) ||
                       jobRequest.JobTypes.Contains(JobType.KeywordsDetection) ||
                       jobRequest.JobTypes.Contains(JobType.Translation) ||
                       jobRequest.JobTypes.Contains(JobType.VerifyAudioLanguage))
                    {
                        var mp3File = file.Replace(".mp4", ".mp3");
                        var sttJsonFile = file.Replace(".mp4", ".json");

                        if (!File.Exists(mp3File))
                        {
                            // Convert .mp4 to .mp3
                            mp3File = await ExtractAudioAsync(file);

                            if (!IsExtractionSucceeded(mp3File)) // it fails if result .mp3 is less than 5MB
                                return;
                        }

                        ///////////////////////////////////////////////////////////////
                        // Introduce a retry mechanism with delay before accessing the mp3 file
                        bool fileReady = false;
                        int retries = 5; // Retry up to 5 times
                        int delayMilliseconds = 2000; // Wait 2 seconds between retries

                        while (!fileReady && retries > 0)
                        {
                            try
                            {
                                // Attempt to open the file exclusively to check if it's ready
                                using (var fileStream = File.Open(mp3File, FileMode.Open, FileAccess.Read, FileShare.None))
                                {
                                    fileReady = true;
                                }
                            }
                            catch (IOException)
                            {
                                // The file is still in use by another process, wait before retrying
                                Console.WriteLine($"File {mp3File} is being used by another process, retrying...");
                                await Task.Delay(delayMilliseconds);
                                retries--;
                            }
                        }
                        if (!fileReady)
                        {
                            Console.WriteLine($"Failed to access {mp3File} after multiple retries. Skipping file.");
                            return;
                        }
                        ///////////////////////////////////////////////

                        if (!File.Exists(sttJsonFile)) // Transcribe only if the json file doesn't exist
                        {
                            InsightResult insightResult = await TranscribeFileAsync(mp3File);
                            await SaveTranscriptionResultAsync(insightResult, sttJsonFile);

                            if (jobRequest.JobTypes.Contains(JobType.KeywordsDetection))
                            {
                                await RunKeywordsDetection(jobRequest, insightResult, channel, sttJsonFile);
                            }

                            if (jobRequest.JobTypes.Contains(JobType.Translation))
                            {
                                await RunTranslation(jobRequest, channel, insightResult);
                            }
                        }
                    }
                };

                watcher.EnableRaisingEvents = true;

                // Keep the FileSystemWatcher running until the end time is reached
                while (DateTime.Now < endTime)
                {
                    await Task.Delay(30000); // Check every 30 seconds
                }
            });

            // Add the task to the list of tasks
            channelTasks.Add(task);
        }

        // Wait for all channel tasks to complete in parallel
        await Task.WhenAll(channelTasks);

        stopwatch.Stop();
        var elapsedMinutes = stopwatch.ElapsedMilliseconds / 60000.0;
        Console.WriteLine($"Finished real-time processing for Rule ID: {jobRequest.Id}, Elapsed Time: {elapsedMinutes} minutes");
    }
    public static async Task MixJobProcessing(JobRequest jobRequest)
    {
        await ProcessExistingFiles(jobRequest);

        await MonitorAndProcessNewFiles(jobRequest);//, CancellationToken.None);
    }
    public static async Task<List<KeywordMatch>> FindKeywordsAsync(InsightResult insightResult, List<string> keywords, string channelName, string fileName)
    {
        var keywordMatches = new List<KeywordMatch>();

        // Loop through each transcript item in the TimeCodedContent
        if (insightResult.TimeCodedContent != null)
        {
            foreach (var transcript in insightResult.TimeCodedContent)
            {
                // Check each keyword against the transcript text
                foreach (var keyword in keywords)
                {
                    if (transcript.Text.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        keywordMatches.Add(new KeywordMatch
                        {
                            Keyword = keyword,
                            StartInSeconds = transcript.StartInSeconds,
                            EndInSeconds = transcript.EndInSeconds,
                            //ChannelName = channelName,
                            //FileName = fileName
                        });
                    }
                }
            }
        }

        // Simulate async operation (if needed for more complex scenarios)
        await Task.CompletedTask;

        return keywordMatches;
    }
    public static async Task SaveKeywordMatchesToFileAsync(string filePath, List<KeywordMatch> keywordMatches)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        // Serialize the keyword matches list to JSON
        string jsonString = JsonSerializer.Serialize(keywordMatches, options);

        // Write the JSON string to a file
        await File.WriteAllTextAsync(filePath, jsonString);
    }
    private static IEnumerable<string> GetFilesForTimeRange(JobRequest jobRequest, string channel)
    {
        var channelFolder = Path.Combine(inputFilesDirectory, channel);
        var startDateString = DateTime.ParseExact(jobRequest.StartTime, "yyyy_MM_dd_HH_mm_ss", null).ToString("yyyy_MM_dd");
        var channelWithDateFolder = Path.Combine(channelFolder, startDateString);

        var allFiles = Directory.GetFiles(channelWithDateFolder, "*.mp4");
        //foreach ( var file in allFiles)
        //{
        //    var res = FileMatchesTimeRange(file, rule.StartTime, rule.EndTime);
        //}
        return allFiles.Where(file => FileMatchesTimeRange(file, jobRequest.StartTime, jobRequest.EndTime));
    }
    private static bool IsExtractionSucceeded(string mp3File)
    {
        long filesizebytes = new FileInfo(mp3File).Length;
        if (filesizebytes < 5000000) // Less than 5MB
        {
            Console.WriteLine($"Conversion failed for file {mp3File}. Skipping transcription.");
            return false;  // Exit this task and move to the next file
        }
        return true;
    }
    private static async Task RunKeywordsDetection(JobRequest jobRequest, InsightResult insightResult, string channel, string sttJsonFile)
    {
        if (jobRequest.Keywords != null && jobRequest.Keywords.Count > 0)
        {
            var keywordMatches = await FindKeywordsAsync(insightResult, jobRequest.Keywords, channel, sttJsonFile);

            if (keywordMatches.Count > 0)
            {
                string keywordFile = sttJsonFile.Replace(".json", "_keywords.json");
                await SaveKeywordMatchesToFileAsync(keywordFile, keywordMatches);
            }
        }
    }
    private static bool FileMatchesTimeRange(string filePath, string startTime, string endTime)
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
    private static async Task<string> ExtractAudioAsync(string videoFilePath)
    {
        // Replace the file extension from .mp4 to .mp3
        var audioFilePath = videoFilePath.Replace(".mp4", ".mp3");

        // Prepare the ffmpeg process to extract audio
        var ffmpeg = new ProcessStartInfo
        {
            FileName = "ffmpeg",
            Arguments = $"-i \"{videoFilePath}\" -q:a 0 -map a \"{audioFilePath}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process
        {
            StartInfo = ffmpeg
        };

        var output = new StringBuilder();
        var error = new StringBuilder();

        // Capture standard output and error
        process.OutputDataReceived += (sender, args) => output.AppendLine(args.Data);
        process.ErrorDataReceived += (sender, args) => error.AppendLine(args.Data);

        // Start the process
        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        // Wait for the process to exit asynchronously
        await process.WaitForExitAsync();

        // Check if ffmpeg succeeded
        if (process.ExitCode != 0)
        {
            var errorMessage = $"ffmpeg failed with exit code {process.ExitCode}. Error: {error}";
            Console.WriteLine(errorMessage);
            throw new InvalidOperationException(errorMessage);
        }
        else
        {
            Console.WriteLine($"ffmpeg conversion of .mp4 {videoFilePath} to .mp3 {audioFilePath} completed successfully.");
        }

        // Return the path of the extracted audio file
        return audioFilePath;
    }
    private static async Task<InsightResult> TranscribeFileAsync(string audioFilePath)
    {
        // Call OpenAI API or local ASR model for transcription
        //return await Task.FromResult("Transcribed Text from ASR"); // Placeholder for actual ASR logic

        var res = await GetAudioBasedCaptionsTest(SystemInsightTypes.Transcription, ProviderType.OpenAI, audioFilePath);

        return res!;
    }
    private static async Task SaveTranscriptionResultAsync(InsightResult insightResult, string filePath)
    {
        // Trim leading and trailing whitespaces in the Text property
        foreach (var item in insightResult.TimeCodedContent)
        {
            item.Text = item.Text.Trim();
        }

        var options = new JsonSerializerOptions
        {
            WriteIndented = true, // Makes the JSON more readable with indentation
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull, // Ignore null properties
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping // Allow direct Unicode characters
        };

        // Serialize the object to a JSON string
        string jsonString = JsonSerializer.Serialize(insightResult, options);

        // Write the JSON string to a file
        await File.WriteAllTextAsync(filePath, jsonString);

        await SaveClosedCaption();
    }
    private static async Task RunTranslation(JobRequest jobRequest, string channel, InsightResult insightResult)
    {
        await Task.CompletedTask;
    }
    private static async Task SaveClosedCaption()
    {
        await Task.CompletedTask;
    }
    private static async Task RunFaceDetection()
    {
        await Task.CompletedTask;
    }
    private static async Task RunLogoDetection()
    {
        await Task.CompletedTask;
    }
    private static ProviderBase? GetProvider(ProviderType providerType, InsightInputData insightInputData, InsightRequest insightRequest)
    {
        //Find all providers by input & insight request type (Translation, Transcription, Summary...)
        var providers = Startup.aiProviderSvc.GetAllAIProvidersForInsightProcessing(insightInputData, insightRequest);
        //Find provider by type (azure, openai, google...)
        var provider = providers.FirstOrDefault(p =>
            p.ProviderMetadata != null &&
            p.ProviderMetadata.DisplayName!.Replace(" ", "").ToLower().StartsWith(providerType.ToString().ToLower()));
        return provider;
    }
    private static InsightInputData GetAudioInputData(string filePath)
    {
        InsightInputData insightInputData = new()
        {
            AudioInput = new AudioDTO { FilePath = filePath }
        };

        return insightInputData;
    }
    private static InsightRequest GetInsightRequest(string insightType, string? sourceLanguage = null, string? targetLanguage = null)
    {
        var insightRequest = new InsightRequest
        {
            InsightType = insightType,
        };
        if (sourceLanguage != null && targetLanguage != null)
        {
            var sourceLanguageCode = languageIds[sourceLanguage];
            var targetLanguageCode = languageIds[targetLanguage];

            insightRequest.AIParameters = new List<KeyValuePair<string, string>>();
            insightRequest.AIParameters.Add(new KeyValuePair<string, string>(AIParametersKeys.SourceLanguageKey, sourceLanguageCode));
            insightRequest.AIParameters.Add(new KeyValuePair<string, string>(AIParametersKeys.TargetLanguageKey, targetLanguageCode));
        }
        return insightRequest;
    }
    private static async Task<InsightResult?> GetAudioBasedCaptionsTest(string insightType, ProviderType providerType, string audioFilePath)
    {
        var insightInputData = GetAudioInputData(audioFilePath);

        InsightRequest insightRequest;

        insightRequest = GetInsightRequest(insightType);

        var provider = GetProvider(providerType, insightInputData, insightRequest);

        if (provider == null)
        {
            Assert.Fail();
            return null;
        }

        var results = await provider.ProcessAsync(insightInputData, insightRequest);

        return results[0];
    }
}

//private static void ExecuteJob(JobRequest jobRequest)
//{
//    if (jobRequest.IsRecurring) //schedule daily (list of days)
//    {
//        var localTimeZone = TimeZoneInfo.Local;

//        RecurringJobOptions recurringJobOptions = new() { TimeZone = localTimeZone };

//        RecurringJob.AddOrUpdate(jobRequest.Name!, () => NotRTJobProcessing(jobRequest), jobRequest.CronExpression, recurringJobOptions);
//    }
//    else //execute right now
//    {
//        BackgroundJob.Enqueue(() => NotRTJobProcessing(jobRequest));
//    }
//}

//Just inner loop parallalized
/*
public static async Task ExecuteRuleAsyncPartialParallelism(JobRequest rule)
{
    Stopwatch stopwatch = Stopwatch.StartNew();

    Console.WriteLine($"Executing Rule ID: {rule.Id}");

    foreach (var channel in rule.Channels)
    {
        Console.WriteLine("Transcribing Channel " + channel);

        var channelFolder = Path.Combine(inputFilesDirectory, channel);
        var startDateString = DateTime.ParseExact(rule.StartTime, "yyyy_MM_dd_HH_mm_ss", null).ToString("yyyy_MM_dd");
        var channelWithDateFolder = Path.Combine(channelFolder, startDateString);

        var filesInRange = GetFilesForTimeRange(rule, channelWithDateFolder);

        // Parallelize processing of files in range
        var fileProcessingTasks = filesInRange.Select(async file =>
        {
            var mp3File = file.Replace(".mp4", ".mp3");
            var sttJsonFile = file.Replace(".mp4", ".json");

            if (!File.Exists(mp3File))
            {
                mp3File = await ConvertToMp3Async(file);  // Await the conversion process
            }

            if (!File.Exists(sttJsonFile))
            {
                InsightResult insightResult = await TranscribeFileAsync(mp3File);
                await SerializeInsightResultToFileAsync(insightResult, sttJsonFile);
            }
        });

        // Await all file processing tasks
        await Task.WhenAll(fileProcessingTasks);

        stopwatch.Stop();

        var elapsed = stopwatch.ElapsedMilliseconds;

        var elapsedMinutes = stopwatch.ElapsedMilliseconds / 60000.0;

        Console.WriteLine($"Finished Executing Rule ID: {rule.Id}, Elapsed Time: {elapsedMinutes} minutes");
    }
}
*/

/*
public static async Task ExecuteRuleAsyncNoParallelism(JobRequest rule)
{
    Stopwatch stopwatch = Stopwatch.StartNew();

    Console.WriteLine($"Executing Rule ID: {rule.Id}");

    foreach (var channel in rule.Channels)
    {
        Console.WriteLine("Transcribing Channel " + channel);

        var channelFolder = Path.Combine(inputFilesDirectory, channel);
        var startDateString = DateTime.ParseExact(rule.StartTime, "yyyy_MM_dd_HH_mm_ss", null).ToString("yyyy_MM_dd");
        var channelWithDateFolder = Path.Combine(channelFolder, startDateString);

        var filesInRange = GetFilesForTimeRange(rule, channelWithDateFolder);

        foreach (var file in filesInRange)
        {
            //Save the converted file to storage location
            //Use ActExport to get legitimate .mp4 file
            //var proxyMp4File = ConvertToMp4ProxyFile(file);

            var mp3File = file.Replace(".mp4", ".mp3");

            var sttJsonFile = file.Replace(".mp4", ".json");

            if (!File.Exists(mp3File))
            {
                try
                {
                    // Await each conversion sequentially
                    mp3File = await ConvertToMp3Async(file);
                    long filesizebytes = new FileInfo(mp3File).Length;

                    // Check if conversion failed based on file size
                    if (filesizebytes < 5000000)
                    {
                        Console.WriteLine($"Conversion Failed for {file}. Moving to the next file.");
                        continue;  // Skip this file and move to the next one
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error converting file {file}: {ex.Message}");
                    continue;  // Skip this file if any error occurs
                }
            }

            if (!File.Exists(sttJsonFile))
            {
                try
                {
                    // Proceed with transcription after successful conversion
                    InsightResult insightResult = await TranscribeFileAsync(mp3File);
                    await SerializeInsightResultToFileAsync(insightResult, sttJsonFile);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error transcribing file {file}: {ex.Message}");
                }
            }
        }
    }

    stopwatch.Stop();

    var elapsed = stopwatch.ElapsedMilliseconds;

    var elapsedMinutes = stopwatch.ElapsedMilliseconds / 60000.0;

    Console.WriteLine($"Finished Executing Rule ID: {rule.Id}, Elapsed Time: {elapsedMinutes} minutes");
}
*/
/*
public static void WriteFile(string fileName, string jobName)
{
    var filePath = Path.Combine(outputFilesDirectory, fileName);
    File.WriteAllText(filePath, $"{jobName} executed at: {DateTime.Now}");
}
*/

//For delayed: for soft RT: for example start transcribing from now but with XXX delay, when we are sure that the records are already ready
//1) BackgroundJob.Schedule(() => ExecuteRuleAsync(rule), TimeSpan.FromMinutes(10)); //invoke in 10 minutes
//For Recurring every day: for instance future news that are set at a specific time!
//2) RecurringJob.AddOrUpdate("recurringJob1", () => ExecuteRuleAsync(rule), "14 15 * * *", localTimeZone); //running every day at localTimeZone, at 14:15
//Immediately: for past time range when we know that the recordings are already there
//3) BackgroundJob.Enqueue(() => ExecuteRuleAsyncFullParallelism(rule));

//app.MapGet("/immediately", () =>
//{
//    BackgroundJob.Enqueue(() => File.WriteAllText(Path.Combine(outputFilesDirectory, "Immediately.txt"), $"Job executed at: {DateTime.Now}"));
//    return "Immediately Execution!";
//});

// Endpoint to enqueue a recurring job 
//app.MapGet("/recurring", () =>
//{
//    //// Recurring job 1: Every day at 1:10 AM
//    RecurringJob.AddOrUpdate("recurringJob1", () => WriteFile("recurring1.txt", "Recurring job 1"), "14 15 * * *", localTimeZone);

//    // Recurring job 2: Every day at 2:10 AM
//    RecurringJob.AddOrUpdate("recurringJob2", () => WriteFile("recurring2.txt", "Recurring job 2"), "15 15 * * *", localTimeZone);

//    // Recurring job 3: Every day at 3:10 AM
//    RecurringJob.AddOrUpdate("recurringJob3", () => WriteFile("recurring3.txt", "Recurring job 3"), "16 15 * * *", localTimeZone);

//    // Recurring job 4: Every day at 4:10 AM
//    RecurringJob.AddOrUpdate("recurringJob4", () => WriteFile("recurring4.txt", "Recurring job 4"), "17 15 * * *", localTimeZone);

//    // Recurring job 5: Every day at 5:10 AM
//    RecurringJob.AddOrUpdate("recurringJob5", () => WriteFile("recurring5.txt", "Recurring job 5"), "18 15 * * *", localTimeZone);

//    // Recurring job 6: Every day at 6:10 AM
//    RecurringJob.AddOrUpdate("recurringJob6", () => WriteFile("recurring6.txt", "Recurring job 6"), "19 15 * * *", localTimeZone);

//    return "NEW 6 recurring jobs created!";
//});

// Endpoint to enqueue a delayed job (runs 1 minute after invocation)
//app.MapGet("/delayed", () =>
//{
//    BackgroundJob.Schedule(() => File.WriteAllText(Path.Combine(outputFilesDirectory, "Delayed.txt"), $"Delayed job executed at: {DateTime.Now}"), TimeSpan.FromMinutes(2));

//    return "Delayed job enqueued to run in 2 minute!";
//});

/*

 private static async Task ExtractAudioAsync(string inputFile, string outputFile)
 {
     var ffmpeg = new ProcessStartInfo
     {
         FileName = "ffmpeg",
         Arguments = $"-i {inputFile} -q:a 0 -map a {outputFile}",
         RedirectStandardOutput = true,
         UseShellExecute = false,
         CreateNoWindow = true
     };

     using var process = Process.Start(ffmpeg);
     if (process != null)
     {
         await process.WaitForExitAsync();
     }
 }*/

//public static async Task Main(string[] args)
//{
//    var builder = WebApplication.CreateBuilder(args);

//    var mongoStorageOptions = new MongoStorageOptions
//    {
//        MigrationOptions = new MongoMigrationOptions
//        {
//            MigrationStrategy = new MigrateMongoMigrationStrategy(),
//            //MigrationLockTimeout = TimeSpan.FromMinutes(2) // Increased timeout as necessary
//        },
//        CheckQueuedJobsStrategy = CheckQueuedJobsStrategy.TailNotificationsCollection, // Use polling instead of change streams

//        SlidingInvisibilityTimeout = TimeSpan.FromMinutes(1) // Default is 5 minutes (300 seconds)

//    };

//    // MongoDB connection string and database name
//    // Use the MongoDB storage with Hangfire
//    string mongoDatabaseName = "hangfire_db";
//    //string mongoConnectionString = "mongodb://localhost:27017,localhost:27018/?replicaSet=rs0"; //when using replicas
//    string mongoConnectionString = "mongodb://localhost:27017"; //no replicase used
//    GlobalConfiguration.Configuration.UseMongoStorage(mongoConnectionString, mongoDatabaseName, mongoStorageOptions);

//    GlobalConfiguration.Configuration.UseMongoStorage(mongoConnectionString, mongoDatabaseName, mongoStorageOptions);

//    builder.Services.AddHangfire(config => config.UseMongoStorage(mongoConnectionString, mongoDatabaseName));

//    builder.Services.AddHangfireServer();

//    var app = builder.Build();

//    // Enable Hangfire Dashboard
//    app.UseHangfireDashboard();

//    Startup.AssemblyInit(null);
//    Startup.languageSvc.InitAsync();
//    Startup.aiProviderSvc.InitAsync().Wait();
//    List<LanguageDm>? list = await Startup.languageSvc.GetAllAsync();
//    if (list != null)
//    {
//        foreach (LanguageDm languageDm in list)
//        {
//            languageIds.Add(languageDm.DisplayName, languageDm.EnglishName!);
//        }
//    }


//    if (!Directory.Exists(outputFilesDirectory))
//    {
//        Directory.CreateDirectory(outputFilesDirectory);
//    }

//    var localTimeZone = TimeZoneInfo.Local; 


//    app.MapPost("/processmedia", async ([FromBody] JobRequest rule) =>
//    {
//        string logMessage = $"Processing media rule:\n" +
//                            $"- Name: {rule.Name}\n" +
//                            $"- IsRecurring: {rule.IsRecurring}\n" +
//                            $"- Channels: {string.Join(", ", rule.Channels)}\n" +
//                            $"- StartTime: {rule.StartTime}\n" +
//                            $"- EndTime: {rule.EndTime}\n" +
//                            $"- Keywords: {string.Join(", ", rule.Keywords ?? new List<string>())}";

//        Console.WriteLine(logMessage);

//        if(rule.IsRecurring)
//        {
//            RecurringJobOptions recurringJobOptions = new() { TimeZone = localTimeZone };
//            RecurringJob.AddOrUpdate(rule.Name!, () => ExecuteRuleAsyncFullParallelism(rule), rule.CronExpression, recurringJobOptions);
//        }
//        else
//        {
//            BackgroundJob.Enqueue(() => ExecuteRuleAsyncFullParallelism(rule));
//        }

//        return Results.Ok(new { Message = "Added Media Processing Rule Successfully", Rule = rule });
//    });

//    app.Run();
//}
//private static async Task SerializeInsightResultToFileAsync(InsightResult insightResult, string filePath)
//{
//    var options = new JsonSerializerOptions
//    {
//        WriteIndented = true, // This makes the JSON more readable with indentation
//        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull // Ignore null properties
//    };

//    // Serialize the object to a JSON string
//    string jsonString = JsonSerializer.Serialize(insightResult, options);

//    // Write the JSON string to a file
//    await File.WriteAllTextAsync(filePath, jsonString);
//}

//public static async Task MonitorAndProcessNewFiles2(JobRequest jobRequest)
//{
//    Stopwatch stopwatch = Stopwatch.StartNew();

//    Console.WriteLine($"Monitoring for new files for Rule ID: {jobRequest.Id}");

//    // Assuming you have a mechanism for detecting new files
//    var fileWatcher = new FileSystemWatcher
//    {
//        Path = @"your_monitoring_directory_path", // Define the directory to monitor
//        Filter = "*.mp4", // Monitor only .mp4 files
//        NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite
//    };

//    fileWatcher.Created += async (sender, e) =>
//    {
//        Console.WriteLine("New file detected: " + e.FullPath);

//        // Loop through each channel
//        await Task.WhenAll(jobRequest.Channels.Select(async channel =>
//        {
//            if (e.FullPath.Contains(channel)) // If the new file belongs to the current channel
//            {
//                Console.WriteLine("Processing file for Channel: " + channel);

//                var file = e.FullPath;

//                // Process the file according to JobTypes
//                if (jobRequest.JobTypes.Contains(JobType.FaceDetection))
//                {
//                    await RunFaceDetection();
//                }

//                if (jobRequest.JobTypes.Contains(JobType.LogoDetection))
//                {
//                    await RunLogoDetection();
//                }

//                if (jobRequest.JobTypes.Contains(JobType.CreateClosedCaptions) ||
//                   jobRequest.JobTypes.Contains(JobType.KeywordsDetection) ||
//                   jobRequest.JobTypes.Contains(JobType.Translation) ||
//                   jobRequest.JobTypes.Contains(JobType.VerifyAudioLanguage)
//                )
//                {
//                    var mp3File = file.Replace(".mp4", ".mp3");

//                    var sttJsonFile = file.Replace(".mp4", ".json");

//                    if (!File.Exists(mp3File))
//                    {
//                        // Convert .mp4 to .mp3
//                        mp3File = await ExtractAudioAsync(file);

//                        if (!IsExtractionSucceeded(mp3File)) // it fails if result .mp3 is less than 5MB
//                            return;
//                    }

//                    if (!File.Exists(sttJsonFile)) // Transcribe only if the json file doesn't exist
//                    {
//                        InsightResult insightResult = await TranscribeFileAsync(mp3File);

//                        await SaveTranscriptionResultAsync(insightResult, sttJsonFile);

//                        if (jobRequest.JobTypes.Contains(JobType.KeywordsDetection))
//                        {
//                            await RunKeywordsDetection(jobRequest, insightResult, channel, sttJsonFile);
//                        }

//                        if (jobRequest.JobTypes.Contains(JobType.Translation))
//                        {
//                            await RunTranslation(jobRequest, channel, insightResult);
//                        }
//                    }
//                }
//            }
//        }));
//    };

//    fileWatcher.EnableRaisingEvents = true;

//    // Optionally, you may want to control how long this watcher should run
//    Console.WriteLine($"File monitoring started for Rule ID: {jobRequest.Id}. Waiting for new files...");

//    // The method could keep running indefinitely, or you can specify a cancellation condition
//    await Task.Delay(TimeSpan.FromHours(1)); // Example: Stop monitoring after 1 hour

//    stopwatch.Stop();

//    var elapsed = stopwatch.ElapsedMilliseconds;
//    var elapsedMinutes = stopwatch.ElapsedMilliseconds / 60000.0;

//    Console.WriteLine($"Finished monitoring Rule ID: {jobRequest.Id}, Elapsed Time: {elapsedMinutes} minutes");
//}
