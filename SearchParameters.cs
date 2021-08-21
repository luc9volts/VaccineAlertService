using System;
using System.Text.RegularExpressions;

namespace VaccineAlertService
{
    public class SearchParameters
    {
        public SearchParameters(string pattern, string idGroup, int[] ages, DateTime? doseDate)
        {
            Pattern = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
            IdGroup = idGroup;
            Ages = ages;
            DoseDate = doseDate;
        }

        public Regex Pattern { get; }
        public string IdGroup { get; }
        public int[] Ages { get; }
        public DateTime? DoseDate { get; }
    }
}