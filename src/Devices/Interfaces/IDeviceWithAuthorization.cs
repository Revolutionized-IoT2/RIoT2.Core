using RIoT2.Core.Models;
using System.Threading;
using System.Threading.Tasks;

namespace RIoT2.Core.Interfaces
{
    /// <summary>
    /// Implemented by a device that signs in to a cloud account with an OAuth 2.0 authorization code (Core 2.4.0), so
    /// the UI can connect it instead of the user copying tokens. The orchestrator creates the <c>state</c>, opens the
    /// provider's page through the UI and receives the redirect; the node passes the code to the device, which
    /// exchanges it with its own secret and keeps the tokens. Hosts call these members through
    /// <see cref="Plugins.DeviceCalls"/>.
    /// </summary>
    public interface IDeviceWithAuthorization : IDevice
    {
        /// <summary>What the UI shows for the device: the provider and the one-time setup. No I/O.</summary>
        DeviceAuthorization Authorization { get; }

        /// <summary>
        /// Whether the device is connected, from what it knows: never calls the provider, and works before the
        /// device is initialized.
        /// </summary>
        DeviceAuthorizationStatus GetAuthorizationStatus();

        /// <summary>
        /// The provider's sign-in page for this device's account, which redirects to <paramref name="redirectUri"/>
        /// with <paramref name="state"/>. Throws <see cref="DeviceNotReadyException"/> when the device's
        /// configuration lacks what the sign-in needs.
        /// </summary>
        Task<string> BeginAuthorizationAsync(string redirectUri, string state, CancellationToken cancellationToken);

        /// <summary>
        /// Exchanges the provider's <paramref name="code"/> for credentials, keeps them, and starts using them.
        /// <paramref name="redirectUri"/> is the one given to <see cref="BeginAuthorizationAsync"/>.
        /// </summary>
        Task CompleteAuthorizationAsync(string code, string redirectUri, CancellationToken cancellationToken);
    }
}
