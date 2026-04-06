namespace ellabi.Schedules
{
    public class AdvancedSchedule : ScheduleBase
    {
        private string? _schedule;

        public override bool IsValid =>
            !string.IsNullOrWhiteSpace(CronExpression) &&
            Quartz.CronExpression.IsValidExpression(CronExpression);

        public override string CronExpression => Schedule ?? string.Empty;

        public string? Schedule
        {
            get => _schedule;
            set { _schedule = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsValid)); }
        }
    }
}
