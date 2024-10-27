using Microsoft.VisualStudio.TestPlatform.Utilities;

namespace HangfireTest.Utility
{
    public class FileWatcher
    {
        private readonly string watchDirectory;
        private readonly IJobQueue jobQueue;

        public FileWatcher(string directory, IJobQueue queue)
        {
            watchDirectory = directory;
            jobQueue = queue;
        }

        public void StartWatching()
        {
            FileSystemWatcher watcher = new FileSystemWatcher(watchDirectory, "*.mp4")
            {
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite
            };
            watcher.Created += OnNewFile;
            watcher.EnableRaisingEvents = true;
        }

        private void OnNewFile(object sender, FileSystemEventArgs e)
        {
            string filePath = e.FullPath;
            Console.WriteLine($"New .mp4 file detected: {filePath}");
            jobQueue.Enqueue(filePath); // Enqueue new file for processing
        }
    }

}
