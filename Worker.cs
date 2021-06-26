using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
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
        const string FILENAME = "ALERTED_AGES.TXT";
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
                var seacher = new Search();
                var pageContent = await seacher.GetPageSourceAsync(_searchSettings.UrlToSearch);
                var ageGroup = GetAgeGroupsAvailable(pageContent);
                var targetAges = FilterAlreadyAlertedAges(_searchSettings.TargetAges);

                if (!targetAges.Any())
                {
                    _appLifeTime.StopApplication();
                    break;
                }

                var coveredTargetAges = GetCoveredTargetAges(targetAges, ageGroup);
                var contact = new Contact(_contactSettings);
                var message = !coveredTargetAges.Any()
                    ? $"Ainda não liberado para {string.Join(',', targetAges)}"
                    : contact.MakeCall(_searchSettings.TargetPhones, $"{string.Join(" E ", coveredTargetAges.Select(c => c.Text))} LIBERADOS PARA CADASTRO." );

                SaveAlreadyAlertedAges(coveredTargetAges.Select(c => c.TargetAge).ToArray());
                _logger.LogInformation($"Worker running at: {DateTime.Now} result {message}");
                await Task.Delay(FIVEMINUTES, stoppingToken);
            }
        }

        private int[] FilterAlreadyAlertedAges(int[] targetAges)
        {
            if (!File.Exists(FILENAME)) return targetAges;

            var savedAgesFile = Array.ConvertAll(File.ReadAllText(FILENAME).Split(','), s => int.Parse(s));
            return targetAges.Where(a => !savedAgesFile.Contains(a)).ToArray();
        }

        private void SaveAlreadyAlertedAges(int[] alertedAges)
        {
            if (!alertedAges.Any())
                return;

            var comma = File.Exists(FILENAME) ? "," : string.Empty;
            File.AppendAllText(FILENAME, $"{comma}{string.Join(',', alertedAges)}");
        }

        private IEnumerable<AgeGroup> GetAgeGroupsAvailable(string pageContent)
        {
            return _regExpAge
                .Matches(pageContent)
                .Select(match => match.Groups.Values.ToArray())
                .Select(group => new AgeGroup { Text = group[0].Value, Min = int.Parse(group[1].Value), Max = int.Parse(group[2].Value) })
                .GroupBy(g => g.Text, (key, g) => new AgeGroup { Text = key, Min = g.First().Min, Max = g.First().Max })
                .Distinct()
                .ToList();
        }
        private IEnumerable<(int TargetAge, string Text, DateTime AlertDate)> GetCoveredTargetAges(int[] targetAges, IEnumerable<AgeGroup> ageGroup)
        {
            return targetAges
                .Select(targetAge =>
                {
                    var a = ageGroup.FirstOrDefault(ag => ag.Min <= targetAge && ag.Max >= targetAge);
                    return (TargetAge: targetAge, Text: a?.Text, AlertDate: DateTime.Now);
                })
                .Where(a => !string.IsNullOrEmpty(a.Text))
                .ToList();
        }
    }
}
