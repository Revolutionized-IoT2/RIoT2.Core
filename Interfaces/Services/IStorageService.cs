using RIoT2.Core.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RIoT2.Core.Interfaces.Services
{
    public interface IStorageService
    {
        Task Save(string filename, byte[] data);
        Task<Document> Get(string filename);
        Task<List<DocumentMetadata>> List();
        Task Delete(string filename);
        void Configure(string username, string password, string rootFolder, string ipAddress);
        bool IsConfigured();
    }
}