using System;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
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
        private readonly SearchSettings _searchSettings;
        private readonly ContactSettings _contactSettings;
        private readonly Regex _regExpAge;
        const int FIVEMINUTES = 300000;

        public Worker(ILogger<Worker> logger, IOptions<AppSettings> appSettings, IHostApplicationLifetime appLifeTime)
        {
            _logger = logger;
            _appLifeTime = appLifeTime;
            _searchSettings = appSettings.Value.SearchSettings;
            _contactSettings = appSettings.Value.ContactSettings;
            _regExpAge = new Regex(_searchSettings.Pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var targetAges = _searchSettings.TargetAges;
                    var pageContent = await GetPageSourceAsync(_searchSettings.UrlToSearch);
                    var alertText = GetSecondDoseAlertText(pageContent, targetAges);

                    if (!string.IsNullOrEmpty(alertText))
                    {
                        var contact = new Contact(_contactSettings);
                        contact.MakeCall(_searchSettings.TargetPhones, alertText);
                        _appLifeTime.StopApplication();
                        break;
                    }

                    var message = $"Ainda não liberado para {string.Join(',', targetAges)}";
                    _logger.LogInformation($"Worker running at: {DateTime.Now} result {message}");
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

        private async Task<string> GetPageSourceAsync(string url)
        {
            using (var client = new HttpClient())
            {
                var response = await client.GetAsync(url);
                return await response.Content.ReadAsStringAsync();
            }
        }

        private string GetSecondDoseAlertText(string pageContent, int[] targetAges)
        {
            bool ContainsSomeTargetAge(Match match)
            {
                var firstMatchedAge = int.Parse(match.Groups["FrtAge"].Value);
                var lastMatchedAge = match.Groups["SecAge"].Success
                                        ? int.Parse(match.Groups["SecAge"].Value)
                                        : firstMatchedAge;

                return targetAges.Any(target => target >= firstMatchedAge && target <= lastMatchedAge);
            }

            var doc = new HtmlDocument();
            doc.LoadHtml(pageContent);

            var itemsFound = doc.DocumentNode
                    .SelectSingleNode(@"//div[@id='GROUP2TABLE']")
                    ?.Descendants("option")
                    .Select(option => option.InnerText)
                    .SelectMany(optionText => _regExpAge.Matches(optionText))
                    .Where(match => match.Success)
                    .Where(ContainsSomeTargetAge)
                    .Select(match => match.Value.Trim());

            return string.Join(" E ", itemsFound);
        }
    }
}
