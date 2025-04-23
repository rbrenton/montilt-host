using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace MonTilt.Driver
{
    /// <summary>
    /// Driver for controlling monitor orientation on Windows
    /// </summary>
    public class MonitorOrientationDriver
    {
        #region Win32 API Declarations

        internal static class NativeMethods
        {
            [DllImport("user32.dll")]
            internal static extern DISP_CHANGE ChangeDisplaySettingsEx(
                string lpszDeviceName, ref DEVMODE lpDevMode, IntPtr hwnd,
                DisplaySettingsFlags dwflags, IntPtr lParam);

            [DllImport("user32.dll")]
            internal static extern bool EnumDisplayDevices(
                string lpDevice, uint iDevNum, ref DISPLAY_DEVICE lpDisplayDevice,
                uint dwFlags);

            [DllImport("user32.dll", CharSet = CharSet.Ansi)]
            internal static extern int EnumDisplaySettings(
                string lpszDeviceName, int iModeNum, ref DEVMODE lpDevMode);

            public const int DMDO_DEFAULT = 0;
            public const int DMDO_90 = 1;
            public const int DMDO_180 = 2;
            public const int DMDO_270 = 3;

            public const int ENUM_CURRENT_SETTINGS = -1;
        }

        [StructLayout(LayoutKind.Explicit, CharSet = CharSet.Ansi)]
        internal struct DEVMODE
        {
            public const int CCHDEVICENAME = 32;
            public const int CCHFORMNAME = 32;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = CCHDEVICENAME)]
            [System.Runtime.InteropServices.FieldOffset(0)]
            public string dmDeviceName;
            [System.Runtime.InteropServices.FieldOffset(32)]
            public Int16 dmSpecVersion;
            [System.Runtime.InteropServices.FieldOffset(34)]
            public Int16 dmDriverVersion;
            [System.Runtime.InteropServices.FieldOffset(36)]
            public Int16 dmSize;
            [System.Runtime.InteropServices.FieldOffset(38)]
            public Int16 dmDriverExtra;
            [System.Runtime.InteropServices.FieldOffset(40)]
            public DM dmFields;

            [System.Runtime.InteropServices.FieldOffset(44)]
            Int16 dmOrientation;
            [System.Runtime.InteropServices.FieldOffset(46)]
            Int16 dmPaperSize;
            [System.Runtime.InteropServices.FieldOffset(48)]
            Int16 dmPaperLength;
            [System.Runtime.InteropServices.FieldOffset(50)]
            Int16 dmPaperWidth;
            [System.Runtime.InteropServices.FieldOffset(52)]
            Int16 dmScale;
            [System.Runtime.InteropServices.FieldOffset(54)]
            Int16 dmCopies;
            [System.Runtime.InteropServices.FieldOffset(56)]
            Int16 dmDefaultSource;
            [System.Runtime.InteropServices.FieldOffset(58)]
            Int16 dmPrintQuality;

            [System.Runtime.InteropServices.FieldOffset(44)]
            public POINTL dmPosition;
            [System.Runtime.InteropServices.FieldOffset(52)]
            public Int32 dmDisplayOrientation;
            [System.Runtime.InteropServices.FieldOffset(56)]
            public Int32 dmDisplayFixedOutput;

            [System.Runtime.InteropServices.FieldOffset(60)]
            public short dmColor;
            [System.Runtime.InteropServices.FieldOffset(62)]
            public short dmDuplex;
            [System.Runtime.InteropServices.FieldOffset(64)]
            public short dmYResolution;
            [System.Runtime.InteropServices.FieldOffset(66)]
            public short dmTTOption;
            [System.Runtime.InteropServices.FieldOffset(68)]
            public short dmCollate;
            [System.Runtime.InteropServices.FieldOffset(72)]
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = CCHFORMNAME)]
            public string dmFormName;
            [System.Runtime.InteropServices.FieldOffset(102)]
            public Int16 dmLogPixels;
            [System.Runtime.InteropServices.FieldOffset(104)]
            public Int32 dmBitsPerPel;
            [System.Runtime.InteropServices.FieldOffset(108)]
            public Int32 dmPelsWidth;
            [System.Runtime.InteropServices.FieldOffset(112)]
            public Int32 dmPelsHeight;
            [System.Runtime.InteropServices.FieldOffset(116)]
            public Int32 dmDisplayFlags;
            [System.Runtime.InteropServices.FieldOffset(116)]
            public Int32 dmNup;
            [System.Runtime.InteropServices.FieldOffset(120)]
            public Int32 dmDisplayFrequency;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        internal struct DISPLAY_DEVICE
        {
            [MarshalAs(UnmanagedType.U4)]
            public int cb;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string DeviceName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string DeviceString;
            [MarshalAs(UnmanagedType.U4)]
            public DisplayDeviceStateFlags StateFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string DeviceID;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string DeviceKey;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct POINTL
        {
            long x;
            long y;
        }

        internal enum DISP_CHANGE : int
        {
            Successful = 0,
            Restart = 1,
            Failed = -1,
            BadMode = -2,
            NotUpdated = -3,
            BadFlags = -4,
            BadParam = -5,
            BadDualView = -6
        }

        [Flags()]
        internal enum DisplayDeviceStateFlags : int
        {
            AttachedToDesktop = 0x1,
            MultiDriver = 0x2,

            PrimaryDevice = 0x4,

            MirroringDriver = 0x8,

            VGACompatible = 0x16,

            Removable = 0x20,

            ModesPruned = 0x8000000,
            Remote = 0x4000000,
            Disconnect = 0x2000000
        }

        [Flags()]
        internal enum DisplaySettingsFlags : int
        {
            CDS_UPDATEREGISTRY = 1,
            CDS_TEST = 2,
            CDS_FULLSCREEN = 4,
            CDS_GLOBAL = 8,
            CDS_SET_PRIMARY = 0x10,
            CDS_RESET = 0x40000000,
            CDS_NORESET = 0x10000000
        }

        [Flags()]
        internal enum DM : int
        {
            Orientation = 0x1,
            PaperSize = 0x2,
            PaperLength = 0x4,
            PaperWidth = 0x8,
            Scale = 0x10,
            Position = 0x20,
            NUP = 0x40,
            DisplayOrientation = 0x80,
            Copies = 0x100,
            DefaultSource = 0x200,
            PrintQuality = 0x400,
            Color = 0x800,
            Duplex = 0x1000,
            YResolution = 0x2000,
            TTOption = 0x4000,
            Collate = 0x8000,
            FormName = 0x10000,
            LogPixels = 0x20000,
            BitsPerPixel = 0x40000,
            PelsWidth = 0x80000,
            PelsHeight = 0x100000,
            DisplayFlags = 0x200000,
            DisplayFrequency = 0x400000,
            ICMMethod = 0x800000,
            ICMIntent = 0x1000000,
            MediaType = 0x2000000,
            DitherType = 0x4000000,
            PanningWidth = 0x8000000,
            PanningHeight = 0x10000000,
            DisplayFixedOutput = 0x20000000
        }
        #endregion
        
        // List of monitors
        private List<MonitorInfo> _monitors = new List<MonitorInfo>();
        
        /// <summary>
        /// Event raised when a monitor's orientation changes
        /// </summary>
        public event EventHandler<MonitorOrientationChangedEventArgs>? OrientationChanged;
        
        /// <summary>
        /// Initializes the monitor driver
        /// </summary>
        public void Initialize()
        {
            if (!IsRunningAsAdministrator())
            {
                Console.WriteLine("WARNING: MonTilt is not running with administrator privileges.");
                Console.WriteLine("Display settings changes may fail. Consider running as administrator.");
            }
            
            RefreshMonitorList();
        }
        
        /// <summary>
        /// Gets the list of available monitors
        /// </summary>
        /// <returns>List of available monitors</returns>
        public List<MonitorInfo> GetMonitors()
        {
            if (_monitors.Count == 0)
            {
                RefreshMonitorList();
            }
            
            return _monitors;
        }
        
        /// <summary>
        /// Sets the orientation of a monitor
        /// </summary>
        /// <param name="monitorIndex">The monitor index</param>
        /// <param name="orientation">The new orientation</param>
        /// <returns>True if successful, false otherwise</returns>
        public bool SetMonitorOrientation(uint monitorIndex, Orientation orientation)
        {
            // Refresh monitor list if needed
            if (_monitors.Count == 0)
            {
                RefreshMonitorList();
            }
            
            // Validate monitor index
            if (monitorIndex < 0 || monitorIndex >= _monitors.Count)
            {
                Console.WriteLine($"Invalid monitor index: {monitorIndex}");
                return false;
            }
            
            // Get the monitor
            MonitorInfo monitor = _monitors[(int) monitorIndex];
            
            // Skip if orientation is already set
            if (monitor.CurrentOrientation == orientation)
            {
                return true;
            }
            
            // Get the device name
            string deviceName = monitor.DeviceName;
            
            // Map orientation to Windows constant
            int winOrientation = MapOrientationToWindows(orientation);
            
            // Apply the orientation change
            bool result = RotateScreen(monitor, winOrientation);
            
            if (result)
            {
                // Update monitor orientation
                monitor.CurrentOrientation = orientation;
                
                // Raise event
                OrientationChanged?.Invoke(this, new MonitorOrientationChangedEventArgs(
                    monitorIndex, orientation
                ));
                
                Console.WriteLine($"Monitor {monitorIndex + 1} rotated to {orientation}");
            }
            
            return result;
        }
        
        /// <summary>
        /// Refreshes the list of available monitors
        /// </summary>
        private void RefreshMonitorList()
        {
            // Clear existing list
            _monitors.Clear();
            
            uint deviceIndex = 0;
            DISPLAY_DEVICE displayDevice = new DISPLAY_DEVICE();
            displayDevice.cb = Marshal.SizeOf(displayDevice);

            // Enumerate all display devices
            while (NativeMethods.EnumDisplayDevices(null, deviceIndex, ref displayDevice, 0))
            {
                if (((int) displayDevice.StateFlags & 0x1) != 0) // Only active devices
                {
                    DEVMODE devMode = new DEVMODE();
                    devMode.dmSize = (short)Marshal.SizeOf(devMode);

                    if (NativeMethods.EnumDisplaySettings(displayDevice.DeviceName, NativeMethods.ENUM_CURRENT_SETTINGS, ref devMode) > 0)
                    {
                        // Determine orientation
                        Orientation orientation = MapWindowsToOrientation((uint) devMode.dmDisplayOrientation);
                        
                        // Create monitor info
                        MonitorInfo info = new MonitorInfo(
                            displayDevice.DeviceName,
                            $"Display {deviceIndex + 1}",
                            deviceIndex,
                            (int)devMode.dmPelsWidth,
                            (int)devMode.dmPelsHeight,
                            orientation,
                            ((int) displayDevice.StateFlags & 0x4) != 0 // Primary display
                        );
                        
                        // Add to list
                        _monitors.Add(info);
                        
                        Console.WriteLine($"Found monitor: {info.DisplayName}, {info.Width}x{info.Height}, Orientation: {orientation}");
                    }
                }
                
                // Move to next device
                deviceIndex++;
                displayDevice.cb = Marshal.SizeOf(displayDevice);
            }
            
            Console.WriteLine($"Found {_monitors.Count} monitor(s)");
        }
        
        /// <summary>
        /// Rotates a screen to the specified orientation
        /// </summary>
        /// <param name="deviceName">The device name</param>
        /// <param name="orientation">The orientation value (0, 1, 2, 3)</param>
        /// <returns>True if successful, false otherwise</returns>
        private bool RotateScreen(MonitorInfo monitor, int orientation)
        {
            try
            {
                Console.WriteLine($"Attempting to rotate screen {monitor.DeviceName} to orientation {orientation}");
                
                DISPLAY_DEVICE d = new DISPLAY_DEVICE();
                DEVMODE dm = new DEVMODE();
                d.cb = Marshal.SizeOf(d);

                d.DeviceName = monitor.DeviceName;

                if (0 != NativeMethods.EnumDisplaySettings(
                    d.DeviceName, NativeMethods.ENUM_CURRENT_SETTINGS, ref dm))
                {
                    int origHeight = dm.dmPelsHeight;
                    int origWidth = dm.dmPelsWidth;

                    // Swap width and height if going between landscape and portrait
                    if ((dm.dmDisplayOrientation % 2 == 1) ^ (orientation % 2 == 1)) {
                        dm.dmPelsHeight = origWidth;
                        dm.dmPelsWidth = origHeight;
                    }

                    dm.dmDisplayOrientation = orientation;
                    DISP_CHANGE iRet = NativeMethods.ChangeDisplaySettingsEx(
                        d.DeviceName, ref dm, IntPtr.Zero,
                        DisplaySettingsFlags.CDS_UPDATEREGISTRY, IntPtr.Zero);
                    if (iRet == DISP_CHANGE.Successful)
                    {
                        Console.WriteLine($"Successfully rotated {monitor.DeviceName} to orientation {orientation}");
                        return true;
                    }
                    else if (iRet == DISP_CHANGE.Restart)
                    {
                        Console.WriteLine($"Need to restart system to apply rotation to {monitor.DeviceName}");
                        return false;
                    }
                    else
                    {
                        Console.WriteLine($"Failed to rotate {monitor.DeviceName}, error code: {iRet}");
                        return false;
                    }
                }

                return false;
            
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception rotating screen: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                return false;
            }
        }
        
        /// <summary>
        /// Maps Orientation enum to Windows orientation values
        /// </summary>
        /// <param name="orientation">The orientation enum value</param>
        /// <returns>The Windows orientation value</returns>
        private int MapOrientationToWindows(Orientation orientation)
        {
            return orientation switch
            {
                Orientation.Landscape => NativeMethods.DMDO_DEFAULT,
                Orientation.Portrait => NativeMethods.DMDO_90,
                Orientation.LandscapeFlipped => NativeMethods.DMDO_180,
                Orientation.PortraitFlipped => NativeMethods.DMDO_270,
                _ => NativeMethods.DMDO_DEFAULT
            };
        }
        
        /// <summary>
        /// Maps Windows orientation values to Orientation enum
        /// </summary>
        /// <param name="winOrientation">The Windows orientation value</param>
        /// <returns>The orientation enum value</returns>
        private Orientation MapWindowsToOrientation(uint winOrientation)
        {
            return winOrientation switch
            {
                NativeMethods.DMDO_DEFAULT => Orientation.Landscape,
                NativeMethods.DMDO_90 => Orientation.Portrait,
                NativeMethods.DMDO_180 => Orientation.LandscapeFlipped,
                NativeMethods.DMDO_270 => Orientation.PortraitFlipped,
                _ => Orientation.Landscape
            };
        }

        /// <summary>
        /// Checks if the application is running with administrator privileges
        /// </summary>
        /// <returns>True if running as administrator, false otherwise</returns>
        private bool IsRunningAsAdministrator()
        {
            try
            {
                using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
                var principal = new System.Security.Principal.WindowsPrincipal(identity);
                return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }
    }
    
    /// <summary>
    /// Event args for monitor orientation changed events
    /// </summary>
    public class MonitorOrientationChangedEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the monitor index
        /// </summary>
        public uint MonitorIndex { get; }
        
        /// <summary>
        /// Gets the new orientation
        /// </summary>
        public Orientation Orientation { get; }
        
        /// <summary>
        /// Constructor for MonitorOrientationChangedEventArgs
        /// </summary>
        /// <param name="monitorIndex">The monitor index</param>
        /// <param name="orientation">The new orientation</param>
        public MonitorOrientationChangedEventArgs(uint monitorIndex, Orientation orientation)
        {
            MonitorIndex = monitorIndex;
            Orientation = orientation;
        }
    }
}