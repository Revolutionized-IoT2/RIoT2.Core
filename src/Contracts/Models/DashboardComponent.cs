using System.Collections.Generic;
using System.Dynamic;

namespace RIoT2.Core.Models
{
    public class DashboardComponent
    {

        public string Name { get; set; }
        public string Description { get; set; }
        public string Icon { get; set; }

        /// <summary>
        /// The legacy card type. A UI that writes a <see cref="Kind"/> also writes the closest of these types, so
        /// older readers still show the card.
        /// </summary>
        public DashboardComponentType Type { get; set; }
        public DashboardComponentSize Size { get; set; }
        public List<DashboardElement> Elements { get; set; }

        /// <summary>
        /// Optional, stable id of the card (Core 2.2.0). The UI assigns it; the orchestrator gives a card without
        /// one a new id when it loads or saves the dashboard and never changes an existing one. Omitted when <c>null</c>.
        /// </summary>
        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public string? Id { get; set; }

        /// <summary>
        /// Optional name of the UI card kind (Core 2.2.0), an open set: for example <c>button</c>, <c>gauge</c>,
        /// <c>sensor</c>, <c>light</c>, <c>entities</c>, <c>markdown</c> or <c>motion</c>. A new kind needs no Core
        /// change. When <c>null</c>, readers use <see cref="Type"/>. Omitted when <c>null</c>.
        /// </summary>
        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public string? Kind { get; set; }

        /// <summary>
        /// Optional card-level settings (Core 2.2.0), defined by the card <see cref="Kind"/>; the orchestrator stores
        /// them as they are. Element-level settings stay in <see cref="DashboardElement.Properties"/>. Omitted when
        /// <c>null</c>.
        /// </summary>
        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public ExpandoObject? Settings { get; set; }
    }
}
