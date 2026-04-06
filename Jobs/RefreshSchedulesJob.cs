using Quartz;
using System;
using System.Threading.Tasks;

namespace ellabi.Jobs
{
    public class RefreshSchedulesJob : IJob
    {
        public async Task Execute(IJobExecutionContext context)
        {
            try
            {
                StaticCode.OnRefreshSchedules();
            }
            catch (Exception ex)
            {
                StaticCode.Logger?.Here().Error(ex.Message);
            }

            await Task.CompletedTask;
        }
    }
}
