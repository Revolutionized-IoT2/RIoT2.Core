using RIoT2.Core.Interfaces;
using RIoT2.Core.Utils;

namespace RIoT2.Core.Models
{
    public class Report : IReport
    {
        public string Id { get; set; }
        public long TimeStamp { get; set; }
        public string Filter { get; set; }
        public ValueModel Value 
        {
            get; set;
        }
        public static Report Create(string json) 
        {
            try
            {
                return Json.Deserialize<Report>(json);
                /*
                var jsonEntity = new JsonEntity(json);
                return new Report()
                {
                    Id = jsonEntity.GetValue("id").ToString(),
                    TimeStamp = long.Parse(jsonEntity.GetValue("timeStamp").ToString()),
                    Filter = jsonEntity.GetValue("filter").ToString(),
                    Value = new ValueModel(jsonEntity.GetValueAsJson("value"))
                };*/
            }
            catch   
            {
                return null;
            }
        }

        public string ToJson()
        {
            return Json.SerializeIgnoreNulls(this);
            /*
            return new JsonObject
            {
                ["id"] = Id,
                ["timeStamp"] = TimeStamp,
                ["filter"] = Filter,
                ["value"] = Value.
            }.ToString();*/
        }
    }
}
