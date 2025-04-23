using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using MonTilt.Driver;

namespace MonTilt.Core
{
    /// <summary>
    /// Status of a mapping between a device and a monitor
    /// </summary>
    public class MappingStatus
    {
        public string DeviceMac { get; set; }
        public string MonitorDeviceName { get; set; }
        public bool IsConnected { get; set; }
        public DateTime LastUpdated { get; set; }
        public Orientation CurrentOrientation { get; set; }
    }
}