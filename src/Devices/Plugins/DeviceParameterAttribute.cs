using System;

namespace RIoT2.Core.Plugins
{
    /// <summary>
    /// Marks a property of a device options class (<see cref="Abstracts.DeviceBase{TOptions}"/>) as the device
    /// parameter <see cref="Key"/>. The base class binds and validates it, and the node describes it in the
    /// configuration template's <c>parameterDefinitions</c>.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public sealed class DeviceParameterAttribute : Attribute
    {
        public DeviceParameterAttribute(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("A device parameter needs a key.", nameof(key));
            Key = key;
        }

        /// <summary>The key in <c>deviceParameters</c>. Keep the spelling stored configurations use.</summary>
        public string Key { get; }

        /// <summary>The device goes to the error state when the parameter is missing or empty and has no default.</summary>
        public bool Required { get; set; }

        /// <summary>A password, key or token: the UI masks it.</summary>
        public bool Secret { get; set; }

        /// <summary>
        /// The value used when the parameter is missing or empty, in invariant format. When not set, the property's
        /// initial value is the default.
        /// </summary>
        public string Default { get; set; }

        /// <summary>Help text shown in the UI.</summary>
        public string Description { get; set; }
    }
}
