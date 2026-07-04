using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using HtmlAgilityPack;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace VaccineAlertService
{
    public class Worker(ILogger<Worker> logger, IOptions<AppSettings> appSettings, IHostApplicationLifetime appLifeTime)
        : BackgroundService
    {
        private static readonly HttpClient Client = new();
        private readonly List<string> _alreadyAlerted = [];
        private const int FiveMinutesInMilliseconds = 60000;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var ranges = await GetRangesAsync();

                    var alerts = appSettings.Value.Targets
                                .SelectMany(t => GetAlerts(ranges, t))
                                .Where(a => !string.IsNullOrEmpty(a.Text))
                                .Where(a => !_alreadyAlerted.Contains(a.Text))
                                .ToList();

                    foreach (var alert in alerts)
                    {
                        var cs = appSettings.Value.ContactSettings;
                        new Contact(cs).MakeCall(alert.Target.Phones, alert.Text);
                        _alreadyAlerted.Add(alert.Text);
                    }

                    if (appSettings.Value.Targets.Length == _alreadyAlerted.Count)
                    {
                        appLifeTime.StopApplication();
                        break;
                    }

                    logger.LogInformation("Worker running at: {DateTime}", DateTime.Now);
                }
                catch (Exception ex)
                {
                    logger.LogInformation("Worker running at: {DateTime} result {ExMessage}", DateTime.Now, ex.Message);
                }
                finally
                {
                    await Task.Delay(FiveMinutesInMilliseconds, stoppingToken);
                }
            }
        }

        private async Task<IEnumerable<string>> GetRangesAsync()
        {
            var response = await Client.GetAsync(appSettings.Value.SearchSettings.UrlToSearch);
            var pageContent = await response.Content.ReadAsStringAsync();
            var doc = new HtmlDocument();
            doc.LoadHtml(pageContent);

            return doc
                    .DocumentNode
                    .SelectNodes(@$"//div[starts-with(@id,'GROUP')]")
                    ?.Descendants("option")
                    .Select(option => option.InnerText)
                    .ToList();
        }

        private IEnumerable<(string Text, Target Target)> GetAlerts(IEnumerable<string> ranges, Target target)
        {
            return ranges
                    .SelectMany(target.MatchPattern)
                    .Where(match => match.Success)
                    .Where(target.MatchDetails)
                    .Select(match => (match.Value.Trim(), target))
                    .ToList(); 
        }
    }
}
