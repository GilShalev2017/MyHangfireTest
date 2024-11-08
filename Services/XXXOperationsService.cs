using ActIntelligenceService.Domain.Models;
using ActIntelligenceService.Providers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NLog;
using System.Diagnostics;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.Json;
using HangfireTest.Models;
using ActIntelligenceService.Domain.Models.InsightProviders;
using IntelligenceServiceTest;
using ActInfra.Models;
using System.Globalization;
using System.IO;
using ActCommon;
using ActIntelligenceService.Connectors;
using ActIntelligenceService.Domain.Models.AIClip;
using Microsoft.AspNetCore.Hosting.Server;
using ActInfra;
using Microsoft.AspNetCore.Mvc;
using HangfireTest.Repositories;
using Hangfire;
using Microsoft.Extensions.FileSystemGlobbing;

namespace HangfireTest.Services
{
    public interface IXXXOperationsService
    {
        Task ExecuteJobAsync(JobRequestEntity job);
        InvocationType GetInvocationType(JobRequestEntity jobRequest);
        Task ProcessExistingFiles(JobRequestEntity job);
        Task MonitorAndProcessNewFiles(JobRequestEntity jobRequest);
        Task MixJobProcessing(JobRequestEntity jobRequest);
        Task<string> ExtractAudioAsync(string videoFilePath);
        bool IsExtractionSucceeded(string mp3File);
        Task /*<FaceDetectionResult>*/ DetectFacesAsync(string filePath);
        Task /*<LogoDetectionResult>*/ DetectLogosAsync(string filePath);
        Task<InsightResult> TranscribeFileAsync(string audioFilePath, string sttFile);
        Task TranslateTranscriptionAsync(JobRequestEntity jobRequest, string channel, InsightResult sttInsightResult, string sttJsonFile);
        Task DetectKeywordsAsync(JobRequestEntity jobRequest, InsightResult insightResult, string channel, string sttJsonFile);
        Task SaveTranscriptionAsClosedCaptionsAsync(InsightResult sttInsightResult, string audioFilePath, string outputFolderPath, string channel);
        //Task<LanguageDetectionResult> DetectAudioLanguageAsync(string filePath);
        //Task<UnexpectedLanguageResult> DetectUnexpectedLanguageAsync(string filePath, string expectedLanguage);
    }

    public class XXXOperationsService : IXXXOperationsService
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger(); // NLog logger
        private static readonly Dictionary<string, string> languageIds = new();
        private static string inputFilesDirectory = @"C:\Development\HangfireTest\Media\Record";
        private readonly IXXXJobRepository _jobsRepository;
        //private static List<FileSystemWatcher> watchers = new List<FileSystemWatcher>();
        //private readonly IAccountManagerConnector _accountManagerConnector;
        //private static HttpClient _httpClient = new HttpClient();
        //private readonly EmailService _emailService;

        public XXXOperationsService(IXXXJobRepository jobsRepository)//EmailService emailService)//IAccountManagerConnector accountManagerConnector)
        {
            InitProvidersEnvironment();
            _jobsRepository = jobsRepository;
            //_emailService = emailService;
            //_accountManagerConnector = new AccountManagerConnector(null, new HttpClient());
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
                    if (!languageIds.ContainsKey(languageDm.DisplayName))
                    {
                        languageIds.Add(languageDm.DisplayName, languageDm.EnglishName!);
                    }
                }
            }
        }

        #region IXXXOperationsService
        public async Task ExecuteJobAsync(JobRequestEntity job)
        {
            var invocationType = GetInvocationType(job);

            if (invocationType == InvocationType.Batch)
            {
                await ProcessExistingFiles(job);
            }
            else if (invocationType == InvocationType.RealTime)
            {
                await MonitorAndProcessNewFiles(job);
            }
            else if(invocationType == InvocationType.Both)
            {
                await MixJobProcessing(job);
            }
        }
        public InvocationType GetInvocationType(JobRequestEntity jobRequest)
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
                //if (startTime < currentTime && endTime >= currentTime)
                //{
                //    // Part of the time range is in the past and part is in the future
                //    invocationType = InvocationType.Both;  // Process past & future
                //}
                //else if (startTime > currentTime)
                //{
                    // Entire time range is in the future, use RTProcessing
                    invocationType = InvocationType.RealTime;
                //}
            }
            else
            {
                // If the request isn't real-time and the time range is in the future, use NotRTPRocessing
                invocationType = InvocationType.Batch;
            }

            return invocationType;
        }
        public async Task ProcessExistingFiles(JobRequestEntity job)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();

            Logger.Debug($"Executing JOB ID: {job.Id}");

            // Loop through each channel
            await Task.WhenAll(job.Channels.Select(async channel =>
            {
                Logger.Debug("Transcribing Channel " + channel);

                var filesInRange = GetFilesForTimeRange(job, channel);

                // Loop through each file in range
                await Task.WhenAll(filesInRange.Select(async file =>
                {
                    if (job.Operations.Contains(Operation.DetectFaces))
                    {
                        await DetectFacesAsync(file);
                    }

                    if (job.Operations.Contains(Operation.DetectLogo))
                    {
                        await DetectLogosAsync(file);
                    }

                    if (job.Operations.Contains(Operation.CreateClosedCaptions) ||
                       job.Operations.Contains(Operation.DetectKeywords) ||
                       job.Operations.Contains(Operation.TranslateTranscription) ||
                       job.Operations.Contains(Operation.VerifyAudioLanguage)
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

                            if (job.Operations.Contains(Operation.DetectKeywords))
                            {
                                await DetectKeywordsAsync(job, insightResult, channel, sttJsonFile);
                            }

                            if (job.Operations.Contains(Operation.TranslateTranscription))
                            {
                                await TranslateTranscriptionAsync(job, channel, insightResult, sttJsonFile);
                            }

                            if (job.Operations.Contains(Operation.CreateClosedCaptions))
                            {
                                var outputFolderPath = Path.Combine(new FileInfo(sttJsonFile).DirectoryName!, "closed_captions"); //TODO should be an inner folder with the LngID name
                                if (!Directory.Exists(outputFolderPath))
                                {
                                    Directory.CreateDirectory(outputFolderPath);
                                }
                                await SaveTranscriptionAsClosedCaptionsAsync(insightResult, mp3File, outputFolderPath!, channel);
                            }
                        }
                    }
                }));

            }));

            stopwatch.Stop();

            var elapsed = stopwatch.ElapsedMilliseconds;

            var elapsedMinutes = stopwatch.ElapsedMilliseconds / 60000.0;

            Logger.Debug($"Finished Executing JOB ID: {job.Id}, Elapsed Time: {elapsedMinutes} minutes");
        }
        private IEnumerable<string> GetFilesForTimeRange(JobRequestEntity job, string channel)
        {
            var channelFolder = Path.Combine(inputFilesDirectory, channel);
            var startDateString = DateTime.ParseExact(job.BroadcastStartTime, "yyyy_MM_dd_HH_mm_ss", null).ToString("yyyy_MM_dd");
            var channelWithDateFolder = Path.Combine(channelFolder, startDateString);

            var allFiles = Directory.GetFiles(channelWithDateFolder, "*.mp4");
            //foreach ( var file in allFiles)
            //{
            //    var res = FileMatchesTimeRange(file, rule.BraodcastStartTime, rule.BroadcastEndTime);
            //}
            return allFiles.Where(file => FileMatchesTimeRange(file, job.BroadcastStartTime, job.BroadcastEndTime));
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
        public async Task MonitorAndProcessNewFiles(JobRequestEntity jobRequest)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            Logger.Debug($"Starting real-time processing for Rule ID: {jobRequest.Id}");

            // Convert the endTime from string to DateTime and add 6 minutes
            //DateTime endTime = DateTime.ParseExact(jobRequest.BroadcastEndTime, "yyyy_MM_dd_HH_mm_ss", CultureInfo.InvariantCulture).AddMinutes(6);
            DateTime endTime = DateTime.ParseExact(jobRequest.BroadcastEndTime, "yyyy_MM_dd_HH_mm_ss", CultureInfo.InvariantCulture).AddMinutes(1);

            List<FileSystemWatcher> watchers = new List<FileSystemWatcher>();

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

            while (DateTime.Now < endTime)
            {
                await Task.Delay(60000); // Check every minute
            }
            foreach (var watcher in watchers)
            {
                watcher.EnableRaisingEvents = false; // Disable events
                watcher.Dispose(); // Dispose of the watcher
            }
            stopwatch.Stop();
            var elapsedMinutes = stopwatch.ElapsedMilliseconds / 60000.0;
            Logger.Debug($"Finished real-time processing for Rule ID: {jobRequest.Id}, Elapsed Time: {elapsedMinutes} minutes");

        }
        public async Task MixJobProcessing(JobRequestEntity jobRequest)
        {
            await ProcessExistingFiles(jobRequest);

            await MonitorAndProcessNewFiles(jobRequest);//, CancellationToken.None);
        }
        private async Task OnNewFileCreated(string filePath, JobRequestEntity jobRequest, string channel)
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
                Logger.Error($"[OnNewFileCreated] Error processing file {filePath}: {ex.Message}");
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
        private async Task ProcessFileAsync(string mp4FilePath, JobRequestEntity jobRequest, string channel)
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
                if (jobRequest.Operations.Contains(Operation.DetectKeywords) &&
                    jobRequest.Operations.Contains(Operation.TranslateTranscription) &&
                    jobRequest.Operations.Contains(Operation.CreateClosedCaptions))
                {
                    var outputFolderPath = Path.Combine(new FileInfo(sttFile).DirectoryName!, "closed_captions"); //TODO should be an inner folder with the LngID name
                    if (!Directory.Exists(outputFolderPath))
                    {
                        Directory.CreateDirectory(outputFolderPath);
                    }

                    await Task.WhenAll(
                        DetectKeywordsAsync(jobRequest, insightResult, channel, sttFile),
                        TranslateTranscriptionAsync(jobRequest, channel, insightResult, sttFile),
                        SaveTranscriptionAsClosedCaptionsAsync(insightResult, mp3File, outputFolderPath!, channel)
                    );
                }
                else if (jobRequest.Operations.Contains(Operation.DetectKeywords))
                {
                    await DetectKeywordsAsync(jobRequest, insightResult, channel, sttFile);
                }
                else if (jobRequest.Operations.Contains(Operation.TranslateTranscription))
                {
                    await TranslateTranscriptionAsync(jobRequest, channel, insightResult, sttFile);
                }
                else if (jobRequest.Operations.Contains(Operation.CreateClosedCaptions))
                {
                    var outputFolderPath = Path.Combine(new FileInfo(sttFile).DirectoryName!, "closed_captions"); //TODO should be an inner folder with the LngID name
                    if (!Directory.Exists(outputFolderPath))
                    {
                        Directory.CreateDirectory(outputFolderPath);
                    }
                    await SaveTranscriptionAsClosedCaptionsAsync(insightResult, mp3File, outputFolderPath!, channel);
                }

                stopwatch.Stop();
                Logger.Debug($"Total processing time for {mp4FilePath}: {stopwatch.Elapsed.TotalSeconds} seconds");
            }
            catch (Exception ex)
            {
                Logger.Error($"[ProcessFileAsync] Error processing {mp4FilePath}: {ex.Message}");
                Logger.Error($"[ProcessFileAsync] Stack Trace: {ex.StackTrace}");
            }
        }

        public async Task<string> ExtractAudioAsync(string videoFilePath)
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
        public bool IsExtractionSucceeded(string mp3File)
        {
            long filesizebytes = new FileInfo(mp3File).Length;
            if (filesizebytes < 5000000) // Less than 5MB
            {
                Logger.Debug($"Conversion failed for file {mp3File}. Skipping transcription.");
                return false;  // Exit this task and move to the next file
            }
            return true;
        }
        public async Task DetectFacesAsync(string filePath)
        {
            await Task.CompletedTask;
        }
        public Task DetectLogosAsync(string filePath)
        {
            throw new NotImplementedException();
        }
        public async Task<InsightResult> TranscribeFileAsync(string audioFilePath, string sttFile)
        {
            Logger.Debug($"[TranscribeFileAsync] Starting transcription for: {audioFilePath} at {DateTime.Now}");

            //OpenAI Transcriber
            var insightResult = await GetAudioBasedCaptionsTest(SystemInsightTypes.Transcription, ProviderType.OpenAI, audioFilePath);
          
            //Whisper Transcriber
            //var insightResult = await GetAudioBasedCaptionsTest(SystemInsightTypes.Transcription, ProviderType.Whisper, audioFilePath);

            await SaveInsightToFileAsync(insightResult!, sttFile);

            Logger.Debug($"[TranscribeFileAsync] Transcription saved to: {sttFile}");

            return insightResult!;
        }
        public async Task TranslateTranscriptionAsync(JobRequestEntity jobRequest, string channel, InsightResult sttInsightResult, string sttJsonFile)
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
        public async Task DetectKeywordsAsync(JobRequestEntity job, InsightResult insightResult, string channel, string sttJsonFile)
        {
            if (job.Keywords != null && job.Keywords.Count > 0)
            {
                var keywordMatches = await FindKeywordsAsync(insightResult, job.Keywords, channel, sttJsonFile);

                if (keywordMatches.Count > 0)
                {
                    string keywordFile = sttJsonFile.Replace(".json", "_keywords.json");
                    await SaveKeywordMatchesAsync(channel, job,keywordFile, keywordMatches);
                }
                //foreach (var keywordMatch in keywordMatches)
                //{
                //    //SendKeywordNotification(keywordMatch, channel);
                //    SendKeywordNotificationEmail(keywordMatch, channel,"gilshalev@actusdigital.com");
                //}
            }
        }
        private string ExtractTimestamp(string filePath)
        {
            // Get the file name from the full path
            string fileName = Path.GetFileNameWithoutExtension(filePath);

            // Split by underscores
            string[] parts = fileName.Split('_');

            // Join the parts that form the timestamp (index 1 to 6)
            string timestamp = string.Join("_", parts[1], parts[2], parts[3], parts[4], parts[5], parts[6]);

            return timestamp;
        }
        public async Task SaveTranscriptionAsClosedCaptionsAsync(InsightResult sttInsightResult, string audioFilePath, string outputFolderPath, string channel)
        {
            try
            {
                if(sttInsightResult.TimeCodedContent != null && sttInsightResult.TimeCodedContent.Count > 0)
                { 
                    string fileNameDate = Path.GetFileNameWithoutExtension(audioFilePath);
                    int firstSplitIndex = fileNameDate.IndexOf('_') + 1;
                    int lastSplitIndex = fileNameDate.LastIndexOf('_');
                    fileNameDate = fileNameDate.Substring(firstSplitIndex, lastSplitIndex - firstSplitIndex);
                    DateTime fileDate = DateTime.ParseExact(fileNameDate, "yyyy_MM_dd_HH_mm", CultureInfo.InvariantCulture);

                    int lastTimeSec = 0;
                    foreach (Transcript ts in sttInsightResult.TimeCodedContent)
                    {
                        DateTime ccStartTime = fileDate + TimeSpan.FromSeconds(lastTimeSec);
                        DateTime ccEndTime = fileDate + TimeSpan.FromSeconds(ts.EndInSeconds);
                        TimeSpan ccDuration = ccEndTime - ccStartTime;
                        lastTimeSec = ts.EndInSeconds;

                        int duration = (int)Math.Round(ccDuration.TotalSeconds);
                        DateTime sttFileDate = new DateTime(ccStartTime.Year, ccStartTime.Month, ccStartTime.Day, ccStartTime.Hour, ccStartTime.Minute, ccStartTime.Second);
                        string rightDigit = (duration / 10).ToString();
                        string leftDigit = (duration % 10).ToString();
                        fileNameDate = sttFileDate.ToString("yyyy_MM_dd_HH_mm_ss") + "_" + rightDigit + leftDigit;
                        string destFileParse = Path.Combine(outputFolderPath, fileNameDate + ".txt");
                        if (!File.Exists(destFileParse))
                        {
                            //CCHelper is defined in ActCommon
                            //await CCHelper.WriteTxtResultAsync(ts.Text, destFileParse); //writing the translated sentence in a txt file
                            await WriteTxtResultAsync(ts.Text, destFileParse);
                            
                            //Safe upload to Lucene <-IS THIS REALLY NEEDED?
                            /*
                            try
                            {
                                int channelID = GetChannelID(channel);
                                LuceneSchema.CCLuceneDoc ccdoc = new LuceneSchema.CCLuceneDoc()
                                {
                                    content = ts.Text,
                                    date = fileDate,
                                    lang = Path.GetFileName(outputFolderPath),
                                    path = destFileParse,
                                    //channel_id = fileInfo.channel.channelID,
                                    //physical_channel = fileInfo.channel.physicalName
                                    channel_id = channelID,
                                    physical_channel = channel
                                };
                                await LuceneHelper.InsertAsync(ccdoc);
                            }
                            catch
                            {

                            }*/
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"SaveTranscriptionAsClosedCaptionsAsync failed: {ex.Message} stack: {ex.StackTrace}");
                throw new Exception($"error: SaveTranscriptionAsClosedCaptionsAsync {ex.Message}");
            }
        }

        #endregion IXXXOperationsService
        //private async void SendKeywordNotificationEmail(KeywordMatch keywordMatch, string channel,string toEmail)
        //{
        //    string subject = $"Actus Keyword Detection: Keyword(s) detected: {keywordMatch.Keyword}";
        //    string body = $"<b>Channel Name</b>: {channel} <br/> between start time: {keywordMatch.StartInSeconds} and end time {keywordMatch.EndInSeconds} {Environment.NewLine}";
        //    await _emailService.SendAWSEmailAsync(toEmail, subject, body);
        //}
        //private async void SendKeywordNotification(KeywordMatch keywordMatch, string channel)
        //{
        //    string subject = $"Actus Keyword Detection: Keyword(s) detected: {keywordMatch.Keyword}";
        //    string body = $"<b>Channel Name</b>: {channel} <br/> between start time: {keywordMatch.StartInSeconds} and end time {keywordMatch.EndInSeconds} {Environment.NewLine}";
            
        //    GlobalNotificationSendDTO globalNotificationSendDTO = new();
        //    globalNotificationSendDTO.NotificationIds = new List<string>();
        //    globalNotificationSendDTO.NotificationIds.AddRange(["671e1b0cd24904b406041bbd"]);//NotificationIds

        //    globalNotificationSendDTO.NotificationParams = new Dictionary<string, string>()
        //    {
        //        {NotificationParamKeys.Subject.ToString() , subject },{NotificationParamKeys.Body.ToString() , body }
        //    };
        //    //string url = $"http://{_accountManagerHost}:{_accountManagerPort}/account/api/service/microservice";
        //    string url = $"http://localhost:8890/account/api/service/microservice";
        //    List<NotificationLogDm> rsp = await WebHelper.PostAsync<List<NotificationLogDm>>(url, globalNotificationSendDTO, null, _httpClient); //response sample: {"Id":"633409088c2e1d7e95e47c6f"}
            
        //    _emailService.SendEmail("gilshalev@actusdigital.com", subject, body);
        //}
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
        //private async Task SaveKeywordMatchesAsync(string channel, JobRequestEntity job, string filePath, List<KeywordMatch> keywordMatches)
        //{
        //    var options = new JsonSerializerOptions
        //    {
        //        WriteIndented = true
        //    };

        //    // Serialize the keyword matches list to JSON
        //    string jsonString = JsonSerializer.Serialize(keywordMatches, options);

        //    // Write the JSON string to a file
        //    await File.WriteAllTextAsync(filePath, jsonString);

        //   //Add channel 5 minutes results to the job operation results dictionary
        //   job.ChannelOperationResults.Add...

        //    await _jobsRepository.SaveOperationResult(job);
        //}
        private async Task SaveKeywordMatchesAsync(string channel, JobRequestEntity job, string filePath, List<KeywordMatch> keywordMatches)
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };

            // Serialize the keyword matches list to JSON
            string jsonString = JsonSerializer.Serialize(keywordMatches, options);

            // Write the JSON string to a file
            await File.WriteAllTextAsync(filePath, jsonString);

            // Define a timestamp for this segment (e.g., "00:00-05:00")
            string timestamp = ExtractTimestamp(filePath);

            // Initialize the structure if necessary
            if (!job.ChannelOperationResults.ContainsKey(channel))
            {
                job.ChannelOperationResults[channel] = new Dictionary<string, List<KeywordMatchSegment>>();
            }

            if (!job.ChannelOperationResults[channel].ContainsKey("DetectKeywords"))
            {
                job.ChannelOperationResults[channel]["DetectKeywords"] = new List<KeywordMatchSegment>();
            }

            // Create the segment data and add it to ChannelOperationResults
            var segmentData = new KeywordMatchSegment
            {
                Timestamp = timestamp,
                Results = keywordMatches
            };

            job.ChannelOperationResults[channel]["DetectKeywords"].Add(segmentData);

            await _jobsRepository.SaveOperationResult(job);
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
        private static async Task WriteTxtResultAsync(string text, string pathDestFile)
        {
            List<string> allLines = new List<string>();
            while (text.Length != 0)
            {
                if (text.Length <= 32)
                {
                    allLines.Add(text);
                    text = "";
                }
                else if (text.Length > 32)
                {
                    int closeSpace = text.LastIndexOfAny(" .,;:{}\uFF0C\uFF0E\uFF1A\uFF1B".ToCharArray(), 32);   //Unicode for FullWidth: ,.:;
                    if (closeSpace == -1)
                        closeSpace = 30;
                    string startLine = text.Substring(0, closeSpace);
                    allLines.Add(startLine);
                    int len = text.Length - 1 - closeSpace;
                    text = text.Substring(closeSpace + 1, len);
                }
            }
            int numOfBlankLines = 22 - allLines.Count;
            using (StreamWriter file = new StreamWriter(pathDestFile, true, Encoding.Unicode)) //true for append
            {
                for (int i = 0; i < numOfBlankLines; i++)
                {
                    //writes all blank lines in the beggining of the file
                    await file.WriteAsync(new String(' ', 40));
                    await file.WriteAsync("\n");
                }
                foreach (string curline in allLines) //writing the text in the correct line in the txt file
                {
                    if (curline.Length == 32)
                    {
                        string line = new String(' ', 4) + curline + new String(' ', 4);
                        await file.WriteAsync(line);
                        await file.WriteAsync("\n");
                    }
                    else if (curline.Length < 32)
                    {
                        int half = (int)Math.Ceiling((double)curline.Length / 2);
                        int indexStart = 16 - half + 4;
                        int numOfspaces = indexStart - 1;
                        string line = new String(' ', numOfspaces) + curline + new string(' ', numOfspaces);
                        if (line.Length < 40)
                        {
                            line += new string(' ', 40 - line.Length);
                        }
                        await file.WriteAsync(line);
                        await file.WriteAsync("\n");
                    }
                }
            }

        }
        private static int GetChannelID(string channel)
        {
            int channelID = Convert.ToInt32(channel.ToLower().Replace("channel",""));
            return channelID;
        }
    }
}
