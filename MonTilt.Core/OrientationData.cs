using System.Text.Json.Serialization;

namespace MonTilt.Core
{
    /// <summary>
    /// Represents orientation data received from an ESP32 device
    /// </summary>
    public class OrientationData
    {
        /// <summary>
        /// Gets or sets the MAC address of the device
        /// </summary>
        [JsonPropertyName("mac")]
        public string Mac { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets the orientation value
        /// 0 = Landscape (0°)
        /// 1 = Portrait (90°)
        /// 2 = Landscape Flipped (180°)
        /// 3 = Portrait Flipped (270°)
        /// </summary>
        [JsonPropertyName("orientation")]
        public int Orientation { get; set; }
        
        /// <summary>
        /// Gets or sets the X-axis value
        /// </summary>
        [JsonPropertyName("x")]
        public float? X { get; set; }
        
        /// <summary>
        /// Gets or sets the Y-axis value
        /// </summary>
        [JsonPropertyName("y")]
        public float? Y { get; set; }
        
        /// <summary>
        /// Gets or sets the Z-axis value
        /// </summary>
        [JsonPropertyName("z")]
        public float? Z { get; set; }
    }
}