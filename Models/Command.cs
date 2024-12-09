using RIoT2.Core.Interfaces;
using RIoT2.Core.Utils;

namespace RIoT2.Core.Models
{
    public class Command : ICommand
    {
        public string Id { get; set; }
        public ValueModel Value { get; set; }

        public static Command Create(string json)
        {
            return Json.Deserialize<Command>(json);
            /*
            var dict = json.ToDict();
            if (!dict.ContainsKey("id") || !dict.ContainsKey("value"))
                return null;

            return new Command()
            {
                Id = dict["id"].ToString(),
                Value = (dict["value"] is object) ? dict["value"].ToJson() : dict["value"].ToString()
            };*/
        }
    }
}
