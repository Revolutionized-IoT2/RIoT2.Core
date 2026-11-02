using RIoT2.Core.Models.Matter;
using System;
using System.Collections.Generic;

namespace RIoT2.Core.Models
{
    public class DeviceConfiguration
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string ClassFullName { get; set; }
        public List<CommandTemplate> CommandTemplates { get; set; }
        public List<ReportTemplate> ReportTemplates { get; set; }
        public Dictionary<string, string> DeviceParameters { get; set; }
        public string RefreshSchedule { get; set; }

        /// <summary>
        /// The Matter endpoints this device is exposed as through the RIoT Control Bridge, declared by
        /// devices that implement <see cref="Interfaces.IMatterDevice"/>. <c>null</c> or empty for devices
        /// that are not bridged to Matter.
        /// </summary>
        public List<MatterEndpointTemplate> MatterEndpoints { get; set; }

        /// <summary>
        /// Optional lowercase hex SHA-256 of this entry as served (design 7.2), so a node can tell which
        /// devices changed. Omitted when <c>null</c>.
        /// </summary>
        [Newtonsoft.Json.JsonProperty(NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public string Hash { get; set; }

        /// <summary>
        /// Optional, templates only (Core 1.16.0, plan M12 step 4): the parameters the device reads, with their
        /// type, whether they are required or secret, a default and a description. The node fills it for
        /// devices with typed options; the orchestrator doesn't store it. Omitted when <c>null</c>.
        /// </summary>
        [Newtonsoft.Json.JsonProperty(NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public List<DeviceParameterDefinition> ParameterDefinitions { get; set; }

        /// <summary>
        /// Builds a scheduler trigger from the configured refresh schedule.
        /// </summary>
        /// <returns>
        /// A <see cref="SchedulerTrigger"/> when <c>RefreshSchedule</c> is set; otherwise, <c>null</c>.
        /// Since 1.13.0 the cron expression is not validated here (the contract package has no Quartz):
        /// the node's scheduler skips an invalid one.
        /// </returns>
        [Obsolete("The node's scheduler builds the trigger and validates the cron expression; removed in Core 2.0 (I45).")]
        public SchedulerTrigger GetDeviceRefreshSchedule()
        {
            if (String.IsNullOrWhiteSpace(RefreshSchedule))
                return null;

            return new SchedulerTrigger()
            {
                CronSchedule = RefreshSchedule,
                Group = Id,
                Name = Id
            };
        }

    }
}
