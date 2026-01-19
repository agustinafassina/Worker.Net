using System.ComponentModel.DataAnnotations;

namespace Worker.SendNotification.Configurations
{
    public class HostedSetting
    {
        [Range(0, int.MaxValue, ErrorMessage = "DelayTimeToStart must be non-negative")]
        public int DelayTimeToStart { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "TimeBetweenExecution must be positive")]
        public int TimeBetweenExecution { get; set; }

        public string DailyTime { get; set; } = string.Empty;
    }
}
