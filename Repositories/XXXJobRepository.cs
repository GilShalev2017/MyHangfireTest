﻿using ActIntelligenceService.Infrastructure;
using Hangfire.Common;
using HangfireTest.Models;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson;
using MongoDB.Driver;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Http;
using System.Globalization;
using ActIntelligenceService.Domain.Models.AIClip;
using System.Threading;

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
        public List<int> ChannelIds { get; set; } = new List<int>();
        public required string BroadcastStartTime { get; set; }
        public required string BroadcastEndTime { get; set; }
        public Rule? RequestRule { get; set; }
        public List<string>? Keywords { get; set; } = new List<string>();
        public required List<string> Operations { get; set; } = new List<string>();
        public Dictionary<string, Dictionary<string, List<KeywordMatchSegment>>> ChannelOperationResults { get; set; } = new Dictionary<string, Dictionary<string, List<KeywordMatchSegment>>>();
        public string? ExpectedAudioLanguage { get; set; }
        public List<string>? TranslationLanguages { get; set; }
        public List<string> NotificationIds { get; set; } = new List<string>();

        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }
        public string? Status { get; set; } //pending, succeded, failed
        public DateTime? NextScheduledTime { get; set; }
        public string? CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
    }
    public interface IXXXJobRepository
    {
        Task CreateJobAsync(JobRequestEntity jobRequest);
        Task DeleteJobAsync(string jobId);
        Task<JobRequestEntity> GetJobStatusAsync(string jobId);
        Task<List<JobRequestEntity>> GetAllJobsAsync();
        Task<List<JobRequestEntity>> GetUnfinishedJobsAsync();
        Task<List<JobRequestEntity>> GetJobsByStatusAsync(string status);
        Task UpdateJobStatusAsync(JobRequestEntity job, string status);
        Task SaveOperationResult(JobRequestEntity job);
        Task SaveOperationResultForChannel(string jobId, string channelName, string operationName, object segmentResult);
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
        public async Task CreateJobAsync(JobRequestEntity jobRequest)
        {
            // Define the format that matches the file timestamp
            string format = "yyyy_MM_dd_HH_mm_ss";

            // Attempt to parse the file timestamp into a DateTime object
            if (DateTime.TryParseExact(jobRequest.BroadcastStartTime,
                                       format,
                                       CultureInfo.InvariantCulture,
                                       DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                                       out DateTime scheduledTime))
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
                    ChannelIds = jobRequest.ChannelIds,
                    BroadcastStartTime = jobRequest.BroadcastStartTime,
                    BroadcastEndTime = jobRequest.BroadcastEndTime,
                    RequestRule = jobRequest.RequestRule,
                    Keywords = jobRequest.Keywords,
                    Operations = jobRequest.Operations,
                    ExpectedAudioLanguage = jobRequest.ExpectedAudioLanguage,
                    TranslationLanguages = jobRequest.TranslationLanguages,
                    NotificationIds = jobRequest.NotificationIds,
                    Status = "Pending",
                    CreatedAt = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss"),
                    CreatedBy = "Administrator",
                    NextScheduledTime = DateTime.SpecifyKind(scheduledTime, DateTimeKind.Utc)
                };

                await _jobsCollection.InsertOneAsync(newJob);
            }
        }
        public async Task<List<JobRequestEntity>> GetAllJobsAsync()
        {
            return await _jobsCollection.Find(Builders<JobRequestEntity>.Filter.Empty).ToListAsync();
        }
        public async Task<JobRequestEntity> GetJobStatusAsync(string jobId)
        {
            return await _jobsCollection.Find(entry => entry.Id == jobId).FirstOrDefaultAsync();
        }
        public async Task<List<JobRequestEntity>> GetJobsByStatusAsync(string status)
        {
            var filter = Builders<JobRequestEntity>.Filter.Eq(j => j.Status, status);
            return await _jobsCollection.Find(filter).ToListAsync();

            //var now = DateTime.Now;
            //var filter = Builders<JobRequestEntity>.Filter.And(
            //    Builders<JobRequestEntity>.Filter.Eq(j => j.Status, status),
            //    Builders<JobRequestEntity>.Filter.Lte(j => j.NextScheduledTime, now)
            //);
            //return await _jobsCollection.Find(filter).ToListAsync();
        }
        public Task<List<JobRequestEntity>> GetUnfinishedJobsAsync()
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
            job.NextScheduledTime = GetNextScheduledTime(job);
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
        public async Task SaveOperationResult(JobRequestEntity job)
        {
            var filter = Builders<JobRequestEntity>.Filter.Eq(j => j.Id, job.Id);

            // Set ChannelOperationResults directly to update the document
            var update = Builders<JobRequestEntity>.Update.Set(j => j.ChannelOperationResults, job.ChannelOperationResults);

            await _jobsCollection.FindOneAndUpdateAsync(filter, update);
        }
        public async Task SaveOperationResultForChannel(string jobId, string channelName, string operationName, object segmentResult)
        {
            // Filter to find the job by its Id
            var filter = Builders<JobRequestEntity>.Filter.Eq(j => j.Id, jobId);

            // Define the path to the specific channel and operation
            var updatePath = $"ChannelOperationResults.{channelName}.{operationName}";

            // Update definition to add the segment result to the specified channel and operation
            var update = Builders<JobRequestEntity>.Update.Push(updatePath, segmentResult);

            // Use FindOneAndUpdateAsync to apply the update
            await _jobsCollection.FindOneAndUpdateAsync(filter, update);
        }
        public async Task DeleteJobAsync(string jobId)
        {
            await _jobsCollection!.DeleteOneAsync(d => d.Id == jobId);
        }
    }
}
