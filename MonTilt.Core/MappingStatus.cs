namespace MonTilt.Core
{
    /// <summary>
    /// Represents the mapping status of a device to a monitor
    /// </summary>
    public class MappingStatus
    {
        /// <summary>
        /// Gets the MAC address of the device
        /// </summary>
        public string MacAddress { get; }
        
        /// <summary>
        /// Gets the monitor index
        /// </summary>
        public int MonitorIndex { get; }
        
        /// <summary>
        /// Gets whether the mapping is valid
        /// </summary>
        public bool IsValid { get; }
        
        /// <summary>
        /// Constructor for MappingStatus
        /// </summary>
        /// <param name="macAddress">The MAC address of the device</param>
        /// <param name="monitorIndex">The monitor index</param>
        /// <param name="isValid">Whether the mapping is valid</param>
        public MappingStatus(string macAddress, int monitorIndex, bool isValid)
        {
            MacAddress = macAddress;
            MonitorIndex = monitorIndex;
            IsValid = isValid;
        }
    }
}