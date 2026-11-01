using RIoT2.Core.Models;
using System;
using System.Security.Cryptography;
using System.Text;

namespace RIoT2.Core.Utils
{
    /// <summary>
    /// The configuration hashes of design 7.2: lowercase hex SHA-256 of the configuration as the
    /// orchestrator serves it (<see cref="Json.Serialize"/>), with its own <c>revision</c> and
    /// <c>hash</c> left out. Nodes compare hashes, never parse them.
    /// </summary>
    public static class ConfigurationHash
    {
        /// <summary>
        /// Sets <see cref="DeviceConfiguration.Hash"/> on every device entry and returns the hash of the
        /// whole document, device hashes included. <see cref="NodeDeviceConfiguration.Revision"/> and
        /// <see cref="NodeDeviceConfiguration.Hash"/> are not changed. Not thread-safe: call it on a
        /// configuration nobody else reads yet.
        /// </summary>
        public static string Stamp(NodeDeviceConfiguration configuration)
        {
            if (configuration == null)
                throw new ArgumentNullException(nameof(configuration));
            foreach (var device in configuration.DeviceConfigurations ?? new System.Collections.Generic.List<DeviceConfiguration>())
            {
                if (device == null)
                    continue;
                device.Hash = null;
                device.Hash = Of(Json.Serialize(device));
            }
            var revision = configuration.Revision;
            var hash = configuration.Hash;
            try
            {
                configuration.Revision = null;
                configuration.Hash = null;
                return Of(Json.Serialize(configuration));
            }
            finally
            {
                configuration.Revision = revision;
                configuration.Hash = hash;
            }
        }

        /// <summary>Lowercase hex SHA-256 of the UTF-8 bytes of <paramref name="text"/>.</summary>
        public static string Of(string text)
        {
            using (var sha = SHA256.Create())
            {
                var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(text ?? ""));
                var hex = new StringBuilder(bytes.Length * 2);
                foreach (var b in bytes)
                    hex.Append(b.ToString("x2"));
                return hex.ToString();
            }
        }
    }
}
