using System;

namespace RIoT2.Core.Models
{
    public class Variable
    {
        private ValueModel _value = new ValueModel(null);
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

        /// <summary>
        /// Creates a report template representing this variable.
        /// </summary>
        /// <returns>A <see cref="ReportTemplate"/> that describes the variable's value.</returns>
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
                Model = Model.GetAsObject()
            };
        }

        /// <summary>
        /// Creates a command template representing this variable.
        /// </summary>
        /// <returns>A <see cref="CommandTemplate"/> that describes the variable's value.</returns>
        public CommandTemplate GetAsCommandTemplate()
        {
            return new CommandTemplate()
            {
                Id = Id,
                Name = Name,
                Address = "variable",
                Type = Model.Type,
                Model = Model.GetAsObject()
            };
        }

        /// <summary>
        /// Creates a report carrying the variable's current value and the current timestamp.
        /// </summary>
        /// <returns>A <see cref="Report"/> for this variable.</returns>
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
