using RIoT2.Core.Interfaces;

namespace RIoT2.Core.Models
{
    public class CommandTemplate : ICommand, IDeviceObject, ITemplate
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Address { get; set; }
        public object? Model { get; set; }
        public ValueType Type { get; set; }

        /// <summary>
        /// Optional description of the command's value (Core 2.2.0): its JSON type, unit, meaning and range, declared
        /// by the device plugin (or the user), so the UI can build forms and pick dashboard cards. When <c>null</c>,
        /// the UI infers it from <see cref="Model"/>. Omitted when <c>null</c>.
        /// </summary>
        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public ValueSchema? Schema { get; set; }

        /// <summary>
        /// Returns the default command genereated from this template.
        /// </summary>
        /// <returns>Default command template</returns>
        public Command GetAsCommand()
        {
            return new Command
            {
                Id = this.Id,
                Value = new ValueModel(this.Model)
            };
        }
    }
}