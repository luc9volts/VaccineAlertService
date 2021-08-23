using System;
using System.Collections.Generic;
using System.Globalization;
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
        private readonly SearchSettings _settings;
        private readonly ContactSettings _contactSettings;
        private readonly IEnumerable<SearchParameters> _searches;
        private readonly List<string> _alreadyAlerted;
        const int FIVEMINUTES = 300000;

        public Worker(ILogger<Worker> logger, IOptions<AppSettings> appSettings, IHostApplicationLifetime appLifeTime)
        {
            _logger = logger;
            _appLifeTime = appLifeTime;
            _settings = appSettings.Value.SearchSettings;
            _contactSettings = appSettings.Value.ContactSettings;
            _alreadyAlerted = new List<string>();

            _searches = new List<SearchParameters>
            {
                new SearchParameters(_settings.Pattern1Dose,"GROUP1TABLE",_settings.TargetAges1Dose, null),
                new SearchParameters(_settings.Pattern2Dose,"GROUP2TABLE",_settings.TargetAges2Dose, new DateTime(2021,6,21))
            };
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var pageContent = await GetPageSourceAsync(_settings.UrlToSearch);
                    var alerts = _searches
                                .Select(s => GetAlertText(pageContent, s))
                                .Where(a => !string.IsNullOrEmpty(a))
                                .Where(a => !_alreadyAlerted.Contains(a))
                                .ToList();

                    foreach (var alert in alerts)
                    {
                        new Contact(_contactSettings).MakeCall(_settings.TargetPhones, alert);
                        _alreadyAlerted.Add(alert);
                    }

                    if (_searches.Count() == _alreadyAlerted.Count())
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

        private async Task<string> GetPageSourceAsync(string url)
        {
            using var client = new HttpClient();
            var response = await client.GetAsync(url);
            return await response.Content.ReadAsStringAsync();
        }

        private string GetAlertText(string pageContent, SearchParameters search)
        {
            bool ContainsTargetAges(Match match)
            {
                var g = match.Groups;
                (int, int, DateTime) matchedValues = (g["FrtAge"].Success, g["SecAge"].Success, g["Day"].Success) switch
                {
                    (true, true, true) => (int.Parse(g["FrtAge"].Value), int.Parse(g["SecAge"].Value), DateTime.Parse($"{g["Day"].Value}/2021", new CultureInfo("pt-BR"))),
                    (true, true, false) => (int.Parse(g["FrtAge"].Value), int.Parse(g["SecAge"].Value), new DateTime()),
                    (true, false, true) => (int.Parse(g["FrtAge"].Value), int.Parse(g["FrtAge"].Value), DateTime.Parse($"{g["Day"].Value}/2021", new CultureInfo("pt-BR"))),
                    (true, false, false) => (int.Parse(g["FrtAge"].Value), int.Parse(g["FrtAge"].Value), new DateTime()),
                    _ => (0, 0, new DateTime())
                };

                return search.Ages.Any(target => target >= matchedValues.Item1
                                                && target <= matchedValues.Item2
                                                && (search.DoseDate <= matchedValues.Item3
                                                || !search.DoseDate.HasValue));
            }

            var doc = new HtmlDocument();
            doc.LoadHtml(pageContent);

            var itemsFound = doc.DocumentNode
                    .SelectSingleNode(@$"//div[@id='{search.IdGroup}']")
                    ?.Descendants("option")
                    .Select(option => option.InnerText)
                    .SelectMany(optionText => search.Pattern.Matches(optionText))
                    .Where(match => match.Success)
                    .Where(ContainsTargetAges)
                    .Select(match => match.Value.Trim());

            return string.Join(" E ", itemsFound);
        }
    }
}
