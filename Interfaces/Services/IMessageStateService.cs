using RIoT2.Core.Models;
using System.Collections.Generic;

namespace RIoT2.Core.Interfaces.Services
{
    /// <summary>
    /// This service maintain the current state of each IO
    /// Maintains history of the suitable IO's
    /// </summary>
    public interface IMessageStateService
    {
        void SetState(Report report, bool maintainHistory = false);
        void SetState(Command command);

        IEnumerable<Command> Commands { get; }
        IEnumerable<Report> Reports { get; }

        IEnumerable<Report> GetHistory(string reportId, int? count = null);

        void Reset(bool includeState = false);
    }
}