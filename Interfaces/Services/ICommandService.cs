namespace RIoT2.Core.Interfaces.Services
{
    /// <summary>
    /// Provides execution of commands received in JSON form.
    /// </summary>
    public interface ICommandService
    {
        /// <summary>
        /// Deserializes and executes a command from its JSON representation.
        /// </summary>
        /// <param name="json">The JSON payload representing the command.</param>
        void ExecuteJsonCommand(string json);
    }
}