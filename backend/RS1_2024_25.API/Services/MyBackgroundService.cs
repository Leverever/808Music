using NCrontab;
using RS1_2024_25.API.Data;
using RS1_2024_25.API.Endpoints.AlbumEndpoints;
using RS1_2024_25.API.Endpoints.ArtistEndpoints;
using RS1_2024_25.API.Endpoints.NotificationEndpoints;
using RS1_2024_25.API.Helper;
using System.Runtime.Serialization;
using System.Threading.Tasks;

namespace RS1_2024_25.API.Services
{
    [DataContract]
    public enum ScheduledTaskTypes
    {
        [EnumMember(Value = "0 */1 * * *")] Hourly,
        [EnumMember(Value = "0 0 * * *")] Daily,
        [EnumMember(Value = "0 0 * * 1")] Weekly,
        [EnumMember(Value = "0 0 1 * *")] Monthly
    };

    public class MyBackgroundService : IHostedService, IDisposable
    {
        private Timer? timer;
        private Dictionary<ScheduledTaskTypes, CrontabSchedule> schedules = [];
        private string[] crontabs = ["*/1 * * * *", "*/5 * * * *", "*/10 * * * *", "*/20 * * * *"];
        private DateTime[] nextRuns = new DateTime[4];

        private readonly IConfiguration cfg;
        private readonly ILogger<MyBackgroundService> logger;
        private int isProcessing;

        public MyBackgroundService(
            IConfiguration cfg,
            ILogger<MyBackgroundService> logger)
        {
            Array tasks = Enum.GetValues(typeof(ScheduledTaskTypes));
            for (int i = 0; i < tasks.Length; i++)
            {
                ScheduledTaskTypes task = (ScheduledTaskTypes)(tasks.GetValue(i))!;
                schedules.Add(task, CrontabSchedule.Parse(StringEnumHelper.GetValue(task)));
                nextRuns[i] = schedules[task].GetNextOccurrence(DateTime.Now);
            }
            this.cfg = cfg;
            this.logger = logger;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            timer = new Timer(Process, null, TimeSpan.Zero, TimeSpan.FromSeconds(10));
            return Task.CompletedTask;
        }

        private async void Process(object? state)
        {
            if (Interlocked.Exchange(ref isProcessing, 1) != 0)
            {
                return;
            }

            try
            {
                Array tasks = Enum.GetValues(typeof(ScheduledTaskTypes));
                using (var client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Add("BackgroundScheduler", cfg["BackendUrl"]);

                    await client.GetAsync(cfg["BackendUrl"] + "/api/" + nameof(SendArtistNotifications));

                    for (int i = 0; i < schedules.Count; i++)
                    {
                        if (nextRuns[i] < DateTime.Now)
                        {
                            switch (tasks.GetValue(i))
                            {
                                case ScheduledTaskTypes.Hourly:
                                    {
                                        //TASKS GO HERE
                                        await client.GetAsync(cfg["BackendUrl"] + "/api/" + nameof(AlbumToggleVisibilityEndpoint));
                                        break;
                                    }
                                case ScheduledTaskTypes.Daily:
                                    {
                                        //TASKS GO HERE
                                        await client.DeleteAsync(cfg["BackendUrl"] + "/api/" + nameof(ArtistDeleteFlaggedEndpoint));
                                        break;
                                    }

                                case ScheduledTaskTypes.Weekly:
                                    {
                                        //TASKS GO HERE
                                        Console.WriteLine("WEEKLY TASK");

                                        break;
                                    }
                                case ScheduledTaskTypes.Monthly:
                                    {
                                        //TASKS GO HERE
                                        Console.WriteLine("MONTHLY TASK");
                                        break;
                                    }
                            }
                            nextRuns[i] = schedules.GetValueOrDefault((ScheduledTaskTypes)tasks.GetValue(i)!)!.GetNextOccurrence(DateTime.Now);
                        }
                        else
                        {
                            Console.WriteLine((ScheduledTaskTypes)tasks.GetValue(i)! + " task at: " + nextRuns[i].ToLongDateString() + nextRuns[i].ToLongTimeString());
                        }
                    }
                }
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Legacy background task execution failed; it will be retried.");
            }
            finally
            {
                Volatile.Write(ref isProcessing, 0);
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            timer?.Change(Timeout.Infinite, 0);
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            timer?.Dispose();
        }
    }
}
