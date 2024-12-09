using System;

namespace RIoT2.Core.Models
{
    public class Variable
    {
        private ValueModel _value;
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public bool IsPersistant { get; set; } //if set to true value is saved -> keeps state after system boot
        public string ValueAsJson 
        {
            get 
            {
                return _value.ToJson();
            }
            set 
            {
                _value = new ValueModel(value);
            }
        }
        public ValueType Type 
        {
            get { return _value.Type; }
        }
        public ValueModel Model { get { return _value; } }
        public object Value 
        {
            get { return _value.GetAsObject(); }
            set { _value = new ValueModel(value); }
        }

        public ReportTemplate GetAsReportTemplate() 
        {
            return new ReportTemplate()
            {
                Id = Id,
                Name = Name,
                Address = "variable",
                MaintainHistory = true,
                Type = Model.Type,
                Parameters = null,
                RefreshSchedule = null
            };
        }

        public CommandTemplate GetAsCommandTemplate()
        {
            return new CommandTemplate()
            {
                Id = Id,
                Name = Name,
                Address = "variable",
                Type = Model.Type,
                Model = Model
            };
        }

        public Report CreateReport() 
        {
            return new Report()
            {
                Id = Id,
                TimeStamp = DateTime.UtcNow.ToEpoch(),
                Value = _value
            };
        }
    }
}
