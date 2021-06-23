using System.Net.Http;
using System.Threading.Tasks;

namespace VaccineAlertService
{
    public class Search
    {
        public async Task<string> GetPageSourceAsync(string url)
        {
            using (var client = new HttpClient())
            {
                var response = await client.GetAsync(url);
                return await response.Content.ReadAsStringAsync();
            }
        }
    }
}