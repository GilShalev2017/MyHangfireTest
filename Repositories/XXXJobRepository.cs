using ActIntelligenceService.Infrastructure;
using Hangfire.Common;
using HangfireTest.Models;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson;
using MongoDB.Driver;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Http;
using System.Globalization;

namespace HangfireTest.Repositories
{
    public class JobRequestEntity
    {
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

        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }
        public string? Status { get; set; } //pending, succeded, failed
        public DateTime? ScheduledTime { get; set; }
        public DateTime? CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
    }
    public interface IXXXJobRepository
    {
        Task CreateJobAsync(JobRequest jobRequest);
        Task<JobRequestEntity> GetJobStatusAsync(string jobId);
        Task<List<JobRequestEntity>> GetUnfinishedJobsAsync();
        Task<List<JobRequestEntity>> GetAllPendingJobsAsync();
    }
    public class XXXJobRepository : IXXXJobRepository
    {
        private readonly IMongoCollection<JobRequestEntity> _jobsCollection;
        private const string CollectionName = "mediainsight_jobs";

        public XXXJobRepository(IMongoClient mongoClient)
        {
            var database = mongoClient.GetDatabase("ActusIntelligenceTest");
           
            _jobsCollection = database.GetCollection<JobRequestEntity>(CollectionName);
        }
        public async Task CreateJobAsync(JobRequest jobRequest)
        {
            var newJob = new JobRequestEntity
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
                TranslationLanguages = jobRequest.TranslationLanguages,

                Status = "Pending",
                CreatedAt = DateTime.Now,
                //ScheduledTime = string dateString = "2023-12-31T09:40:00";
                //ScheduledTime = DateTime.Parse(dateString),
                ScheduledTime = DateTime.ParseExact(jobRequest.BroadcastStartTime, "yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture)
            };

            await _jobsCollection.InsertOneAsync(newJob);
        }
        public async Task<JobRequestEntity> GetJobStatusAsync(string jobId)
        {
            return await _jobsCollection.Find(entry => entry.Id == jobId).FirstOrDefaultAsync();
        }
        public async Task<List<JobRequestEntity>> GetAllPendingJobsAsync()
        {
            var now = DateTime.UtcNow;
            var filter = Builders<JobRequestEntity>.Filter.And(
                Builders<JobRequestEntity>.Filter.Eq(j => j.Status, "Pending"),
                Builders<JobRequestEntity>.Filter.Lte(j => j.ScheduledTime, now)
            );
            return await _jobsCollection.Find(filter).ToListAsync();
        }
        public Task<List<JobRequest>> GetUnfinishedJobsAsync()
        {
            throw new NotImplementedException();
        }
        public async Task UpdateJobStatusAsync(JobRequestEntity job, string status)
        {
            var filter = Builders<JobRequestEntity>.Filter.Eq(j => j.Id, job.Id);
            var update = Builders<JobRequestEntity>.Update.Set(j => j.Status, status);
            await _jobsCollection.UpdateOneAsync(filter, update);
        }
        public async Task ScheduleNextOccurrenceAsync(JobRequestEntity job)
        {
            job.ScheduledTime = GetNextScheduledTime(job);
            var filter = Builders<JobRequestEntity>.Filter.Eq(j => j.Id, job.Id);
            await _jobsCollection.ReplaceOneAsync(filter, job);
        }
        private DateTime GetNextScheduledTime(JobRequestEntity job)
        {
            return DateTime.Now;
        }
        Task<List<JobRequestEntity>> IXXXJobRepository.GetUnfinishedJobsAsync()
        {
            throw new NotImplementedException();
        }
    }
}
