using RIoT2.Core.Interfaces;
using System.Collections.Generic;

namespace RIoT2.Core.Models
{
    public class RuleOutput : IRuleItem
    {
        private ValueModel _value;
        private ValueModel _mapping;
        public int Id { get; set; }
        public string Name { get; set; }
        public RuleType RuleType { get { return RuleType.Output; } }

        //output
        public string CommandId { get; set; }
        public string Description { get; set; }
        public OutputOperation Operation { get; set; }
        public bool ContinueFlow { get; set; }
        public object DataModelMapping
        {
            get
            {
                if (_mapping == null)
                    return null;

                return _mapping.GetAsObject();
            }
            set
            {
                _mapping = new ValueModel(value);
            }
        }

        public ValueModel DataModelMappingAsValue 
        {
            get 
            {
                return _mapping;
            }
        }

        public bool UseMapping { get; set; }

        //Template values! -> update from template when given to UI
        public ValueType ValueType { get { return _value.Type; } }
        public object Model
        {
            get
            {
                if (_value == null)
                    return null;

                return _value.GetAsObject();
            }
            set
            {
                _value = new ValueModel(value);
            }
        }

        /*
        public string Map(string jsonDataModel) 
        {
            if (string.IsNullOrEmpty(DataModelMapping))
                return jsonDataModel;

            JObject model = JObject.Parse(jsonDataModel);

            if (Json.Instance.IsJson(DataModelMapping))
            {
                var returnObj = new JObject();
                JObject j = JObject.Parse(DataModelMapping);
                foreach (var entry in j) 
                {
                    var currentKey = entry.Key;
                    var currentValue = entry.Value.ToString();
                    if (model.ContainsKey(currentValue)) 
                        returnObj.Add(currentKey, model[currentValue]);
                }

                return Json.Instance.Serialize(returnObj);
            }
            else //only one field select field from incoming model
            {
                return model[DataModelMapping].Value<string>();
            }
        }*/

        public IEnumerable<string> Validate()
        {
            if (string.IsNullOrEmpty(CommandId))
                yield return $"Output [{Id}]: Missing CommandId";

            if (string.IsNullOrEmpty(Description))
                yield return $"Output [{Id}]: Missing Description";

            if (string.IsNullOrEmpty(Name))
                yield return $"Output [{Id}]: Missing Name";
        }
    }
}
