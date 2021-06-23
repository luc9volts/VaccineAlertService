using System;
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
        private readonly SearchSettings _searchSettings;
        private readonly Regex _regExpAge;
        private readonly Contact _contact;
        private readonly string[] _destinationPhones;

        public Worker(ILogger<Worker> logger, IOptions<AppSettings> appSettings, IOptions<Destination> destination)
        {
            _logger = logger;
            _searchSettings = appSettings.Value.SearchSettings;
            _regExpAge = new Regex(_searchSettings.Pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
            _contact = new Contact(appSettings.Value.ContactSettings);
            _destinationPhones = destination.Value.Phones;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var seacher = new Search();
                var pageContent = await seacher.GetPageSourceAsync(_searchSettings.UrlToSearch);
                var ageTarget = _searchSettings.AgeTarget;
                var ageGroup = GetLimitsAgeGroup(pageContent);

                var result = ageGroup switch
                {
                    (var min, var max) when ageTarget >= min && ageTarget <= max => _contact.MakeCall(_destinationPhones),
                    _ => new[] { "Ainda não disponível" }
                };

                _logger.LogInformation("Worker running at: {time} result {result}", DateTimeOffset.Now, result);
                await Task.Delay(300 * 1000, stoppingToken);
            }
        }

        private (int, int) GetLimitsAgeGroup(string pageContent)
        {
            var list = _regExpAge
                .Matches(pageContent)
                .SelectMany(match => match.Groups.Values.Skip(1))
                .Select(group => int.Parse(group.Value))
                .OrderBy(x => x)
                .Distinct()
                .DefaultIfEmpty()
                .ToList();

            return (list.Min(), list.Max());
        }
    }
}
