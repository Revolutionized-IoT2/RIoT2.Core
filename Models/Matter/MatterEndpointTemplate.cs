using System.Collections.Generic;

namespace RIoT2.Core.Models.Matter
{
    /// <summary>
    /// One Matter endpoint a RIoT device asks the RIoT Control Bridge to expose. A device declares these
    /// by implementing <see cref="Interfaces.IMatterDevice"/>; the bridge composes a bridged
    /// (0x0013 + <see cref="DeviceType"/>) endpoint per template and keeps it in sync with the device.
    /// </summary>
    /// <remarks>
    /// A device may declare several endpoints — a Hue bridge plugin, for example, returns one per lamp.
    /// </remarks>
    public class MatterEndpointTemplate
    {
        public MatterEndpointTemplate()
        {
            Attributes = new List<MatterAttributeBinding>();
            Commands = new List<MatterCommandBinding>();
        }

        /// <summary>
        /// A stable identifier for this endpoint, unique within the owning device. It is used as the
        /// bridged device's Matter UniqueId and as the key of the orchestrator's persisted endpoint map,
        /// so it must stay the same across restarts and configuration re-imports — otherwise a
        /// controller sees the endpoint disappear and a new one take its place. Derive it from something
        /// durable about the underlying device (a serial number, a hub-assigned id), not from a fresh
        /// <c>Guid</c>.
        /// </summary>
        public string Id { get; set; }

        /// <summary>The name shown to the user in the controller (e.g. "Living Room Lamp").</summary>
        public string Name { get; set; }

        /// <summary>The Matter device type to expose, which determines the composed cluster set.</summary>
        public MatterDeviceType DeviceType { get; set; }

        /// <summary>The vendor name reported in Bridged Device Basic Information. Optional.</summary>
        public string VendorName { get; set; }

        /// <summary>The product name reported in Bridged Device Basic Information. Optional.</summary>
        public string ProductName { get; set; }

        /// <summary>The RIoT-to-Matter bindings: which reports drive which cluster attributes.</summary>
        public List<MatterAttributeBinding> Attributes { get; set; }

        /// <summary>The Matter-to-RIoT bindings: which cluster attribute changes send which commands.</summary>
        public List<MatterCommandBinding> Commands { get; set; }
    }
}
