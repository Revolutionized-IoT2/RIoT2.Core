using System.Collections.Generic;

namespace RIoT2.Core.Models
{
    public class DashboardPage
    {
        public string Name { get; set; }
        public string Icon { get; set; }
        public List<DashboardComponent> Components { get; set; }

        /// <summary>
        /// Optional, stable id of the page (Core 2.2.0). The UI assigns it; the orchestrator gives a page without
        /// one a new id when it loads or saves the dashboard and never changes an existing one. Omitted when <c>null</c>.
        /// </summary>
        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public string? Id { get; set; }
    }
}
