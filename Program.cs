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
using Microsoft.Extensions.DependencyInjection;
using System.Net.WebSockets;

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
            Logger.Error($"[Main] Error occurred: {ex.Message}");
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

        //builder.Services.AddHostedService<CustomJobSchedulerService>(provider =>
        //             (CustomJobSchedulerService)provider.GetRequiredService<ICustomJobSchedulerService>());

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

        builder.Services.AddCors(options =>
        {
            options.AddPolicy("CorsPolicy",
                builder => builder.AllowAnyOrigin()
                                  .AllowAnyMethod()
                                  .AllowAnyHeader()
                                  );
        });

        var app = builder.Build();

        app.UseCors("CorsPolicy");
        //app.UseCors(options => options.AllowAnyHeader().AllowAnyMethod().AllowAnyOrigin());

        app.UseWebSockets();

        app.Services.GetService<ICustomJobSchedulerService>();

       
        app.UseHangfireDashboard();

        app.UseHttpsRedirection();

        //app.UseSwagger();
        //app.UseSwaggerUI();

        app.MapControllers();

        app.UseRouting();

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapControllers();
            endpoints.Map("/ws", async context =>
            {
                if (context.WebSockets.IsWebSocketRequest)
                {
                    var webSocket = await context.WebSockets.AcceptWebSocketAsync();
                    await WebSocketHandler.HandleWebSocketConnection(webSocket);
                }
                else
                {
                    context.Response.StatusCode = 400;
                }
            });
        });

        return app;
    }

    //private static async Task HandleWebSocketConnection(WebSocket webSocket)
    //{
    //    var buffer = new byte[1024 * 4];
    //    WebSocketReceiveResult result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

    //    while (!result.CloseStatus.HasValue)
    //    {
    //        string message = Encoding.UTF8.GetString(buffer, 0, result.Count);
    //        Console.WriteLine("Received: " + message);

    //        // Echo the message back to the client or send another response
    //        var response = Encoding.UTF8.GetBytes("Server response: " + message);
    //        await webSocket.SendAsync(new ArraySegment<byte>(response, 0, response.Length), result.MessageType, result.EndOfMessage, CancellationToken.None);

    //        result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
    //    }

    //    await webSocket.CloseAsync(result.CloseStatus.Value, result.CloseStatusDescription, CancellationToken.None);
    //}
}