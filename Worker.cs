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
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly IHostApplicationLifetime _appLifeTime;
        private readonly IOptions<AppSettings> _appSettings;
        private readonly List<string> _alreadyAlerted;
        const int FIVEMINUTES = 300000;

        public Worker(ILogger<Worker> logger, IOptions<AppSettings> appSettings, IHostApplicationLifetime appLifeTime)
        {
            _logger = logger;
            _appLifeTime = appLifeTime;
            _appSettings = appSettings;
            _alreadyAlerted = new List<string>();
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var ranges = await GetRangesAsync();

                    var alerts = _appSettings.Value.Targets
                                .SelectMany(t => GetAlerts(ranges, t))
                                .Where(a => !string.IsNullOrEmpty(a.Text))
                                .Where(a => !_alreadyAlerted.Contains(a.Text))
                                .ToList();

                    foreach (var alert in alerts)
                    {
                        var cs = _appSettings.Value.ContactSettings;
                        new Contact(cs).MakeCall(alert.Target.Phones, alert.Text);
                        _alreadyAlerted.Add(alert.Text);
                    }

                    if (_appSettings.Value.Targets.Count() == _alreadyAlerted.Count())
                    {
                        _appLifeTime.StopApplication();
                        break;
                    }

                    _logger.LogInformation($"Worker running at: {DateTime.Now}");
                }
                catch (Exception ex)
                {
                    _logger.LogInformation($"Worker running at: {DateTime.Now} result {ex.Message}");
                }
                finally
                {
                    await Task.Delay(FIVEMINUTES, stoppingToken);
                }
            }
        }

        private async Task<IEnumerable<string>> GetRangesAsync()
        {
            using var client = new HttpClient();
            var response = await client.GetAsync(_appSettings.Value.SearchSettings.UrlToSearch);
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
                    .ToList(); ;
        }
    }
}
