using Quartz;
using System;
using System.Threading.Tasks;

namespace ellabi.Jobs
{
    public class ResetUpdateStatusJob : IJob
    {
        public async Task Execute(IJobExecutionContext context)
        {
            try
            {
                StaticCode.OnUpdateAvailablityChanged(false);
            }
            catch (Exception ex)
            {
                StaticCode.Logger?.Here().Error(ex.Message);
            }

            await Task.CompletedTask;
        }
    }
}
