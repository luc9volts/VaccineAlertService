using System;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace VaccineAlertService
{
    public class AppSettings
    {
        public SearchSettings SearchSettings { get; set; }
        public Target[] Targets { get; set; }
        public ContactSettings ContactSettings { get; set; }
    }

    public class SearchSettings
    {
        public string UrlToSearch { get; set; }
    }

    public class Target
    {
        private readonly Regex _compiledPattern;
        private readonly string _pattern;

        public string Pattern
        {
            get => _pattern;
            init
            {
                _pattern = value;
                _compiledPattern = new Regex(_pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
            }
        }
        public Detail[] Details { get; init; }
        public string[] Phones { get; init; }
        public MatchCollection MatchPattern(string text) => _compiledPattern.Matches(text);

        public bool MatchDetails(Match m)
        {
            var firstAge = int.Parse($"0{m.Groups["FrtAge"].Value}");
            var secondAge = int.Parse($"0{m.Groups["SecAge"].Value}");
            var firstDoseDate = DateTime.Parse($"{(m.Groups["Day"].Success ? m.Groups["Day"].Value : 1)}/2021", new CultureInfo("pt-BR"));

            return this.Details.Any(d => d.Age >= firstAge
                                        && d.Age <= secondAge
                                        && (d.FirstDoseDate <= firstDoseDate
                                        || !d.FirstDoseDate.HasValue));
        }
    }

    public class Detail
    {
        public int Age { get; init; }
        public DateTime? FirstDoseDate { get; init; }
    }

    public class ContactSettings
    {
        public string AccountSid { get; set; }
        public string AuthToken { get; set; }
        public string OriginPhone { get; set; }
        public string UrlAudio { get; set; }
    }
}