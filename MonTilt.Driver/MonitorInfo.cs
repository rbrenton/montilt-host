using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace MonTilt.Driver
{
    /// <summary>
    /// Information about a monitor
    /// </summary>
    public class MonitorInfo
    {
        public int Index { get; set; }
        public string DeviceName { get; set; }
        public bool IsPrimary { get; set; }
        public Orientation CurrentOrientation { get; set; }
        public System.Drawing.Rectangle Bounds { get; set; }
        
        public override string ToString()
        {
            string orientationText = CurrentOrientation switch
            {
                Orientation.Landscape => "Landscape (0°)",
                Orientation.Portrait => "Portrait (90°)",
                Orientation.LandscapeFlipped => "Landscape Flipped (180°)",
                Orientation.PortraitFlipped => "Portrait Flipped (270°)",
                _ => $"Unknown ({(int)CurrentOrientation}°)"
            };
            
            string primaryText = IsPrimary ? " (Primary)" : "";
            
            return $"Monitor {Index + 1}: {DeviceName}{primaryText} - {orientationText} - {Bounds.Width}x{Bounds.Height}";
        }
    }
}