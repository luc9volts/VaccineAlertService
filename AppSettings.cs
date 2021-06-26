namespace VaccineAlertService
{
    public class AppSettings
    {
        public SearchSettings SearchSettings { get; set; }
        public ContactSettings ContactSettings { get; set; }
    }

    public class ContactSettings
    {
        public string AccountSid { get; set; }
        public string AuthToken { get; set; }
        public string OriginPhone { get; set; }
        public string UrlAudio { get; set; }
    }

    public class SearchSettings
    {
        public string UrlToSearch { get; set; }
        public int[] TargetAges { get; set; }
        public string Pattern { get; set; }
        public string[] TargetPhones { get; set; }
    }
}