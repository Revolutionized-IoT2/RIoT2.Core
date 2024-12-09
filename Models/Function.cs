using System.Collections.Generic;

namespace RIoT2.Core.Models
{
    public class Function
    {
        public string Name { get; set; }
        public string FunctionId { get; set; }
        public object Data { get; set; }
        public ValueModel DataAsModel { get { return new ValueModel(Data); }  }
        public IEnumerable<Parameter> Parameters { get; set; }
    }
}
