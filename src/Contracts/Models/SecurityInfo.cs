using System.Collections.Generic;

namespace RIoT2.Core.Models
{
    /// <summary>
    /// The deployment's security state, from the orchestrator's anonymous <c>GET /api/security/info</c>
    /// (design 7.5). The UI, the Mobile app and the firmware read it to adapt without configuration of
    /// their own.
    /// </summary>
    public class SecurityInfo
    {
        /// <summary>The value of <see cref="Realtime"/> while the UI receives live state over MQTT.</summary>
        public const string RealtimeMqtt = "mqtt";

        /// <summary>The value of <see cref="Realtime"/> when the UI uses the orchestrator's <c>/realtime</c> hub.</summary>
        public const string RealtimeGateway = "gateway";

        /// <summary><c>off</c>, <c>audit</c> or <c>on</c> (<c>RIOT2_SECURITY_MODE</c>).</summary>
        public string Mode { get; set; } = "off";

        /// <summary>The features enforced (<c>RIOT2_SECURITY_FEATURES</c>); empty while the mode is <c>off</c>.</summary>
        public List<string> Features { get; set; } = new List<string>();

        /// <summary>Whether the UI must show its login page before calling the API.</summary>
        public bool LoginRequired { get; set; }

        /// <summary>How the UI gets live state: <see cref="RealtimeMqtt"/> or <see cref="RealtimeGateway"/>.</summary>
        public string Realtime { get; set; } = RealtimeMqtt;
    }
}
