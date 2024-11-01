using IntelligenceServiceTest;
using System.Text.Json;
using System.Text.Json.Serialization;
using Hangfire;
using Hangfire.Mongo;
using Hangfire.Mongo.Migration.Strategies;
using Hangfire.SqlServer;
using HangfireTest.Models;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using ActIntelligenceService.Domain.Models;
using ActIntelligenceService.Providers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Text;
using HangfireTest.Utility;
using System.Globalization;
using System.Collections.Concurrent;
using NLog;
using SharpCompress.Common;
using ActInfra.TestMocks;
using ActIntelligenceService.Domain.Services;
using HangfireTest.Services;
using HangfireTest.Repositories;
using MongoDB.Driver;
using ActIntelligenceService.Connectors;

public class Program
{
    // Directory to store files
    //private static string inputFilesDirectory = @"C:\Development\HangfireTest\Media\Record";    
    //private static List<FileSystemWatcher> watchers = new List<FileSystemWatcher>();
    //private static readonly Dictionary<string, string> languageIds = new();
    //private static HashSet<string> processedFiles = new HashSet<string>();
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    public static async Task Main(string[] args)
    {
        try
        {
            var app = CreateApplicationWithHangfire(args);

            var logger = LogManager.GetCurrentClassLogger();

            logger.Info("Application starting...");

            //AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
            
            //Console.CancelKeyPress += OnCancelKeyPress;

            app.Run();
        }
        catch(Exception ex)
        {
            Logger.Debug($"[Main] Error occurred: {ex.Message}");
        }
        finally
        {
            LogManager.Shutdown(); // Ensure to flush and stop internal timers/threads before application-exit
        }
    }
    //private static void OnCancelKeyPress(object sender, ConsoleCancelEventArgs e)
    //{
    //    Logger.Info("Application is closing due to Ctrl+C or other signal.");
    //    DisposeResources();
    //}
    //private static void OnProcessExit(object sender, EventArgs e)
    //{
    //    Logger.Info("Application is shutting down.");
    //    DisposeResources();
    //}
    //private static void DisposeResources()
    //{
    //    // Dispose the FileSystemWatcher and ensure ffmpeg processes are closed
    //    StopWatching();
    //}
    //public static void StopWatching()
    //{
    //    foreach (var watcher in watchers)
    //    {
    //        watcher.EnableRaisingEvents = false;
    //        watcher.Dispose();
    //        Logger.Info($"Stopped and disposed watcher for path: {watcher.Path}");
    //    }
    //}
    private static WebApplication CreateApplicationWithHangfire(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        /*
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

        builder.Services.AddHangfire(config => config.UseMongoStorage(mongoConnectionString, mongoDatabaseName));

        builder.Services.AddHangfireServer();
        */


        // Configure SQL Server options
        string sqlServerConnectionString = @"Server=DESKTOP-2JQIL5E\SQLEXPRESS;Database=HangfireDB;Integrated Security=True;Encrypt=False;";
        //string sqlServerConnectionString = @"Server=DESKTOP-2JQIL5E\SQLEXPRESS;Database=HangfireDB;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;";

        builder.Services.AddHangfire(config =>
            config.UseSqlServerStorage(sqlServerConnectionString, new SqlServerStorageOptions
            {
                CommandBatchMaxTimeout = TimeSpan.FromMinutes(5),
                SlidingInvisibilityTimeout = TimeSpan.FromMinutes(1),
                QueuePollInterval = TimeSpan.Zero, // Default is 15 seconds
                UseRecommendedIsolationLevel = true,
                DisableGlobalLocks = true // Recommended for SQL Server 2005+
            }));

        builder.Services.AddHangfireServer();


        builder.Services.AddSingleton<IXXXOperationsService, XXXOperationsService>();

        builder.Services.AddSingleton<IXXXHangfireJobSchedulerService, XXXHangfireJobSchedulerService>();

        builder.Services.AddSingleton<ICustomJobSchedulerService, CustomJobSchedulerService>();

        builder.Services.AddSingleton<IXXXJobRepository, XXXJobRepository>();

        builder.Services.AddSingleton<IXXXJobService, XXXJobService>();

        //builder.Services.AddHttpClient<IAccountManagerConnector, AccountManagerConnector>();
        //builder.Services.AddSingleton<IAccountManagerConnector, AccountManagerConnector>();
        
        builder.Services.AddSingleton<EmailService>();

        builder.Services.AddControllers();

        //builder.Services.AddSwaggerGen();

        // Register MongoClient for Dependency Injection
        builder.Services.AddSingleton<IMongoClient>(sp =>
        {
            return new MongoClient("mongodb://localhost:27017"); 
        });

        // Register your XXXJobRepository
        builder.Services.AddSingleton<IXXXJobRepository, XXXJobRepository>();

        var app = builder.Build();

        app.UseHangfireDashboard();

        app.UseHttpsRedirection();

        //app.UseSwagger();
        //app.UseSwaggerUI();

        app.MapControllers();

        return app;
    }

}
/*
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
private static void ProcessJob(JobRequest jobRequest)
{
    var invocationType = GetInvocationType(jobRequest);

    EnqueueJob(jobRequest, invocationType);
}
private static InvocationType GetInvocationType(JobRequest jobRequest)
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
private static void EnqueueJob(JobRequest jobRequest, InvocationType invocationType)
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
public static async Task ProcessExistingFiles(JobRequest jobRequest)
{
    Stopwatch stopwatch = Stopwatch.StartNew();

    Logger.Debug($"Executing Rule ID: {jobRequest.Id}");

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
                await RunFaceDetection();
            }

            if (jobRequest.Operations.Contains(Operation.DetectLogo))
            {
                await RunLogoDetection();
            }

            if (jobRequest.Operations.Contains(Operation.CreateClosedCaptions) ||
               jobRequest.Operations.Contains(Operation.DetectKeywords) ||
               jobRequest.Operations.Contains(Operation.Translation) ||
               jobRequest.Operations.Contains(Operation.VerifyAudioLanguage)
            )
            {
                var mp3File = file.Replace(".mp4", ".mp3");

                var sttJsonFile = file.Replace(".mp4", ".json");

                if (!File.Exists(mp3File))
                {
                    mp3File = await ExtractAudioAsync(file);// Convert .mp4 to .mp3

                    if (!IsExtractionSucceeded(mp3File)) // it fails if result .mp3 is less than 5MB
                        return;
                }

                if (!File.Exists(sttJsonFile)) // Transcribe only if the json file doesn't exist
                {
                    InsightResult insightResult = await TranscribeFileAsync(mp3File, sttJsonFile);

                    if (jobRequest.Operations.Contains(Operation.DetectKeywords))
                    {
                        await RunKeywordsDetection(jobRequest, insightResult, channel, sttJsonFile);
                    }

                    if (jobRequest.Operations.Contains(Operation.Translation))
                    {
                        await RunTranslations(jobRequest, channel, insightResult, sttJsonFile);
                    }
                }
            }
        }));

    }));

    stopwatch.Stop();

    var elapsed = stopwatch.ElapsedMilliseconds;

    var elapsedMinutes = stopwatch.ElapsedMilliseconds / 60000.0;

    Logger.Debug($"Finished Executing Rule ID: {jobRequest.Id}, Elapsed Time: {elapsedMinutes} minutes");
}
public static Task MonitorAndProcessNewFiles(JobRequest jobRequest)
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
private static async Task MixJobProcessing(JobRequest jobRequest)
{
    await ProcessExistingFiles(jobRequest);

    await MonitorAndProcessNewFiles(jobRequest);//, CancellationToken.None);
}
private static async Task OnNewFileCreated(string filePath, JobRequest jobRequest, string channel)
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
private static async Task WaitForFileReady(string filePath)
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
private static bool FileIsReady(string filePath) 
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
private static async Task ProcessFileAsync(string mp4FilePath, JobRequest jobRequest, string channel)
{
    try
    {
        var stopwatch = Stopwatch.StartNew();

        var mp3File = await ExtractAudioAsync(mp4FilePath);

        Logger.Debug($"Audio extraction took: {stopwatch.Elapsed.TotalSeconds} seconds");

        var sttFile = mp3File.Replace(".mp3", ".json");

        InsightResult insightResult = await TranscribeFileAsync(mp3File, sttFile);

        Logger.Debug($"Transcription took: {stopwatch.Elapsed.TotalSeconds} seconds");

        //parallelized invocation of RunKeywordsDetection and an RunTranslation
        if (jobRequest.Operations.Contains(Operation.DetectKeywords) && jobRequest.Operations.Contains(Operation.Translation))
        {
            await Task.WhenAll(
                RunKeywordsDetection(jobRequest, insightResult, channel, sttFile),
                RunTranslations(jobRequest, channel, insightResult, sttFile)
            );
        }
        else if (jobRequest.Operations.Contains(Operation.DetectKeywords))
        {
            await RunKeywordsDetection(jobRequest, insightResult, channel, sttFile);
        }
        else if (jobRequest.Operations.Contains(Operation.Translation))
        {
            await RunTranslations(jobRequest, channel, insightResult, sttFile);
        }

        stopwatch.Stop();
        Logger.Debug($"Total processing time for {mp4FilePath}: {stopwatch.Elapsed.TotalSeconds} seconds");
    }
    catch  (Exception ex) 
    {
        Logger.Debug($"[ProcessFileAsync] Error processing {mp4FilePath}: {ex.Message}");
        Logger.Debug($"[ProcessFileAsync] Stack Trace: {ex.StackTrace}");
    }
}
private static async Task<List<KeywordMatch>> FindKeywordsAsync(InsightResult insightResult, List<string> keywords, string channelName, string fileName)
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
private static async Task SaveKeywordMatchesToFileAsync(string filePath, List<KeywordMatch> keywordMatches)
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
    var startDateString = DateTime.ParseExact(jobRequest.BroadcastStartTime, "yyyy_MM_dd_HH_mm_ss", null).ToString("yyyy_MM_dd");
    var channelWithDateFolder = Path.Combine(channelFolder, startDateString);

    var allFiles = Directory.GetFiles(channelWithDateFolder, "*.mp4");
    //foreach ( var file in allFiles)
    //{
    //    var res = FileMatchesTimeRange(file, rule.BraodcastStartTime, rule.BroadcastEndTime);
    //}
    return allFiles.Where(file => FileMatchesTimeRange(file, jobRequest.BroadcastStartTime, jobRequest.BroadcastEndTime));
}
private static bool IsExtractionSucceeded(string mp3File)
{
    long filesizebytes = new FileInfo(mp3File).Length;
    if (filesizebytes < 5000000) // Less than 5MB
    {
        Logger.Debug($"Conversion failed for file {mp3File}. Skipping transcription.");
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
    var audioFilePath = videoFilePath.Replace(".mp4", ".mp3");

    Logger.Debug($"[ExtractAudioAsync] Starting FFmpeg conversion for: {videoFilePath} at {DateTime.Now}");

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
        Logger.Debug($"[ExtractAudioAsync] FFmpeg failed for: {audioFilePath}");
        throw new InvalidOperationException($"FFmpeg failed with exit code {process.ExitCode}");
    }
    else
    {
        Logger.Debug($"[ExtractAudioAsync] FFmpeg conversion succeeded for: {audioFilePath}");
        Logger.Debug($"[ExtractAudioAsync] File size of {audioFilePath}: {new FileInfo(audioFilePath).Length} bytes");
    }

    // Return the path of the extracted audio file
    return audioFilePath;
}
private static async Task<InsightResult> TranscribeFileAsync(string audioFilePath, string sttFile)
{
    Logger.Debug($"[TranscribeFileAsync] Starting transcription for: {audioFilePath} at {DateTime.Now}");

    var insightResult = await GetAudioBasedCaptionsTest(SystemInsightTypes.Transcription, ProviderType.OpenAI, audioFilePath);

    await SaveInsightToFileAsync(insightResult!, sttFile);

    Logger.Debug($"[TranscribeFileAsync] Transcription saved to: {sttFile}");

    return insightResult!;
}
/*
private static async Task RunTranslation(JobRequest jobRequest, string channel, InsightResult sttInsightResult, string sttJsonFile)
{
    if(jobRequest.TranslationLanguages == null || jobRequest.ExpectedAudioLanguage == null)
    {
        Logger.Debug("Request is missing TranslationLanguages or ExpectedAudioLanguage");
        throw new Exception($"Translation Request is missing translation languages.");
    }

    foreach (var trLanguage in jobRequest.TranslationLanguages!)
    {
        Logger.Debug($"[RunTranslation] Translating to {trLanguage} for {sttJsonFile} at {DateTime.Now}");

        var trRequest = GetInsightRequest(SystemInsightTypes.Translation, jobRequest.ExpectedAudioLanguage, trLanguage);

        var trInsightInputData = new InsightInputData
        {
            SourceInsightInput = sttInsightResult,
        };

        trInsightInputData.SourceInsightInput.SourceInsightType = SystemInsightTypes.Transcription;

        var azureTrProvider = GetProvider(ProviderType.Azure, trInsightInputData, trRequest);

        var trInsightResult = await azureTrProvider!.ProcessAsync(trInsightInputData, trRequest);

        await SaveInsightToFileAsync(trInsightResult![0], sttJsonFile.Replace(".json",$"_{trLanguage}.json"));

        Logger.Debug($"[RunTranslation] Translation saved: {sttJsonFile.Replace(".json", $"_{trLanguage}.json")}");
    }

    await Task.CompletedTask;
}

private static async Task RunTranslations(JobRequest jobRequest, string channel, InsightResult sttInsightResult, string sttJsonFile)
{
    if (jobRequest.TranslationLanguages == null || jobRequest.ExpectedAudioLanguage == null)
    {
        Logger.Debug("Request is missing TranslationLanguages or ExpectedAudioLanguage");
        throw new Exception("Translation Request is missing translation languages.");
    }

    var translationTasks = jobRequest.TranslationLanguages.Select(async trLanguage =>
    {
        Logger.Debug($"[RunTranslation] Translating to {trLanguage} for {sttJsonFile} at {DateTime.Now}");

        var trRequest = GetInsightRequest(SystemInsightTypes.Translation, jobRequest.ExpectedAudioLanguage, trLanguage);
        var trInsightInputData = new InsightInputData { SourceInsightInput = sttInsightResult };

        var azureTrProvider = GetProvider(ProviderType.Azure, trInsightInputData, trRequest);
        var trInsightResult = await azureTrProvider!.ProcessAsync(trInsightInputData, trRequest);

        await SaveInsightToFileAsync(trInsightResult![0], sttJsonFile.Replace(".json", $"_{trLanguage}.json"));

        Logger.Debug($"[RunTranslation] Translation saved: {sttJsonFile.Replace(".json", $"_{trLanguage}.json")}");
    });

    await Task.WhenAll(translationTasks);
}
private static async Task SaveInsightToFileAsync(InsightResult insightResult, string filePath)
{
    // Trim leading and trailing whitespaces in the Text property
    foreach (var item in insightResult.TimeCodedContent!)
    {
        item.Text = item.Text.Trim();
    }

    var options = new JsonSerializerOptions
    {
        WriteIndented = true, // Makes the JSON more readable with indentation
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull, // Ignore null properties
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping // Allow direct Unicode characters
    };

    string jsonString = JsonSerializer.Serialize(insightResult, options);

    await File.WriteAllTextAsync(filePath, jsonString);

    Logger.Debug($"[SaveInsightToFileAsync] File saved: {filePath}, size: {new FileInfo(filePath).Length} bytes");

    await SaveClosedCaption();
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
public static async Task PollDirectoryForNewFiles(JobRequest jobRequest, TimeSpan pollingInterval)
{
    while (true)
    {
        foreach (var channel in jobRequest.Channels)
        {
            string channelPath = Path.Combine(inputFilesDirectory, channel);
            var newFiles = Directory.GetFiles(channelPath, "*.mp4");

            foreach (var filePath in newFiles)
            {
                // If the file hasn't been processed yet, process it
                if (!processedFiles.Contains(filePath))
                {
                    await OnNewFileCreated(filePath, jobRequest, channel);
                    processedFiles.Add(filePath);
                }
            }
        }

        await Task.Delay(pollingInterval); // Wait before polling again
    }
}

*/

/*
If multiple files are being detected frequently (e.g., every 20 seconds), you might consider using a queue system.This decouples the file detection from the processing and allows you to control how many files are processed at a time.

Example using a queue and task executors:
private static ConcurrentQueue<string> fileQueue = new ConcurrentQueue<string>();
private static void EnqueueNewFile(string filePath)
{
    fileQueue.Enqueue(filePath);
}
public static async Task ProcessQueueAsync(JobRequest jobRequest)
{
    while (true)
    {
        if (fileQueue.TryDequeue(out var filePath))
        {
            await ProcessFileAsync(filePath, jobRequest, "channel");
        }

        await Task.Delay(100); // Prevent tight looping
    }
}
You would then call EnqueueNewFile in the OnNewFileCreated method and let ProcessQueueAsync run as a background task.
*/

//private static void PrintJobRequest(JobRequest jobRequest)
//{
//    string logMessage = $"Processing media rule:\n" +
//                        $"- Name: {jobRequest.Name}\n" +
//                        $"- IsRealTime: {jobRequest.IsRealTime}\n" +
//                        $"- IsRecurring: {jobRequest.IsRecurring}\n" +
//                        $"- ExecutionTime: {jobRequest.ExecutionTime}\n" +
//                        $"- CronExpression: {jobRequest.CronExpression}\n" +
//                        $"- Channels: {string.Join(", ", jobRequest.Channels)}\n" +
//                        $"- BraodcastStartTime: {jobRequest.BroadcastStartTime}\n" +
//                        $"- BroadcastEndTime: {jobRequest.BroadcastEndTime}\n" +
//                        $"- Keywords: {string.Join(", ", jobRequest.Keywords ?? new List<string>())}\n" +
//                        $"- Operations: {string.Join(", ", jobRequest.Operations ?? new List<string>())}\n" +
//                        $"- ExpectedAudioLanguage: {jobRequest.ExpectedAudioLanguage}\n" +
//                        $"- TranslationLanguages: {string.Join(", ", jobRequest.TranslationLanguages ?? new List<string>())}";

//    Logger.Debug(logMessage);
//}
