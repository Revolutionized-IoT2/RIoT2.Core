using RIoT2.Core.Models;
using System.Collections.Generic;

namespace RIoT2.Core.Interfaces.Services
{
    public interface IMemoryStorageService
    {
        void Save(Document document, string address);
        Document Get(string filename, string address = "");
        Document GetLatest(string address);
        List<DocumentMetadata> List(string address);
        void Format(string address);

    }
}