using ellabi.Classes;
using Quartz;
using System;
using System.Reflection;
using System.Threading.Tasks;
using System.Xml;

namespace ellabi.Jobs
{
    public class CheckForUpdateJob : IJob
    {
        public async Task Execute(IJobExecutionContext context)
        {
            try
            {
                var update = GetUpdate();

                if (update?.LatestVersion != null &&
                    update.LatestVersion > Assembly.GetExecutingAssembly().GetName().Version)
                {
                    StaticCode.UpdateUrl = update.DownloadUrl ?? string.Empty;
                    StaticCode.OnUpdateAvailablityChanged(true);
                }
            }
            catch (Exception ex)
            {
                StaticCode.Logger?.Here().Error(ex.Message);
            }

            await Task.CompletedTask;
        }

        private Update? GetUpdate()
        {
            try
            {
                var xmlDoc = new XmlDocument();
                xmlDoc.Load(StaticCode.UpdateXmlUrl);
                var versionNode = xmlDoc.SelectSingleNode("Update/LatestVersion");
                var urlNode     = xmlDoc.SelectSingleNode("Update/DownloadUrl");

                if (versionNode != null && urlNode != null &&
                    !string.IsNullOrWhiteSpace(versionNode.InnerText) &&
                    !string.IsNullOrWhiteSpace(urlNode.InnerText) &&
                    Version.TryParse(versionNode.InnerText, out var version))
                {
                    return new Update { DownloadUrl = urlNode.InnerText, LatestVersion = version };
                }
            }
            catch (Exception ex)
            {
                StaticCode.Logger?.Here().Error(ex.Message);
            }

            return null;
        }
    }
}
