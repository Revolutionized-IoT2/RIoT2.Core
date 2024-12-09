using System.Threading.Tasks;

namespace RIoT2.Core.Interfaces.Services
{
    public interface IWebhookService
    {
        event WebhookHandler WebhookReceived;
        Task<string> SendMessageAsync(string address, string content);

        void SetWebhook(string id, string content);
    }

    public delegate void WebhookHandler(string address, string content);
}