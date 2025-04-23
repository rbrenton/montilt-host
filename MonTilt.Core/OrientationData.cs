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
    /// Data from the ESP32 device
    /// </summary>
    public class OrientationData
    {
        public string Mac { get; set; }
        public int Orientation { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
    }
}