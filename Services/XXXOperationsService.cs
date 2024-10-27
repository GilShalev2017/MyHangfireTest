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

namespace HangfireTest.Services
{
    public interface IXXXOperationsService
    {
        Task<string> ExtractAudioAsync(string videoFilePath);
        bool IsExtractionSucceeded(string mp3File);
        Task /*<FaceDetectionResult>*/ DetectFacesAsync(string filePath);
        Task /*<LogoDetectionResult>*/ DetectLogosAsync(string filePath);
        Task<InsightResult> TranscribeFileAsync(string audioFilePath, string sttFile);
        Task TranslateTranscriptionAsync(JobRequest jobRequest, string channel, InsightResult sttInsightResult, string sttJsonFile);
        Task DetectKeywordsAsync(JobRequest jobRequest, InsightResult insightResult, string channel, string sttJsonFile);
        Task SaveTranscriptionAsClosedCaptionsAsync(InsightResult sttInsightResult, string audioFilePath, string outputFolderPath, string channel);
        //Task<LanguageDetectionResult> DetectAudioLanguageAsync(string filePath);
        //Task<UnexpectedLanguageResult> DetectUnexpectedLanguageAsync(string filePath, string expectedLanguage);
    }

    public class XXXOperationsService : IXXXOperationsService
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger(); // NLog logger
        private static readonly Dictionary<string, string> languageIds = new();
        //private readonly IAccountManagerConnector _accountManagerConnector;
        //private static HttpClient _httpClient = new HttpClient();
        //private readonly EmailService _emailService;

        public XXXOperationsService()//EmailService emailService)//IAccountManagerConnector accountManagerConnector)
        {
            InitProvidersEnvironment();
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
                    languageIds.Add(languageDm.DisplayName, languageDm.EnglishName!);
                }
            }
        }

        #region IXXXOperationsService
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

            var insightResult = await GetAudioBasedCaptionsTest(SystemInsightTypes.Transcription, ProviderType.OpenAI, audioFilePath);

            await SaveInsightToFileAsync(insightResult!, sttFile);

            Logger.Debug($"[TranscribeFileAsync] Transcription saved to: {sttFile}");

            return insightResult!;
        }
        public async Task TranslateTranscriptionAsync(JobRequest jobRequest, string channel, InsightResult sttInsightResult, string sttJsonFile)
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
        public async Task DetectKeywordsAsync(JobRequest jobRequest, InsightResult insightResult, string channel, string sttJsonFile)
        {
            if (jobRequest.Keywords != null && jobRequest.Keywords.Count > 0)
            {
                var keywordMatches = await FindKeywordsAsync(insightResult, jobRequest.Keywords, channel, sttJsonFile);

                if (keywordMatches.Count > 0)
                {
                    string keywordFile = sttJsonFile.Replace(".json", "_keywords.json");
                    await SaveKeywordMatchesToFileAsync(keywordFile, keywordMatches);
                }
                //foreach (var keywordMatch in keywordMatches)
                //{
                //    //SendKeywordNotification(keywordMatch, channel);
                //    SendKeywordNotificationEmail(keywordMatch, channel,"gilshalev@actusdigital.com");
                //}
            }
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
