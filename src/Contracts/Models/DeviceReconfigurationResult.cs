namespace RIoT2.Core.Models
{
    /// <summary>
    /// What <see cref="Abstracts.DeviceServiceBase.ReconfigureChangedDevicesAsync"/> did with each device.
    /// </summary>
    public class DeviceReconfigurationResult
    {
        /// <summary>Devices left running because their configuration did not change.</summary>
        public int Unchanged { get; set; }

        /// <summary>Devices (re)started with a new or changed configuration.</summary>
        public int Started { get; set; }

        /// <summary>Devices stopped because the configuration no longer contains them.</summary>
        public int Removed { get; set; }

        /// <summary>Devices that failed to stop, initialize or start. Each failure is logged.</summary>
        public int Failed { get; set; }

        /// <summary><c>true</c> when no device was stopped or started.</summary>
        public bool NothingChanged => Started == 0 && Removed == 0 && Failed == 0;

        /// <inheritdoc/>
        public override string ToString() =>
            $"{Started} started, {Removed} removed, {Failed} failed, {Unchanged} unchanged";
    }
}
