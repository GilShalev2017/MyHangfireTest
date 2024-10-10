using ActIntelligenceService.Domain.Models;
using HangfireTest.Models;
using System.Diagnostics;

namespace HangfireTest.Utility
{
    //public class FaceDetectionJob
    //{
    //    private readonly string filePath;

    //    public FaceDetectionJob(string filePath)
    //    {
    //        this.filePath = filePath;
    //    }

    //    public void Execute()
    //    {
    //        // Call face/logo detection provider here
    //    }
    //}

    //public class SpeechToTextJob
    //{
    //    private readonly string filePath;
    //    public string Result { get; private set; }

    //    public SpeechToTextJob(string filePath)
    //    {
    //        this.filePath = filePath;
    //    }

    //    public void Execute()
    //    {
    //        // Extract audio, perform STT, and save transcription to Result
    //    }
    //}

    //public class KeywordDetectionJob
    //{
    //    private readonly string transcription;

    //    public KeywordDetectionJob(string transcription)
    //    {
    //        this.transcription = transcription;
    //    }

    //    public void Execute()
    //    {
    //        // Perform keyword detection and send alerts
    //    }
    //}

    //public class TranslationJob
    //{
    //    private readonly string transcription;

    //    public TranslationJob(string transcription)
    //    {
    //        this.transcription = transcription;
    //    }

    //    public void Execute()
    //    {
    //        // Translate the transcription to the desired language
    //    }
    //}

    public static class JobType
    {
        public const string KeywordsDetection = "KeywordsDetection";
        public const string CreateClosedCaptions = "CreateClosedCaptions";
        public const string Translation = "Translation";
        public const string VerifyAudioLanguage = "VerifyAudioLanguage";
        public const string FaceDetection = "FaceDetection";
        public const string LogoDetection = "LogoDetection";
    }


    public class Jobs
    {
        //These is the actual functionality of each job type
        
        // Directory to store files
        static string inputFilesDirectory = @"C:\Development\HangfireTest\Media\Record";
        static string outputFilesDirectory = @"C:\Development\HangfireTest\InputFiles";

        //public static async Task ExecuteAudioBasedJob(JobRequest jobRquest)
        //{
        //    Stopwatch stopwatch = Stopwatch.StartNew();
        //    Console.WriteLine($"Executing Rule ID: {jobRquest.Id}");

        //    // Loop through each channel
        //    await Task.WhenAll(jobRquest.Channels.Select(async channel =>
        //    {
        //        Console.WriteLine("Transcribing Channel " + channel);

        //        var channelFolder = Path.Combine(inputFilesDirectory, channel);
        //        var startDateString = DateTime.ParseExact(jobRquest.StartTime, "yyyy_MM_dd_HH_mm_ss", null).ToString("yyyy_MM_dd");
        //        var channelWithDateFolder = Path.Combine(channelFolder, startDateString);

        //        var filesInRange = GetFilesForTimeRange(rule, channelWithDateFolder);

        //        // Loop through each file in range
        //        await Task.WhenAll(filesInRange.Select(async file =>
        //        {
        //            var mp3File = file.Replace(".mp4", ".mp3");
        //            var sttJsonFile = file.Replace(".mp4", ".json");

        //            if (!File.Exists(mp3File))
        //            {
        //                // Convert the file to mp3
        //                mp3File = await ConvertToMp3Async(file);

        //                // Check if the mp3 file size is too small
        //                long filesizebytes = new FileInfo(mp3File).Length;
        //                if (filesizebytes < 5000000) // Less than 5MB
        //                {
        //                    Console.WriteLine($"Conversion failed for file {file}. Skipping transcription.");
        //                    return;  // Exit this task and move to the next file
        //                }
        //            }

        //            // Only transcribe if the json file doesn't exist
        //            if (!File.Exists(sttJsonFile))
        //            {
        //                InsightResult insightResult = await TranscribeFileAsync(mp3File);

        //                await AnotherSerializeInsightResultToFileAsync(insightResult, sttJsonFile);

        //                if (rule.Keywords != null && rule.Keywords.Count > 0)
        //                {
        //                    var keywordMatches = await FindKeywordsAsync(insightResult, rule.Keywords, channel, sttJsonFile);

        //                    if (keywordMatches.Count > 0)
        //                    {
        //                        string keywordFile = sttJsonFile.Replace(".json", "_keywords.json");
        //                        await SaveKeywordMatchesToFileAsync(keywordFile, keywordMatches);
        //                    }
        //                }
        //            }
        //        }));

        //    }));

        //    stopwatch.Stop();

        //    var elapsed = stopwatch.ElapsedMilliseconds;

        //    var elapsedMinutes = stopwatch.ElapsedMilliseconds / 60000.0;

        //    Console.WriteLine($"Finished Executing Rule ID: {rule.Id}, Elapsed Time: {elapsedMinutes} minutes");
        //}

        

        //public static async Task ExecuteImageProcessingJob()
        //{

        //}
    }
}
