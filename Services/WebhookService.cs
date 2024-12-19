using RIoT2.Core.Interfaces.Services;
using RIoT2.Core.Utils;
using System.Net.Http;
using System.Threading.Tasks;

namespace RIoT2.Core.Services
{
    public class WebhookService : IWebhookService
    {
        public event WebhookHandler WebhookReceived;

        public async Task<string> SendMessageAsync(string address, string content)
        {
            HttpResponseMessage result;
            if (string.IsNullOrEmpty(content))
            {
                result = await Web.GetAsync(address);
            }
            else 
            {
                result = await Web.PostAsync(address, content);
            }
            return await result.Content.ReadAsStringAsync();
        }

        public void SetWebhook(string address, string content)
        {
            WebhookReceived(address, content);
        }
    }
}