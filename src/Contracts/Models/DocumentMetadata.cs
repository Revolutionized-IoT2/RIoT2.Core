using System;
using System.Collections.Generic;

namespace RIoT2.Core.Models
{
    public class DocumentMetadata
    {
        public DocumentMetadata() 
        {
            Properties = new Dictionary<string, string>();
        }

        public string Filename { get; set; }
        public FileOrFolder Isfolder { get; set; }
        public string Filesize { get; set; }
        public DocumentType Filetype { get; set; } 
        public long Epochmt { get; set; } 
        public Dictionary<string, string> Properties { get; set; }
        public DateTime Timestamp 
        { 
            get 
            {
                return Epochmt.FromEpoch();
            } 
        }
    }
}
