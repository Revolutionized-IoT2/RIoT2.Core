namespace RIoT2.Core.Models
{
    public class DeviceState
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Message { get; set; }
        public DeviceState State { get; set; }
    }
}