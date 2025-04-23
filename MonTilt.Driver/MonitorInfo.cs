namespace MonTilt.Driver
{
    /// <summary>
    /// Contains information about a monitor
    /// </summary>
    public class MonitorInfo
    {
        /// <summary>
        /// Gets the display name of the monitor
        /// </summary>
        public string DisplayName { get; }
        
        /// <summary>
        /// Gets the display device name of the monitor
        /// </summary>
        public string DeviceName { get; }
        
        /// <summary>
        /// Gets the monitor index
        /// </summary>
        public uint Index { get; }
        
        /// <summary>
        /// Gets the width of the monitor in pixels
        /// </summary>
        public int Width { get; }
        
        /// <summary>
        /// Gets the height of the monitor in pixels
        /// </summary>
        public int Height { get; }
        
        /// <summary>
        /// Gets the current orientation of the monitor
        /// </summary>
        public Orientation CurrentOrientation { get; set; }
        
        /// <summary>
        /// Gets the primary monitor flag
        /// </summary>
        public bool IsPrimary { get; }
        
        /// <summary>
        /// Constructor for MonitorInfo
        /// </summary>
        /// <param name="deviceName">The device name</param>
        /// <param name="displayName">The display name</param>
        /// <param name="index">The monitor index</param>
        /// <param name="width">The width in pixels</param>
        /// <param name="height">The height in pixels</param>
        /// <param name="orientation">The current orientation</param>
        /// <param name="isPrimary">Whether this is the primary monitor</param>
        public MonitorInfo(string deviceName, string displayName, uint index, int width, int height, Orientation orientation, bool isPrimary)
        {
            DeviceName = deviceName;
            DisplayName = displayName;
            Index = index;
            Width = width;
            Height = height;
            CurrentOrientation = orientation;
            IsPrimary = isPrimary;
        }
    }
}