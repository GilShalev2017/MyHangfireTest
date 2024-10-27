using ActIntelligenceService.Infrastructure;
using Hangfire.Common;
using HangfireTest.Models;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson;
using MongoDB.Driver;

namespace HangfireTest.Repositories
{
    public class XXXInsightJobEntity
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        public required string Name { get; set; }
        public required bool IsRealTime { get; set; }
        public bool IsRecurring { get; set; } = false;
        public string? ExecutionTime { get; set; }
        public string? CronExpression { get; set; } //Cron time for example: "14 15 * * *" <-when to invoke the processing every day! the format is:"mm hh * * *"
        public List<string> Channels { get; set; } = new List<string>();
        public required string BroadcastStartTime { get; set; }
        public required string BroadcastEndTime { get; set; }
        public List<string>? Keywords { get; set; } = new List<string>();
        public required List<string> Operations { get; set; } = new List<string>();
        public string? ExpectedAudioLanguage { get; set; }
        public List<string>? TranslationLanguages { get; set; }


        public string? Status { get; set; } //pending, succeded, failed
        public string? StartTime { get; set; }
        public string? CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
    }
    public interface IXXXJobRepository
    {
        Task CreateJobAsync(JobRequest jobRequest);
        Task<XXXInsightJobEntity> GetJobStatusAsync(string jobId);
        Task<List<JobRequest>> GetUnfinishedJobsAsync();
    }
    public class XXXJobRepository : IXXXJobRepository
    {
        private readonly IMongoCollection<XXXInsightJobEntity> _jobsCollection;
        private const string CollectionName = "mediainsight_jobs";

        public XXXJobRepository(IMongoClient mongoClient)
        {
            var database = mongoClient.GetDatabase("ActusIntelligenceTest");
           
            _jobsCollection = database.GetCollection<XXXInsightJobEntity>(CollectionName);
        }
        public async Task CreateJobAsync(JobRequest jobRequest)
        {
            var newJob = new XXXInsightJobEntity
            {
                // Set up the job details: time range, operations, etc.
                Name = jobRequest.Name,
                IsRealTime = jobRequest.IsRealTime,
                IsRecurring = jobRequest.IsRecurring,
                ExecutionTime = jobRequest.ExecutionTime,
                CronExpression = jobRequest.CronExpression,
                Id = jobRequest.Id,
                Channels = jobRequest.Channels,
                BroadcastStartTime = jobRequest.BroadcastStartTime,
                BroadcastEndTime = jobRequest.BroadcastEndTime,
                Keywords = jobRequest.Keywords,
                Operations = jobRequest.Operations,
                ExpectedAudioLanguage = jobRequest.ExpectedAudioLanguage,
                TranslationLanguages = jobRequest.TranslationLanguages
            };

            await _jobsCollection.InsertOneAsync(newJob);
        }
        public async Task<XXXInsightJobEntity> GetJobStatusAsync(string jobId)
        {
            return await _jobsCollection.Find(entry => entry.Id == jobId).FirstOrDefaultAsync();
        }
        public Task<List<JobRequest>> GetUnfinishedJobsAsync()
        {
            throw new NotImplementedException();
        }
    }
}
