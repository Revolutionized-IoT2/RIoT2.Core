namespace RIoT2.Core.Models
{
    public class NodeOnlineMessage
    {
        public NodeOnlineMessage() 
        {
            IsOnline = true;
        }
        public string Name { get; set; }
        public bool IsOnline { get; set; }
        public string ConfigurationTemplateUrl { get; set; }
        public string DeviceStateUrl { get; set; }
    }
}
