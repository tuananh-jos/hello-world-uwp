using System;
using Windows.ApplicationModel.Background;
using Windows.Storage;

namespace App4.BackgroundTasks
{
    /// <summary>
    /// In-process background task triggered manually via ApplicationTrigger.
    /// Writes a heartbeat timestamp to app LocalFolder to prove it ran.
    /// </summary>
    public sealed class HeartbeatTask : IBackgroundTask
    {
        public async void Run(IBackgroundTaskInstance taskInstance)
        {
            // Deferral is required for any async work inside a background task.
            // Without it the runtime tears down the task before async ops complete.
            var deferral = taskInstance.GetDeferral();

            try
            {
                var timestamp = DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                var message   = $"[HeartbeatTask] Ran at {timestamp} | Cost: {taskInstance.SuspendedCount} suspends";

                // Write proof-of-execution file into app's LocalFolder.
                // UWP sandbox can read this file back in the foreground.
                var folder = ApplicationData.Current.LocalFolder;
                var file   = await folder.CreateFileAsync(
                    "HeartbeatTaskLog.txt",
                    CreationCollisionOption.ReplaceExisting);

                await FileIO.WriteTextAsync(file, message);
            }
            finally
            {
                // Always complete the deferral — even on exception — or the
                // task will appear to hang and the system will cancel it.
                deferral.Complete();
            }
        }
    }
}
