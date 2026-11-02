using RIoT2.Core.Interfaces;
using RIoT2.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RIoT2.Core.Plugins
{
    /// <summary>
    /// Builds the configuration template the node returns from <c>GET /api/device/configuration/templates</c>.
    /// </summary>
    public static class DeviceTemplates
    {
        /// <summary>Refresh schedule of the default template for refreshable devices: every minute (Quartz cron).</summary>
        public const string DefaultRefreshSchedule = "0 * * ? * * *";

        /// <summary>
        /// The device's own template when it implements <see cref="IDeviceWithConfiguration"/>, with its Matter
        /// endpoints; otherwise a default template without parameters. For a device with typed options
        /// (<see cref="IDeviceWithOptions"/>) the template also gets <c>ParameterDefinitions</c>, and every
        /// parameter key it lacks, with the default value or an empty one.
        /// </summary>
        public static DeviceConfiguration Create(IDevice device)
        {
            var template = CreateTemplate(device);
            if (device is IDeviceWithOptions withOptions)
                AddParameterDefinitions(template, DeviceOptions.Describe(withOptions.OptionsType));
            return template;
        }

        private static void AddParameterDefinitions(DeviceConfiguration template, IReadOnlyList<DeviceParameterDefinition> definitions)
        {
            template.ParameterDefinitions = definitions.ToList();
            template.DeviceParameters = template.DeviceParameters ?? new Dictionary<string, string>();
            foreach (var definition in definitions)
            {
                if (DeviceOptions.Find(template.DeviceParameters, definition.Key) == null)
                    template.DeviceParameters[definition.Key] = definition.Default ?? "";
            }
        }

        private static DeviceConfiguration CreateTemplate(IDevice device)
        {
            if (device == null)
                throw new ArgumentNullException(nameof(device));

            if (device is IDeviceWithConfiguration configurable)
            {
                var template = configurable.GetConfigurationTemplate()
                    ?? throw new InvalidOperationException($"{device.GetType().FullName} returned no configuration template.");

                // Matter devices declare their endpoints against this same template instance, so the report and
                // command template ids they reference are the ones that get persisted.
                if (device is IMatterDevice matterDevice)
                    template.MatterEndpoints = matterDevice.GetMatterEndpoints(template)?.ToList();

                return template;
            }

            return new DeviceConfiguration
            {
                ClassFullName = device.GetType().FullName,
                Id = Guid.NewGuid().ToString(),
                Name = device.GetType().Name,
                RefreshSchedule = device is IRefreshableReportDevice ? DefaultRefreshSchedule : null,
                CommandTemplates = device is ICommandDevice ? new List<CommandTemplate>() : null,
                ReportTemplates = new List<ReportTemplate>(),
                DeviceParameters = new Dictionary<string, string>()
            };
        }
    }
}
