using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace MonTilt.Driver
{
    public class MonitorOrientationDriver
    {
        // P/Invoke declarations for Windows API
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int ChangeDisplaySettingsEx(string lpszDeviceName, ref DEVMODE lpDevMode, IntPtr hwnd, int dwflags, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern bool EnumDisplaySettings(string deviceName, int modeNum, ref DEVMODE devMode);

        // DEVMODE struct for display settings
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct DEVMODE
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string dmDeviceName;

            public short dmSpecVersion;
            public short dmDriverVersion;
            public short dmSize;
            public short dmDriverExtra;
            public int dmFields;
            public int dmPositionX;
            public int dmPositionY;
            public int dmDisplayOrientation;
            public int dmDisplayFixedOutput;
            public short dmColor;
            public short dmDuplex;
            public short dmYResolution;
            public short dmTTOption;
            public short dmCollate;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string dmFormName;

            public short dmLogPixels;
            public int dmBitsPerPel;
            public int dmPelsWidth;
            public int dmPelsHeight;
            public int dmDisplayFlags;
            public int dmDisplayFrequency;
            public int dmICMMethod;
            public int dmICMIntent;
            public int dmMediaType;
            public int dmDitherType;
            public int dmReserved1;
            public int dmReserved2;
            public int dmPanningWidth;
            public int dmPanningHeight;
        }

        // Display settings constants
        private const int ENUM_CURRENT_SETTINGS = -1;
        private const int CDS_UPDATEREGISTRY = 0x01;
        private const int CDS_TEST = 0x02;
        private const int CDS_FULLSCREEN = 0x04;
        private const int CDS_GLOBAL = 0x08;
        private const int CDS_SET_PRIMARY = 0x10;
        private const int CDS_NORESET = 0x10000000;
        
        private const int DM_DISPLAYORIENTATION = 0x00800000;
        
        // Display change result constants
        private const int DISP_CHANGE_SUCCESSFUL = 0;
        private const int DISP_CHANGE_RESTART = 1;
        private const int DISP_CHANGE_FAILED = -1;
        private const int DISP_CHANGE_BADMODE = -2;
        private const int DISP_CHANGE_NOTUPDATED = -3;
        private const int DISP_CHANGE_BADFLAGS = -4;
        private const int DISP_CHANGE_BADPARAM = -5;

        /// <summary>
        /// Get a list of all available monitor device names
        /// </summary>
        public List<string> GetAvailableMonitors()
        {
            List<string> monitors = new List<string>();
            
            // Use Screen.AllScreens to get all monitors
            Screen[] screens = Screen.AllScreens;
            
            foreach (Screen screen in screens)
            {
                monitors.Add(screen.DeviceName);
            }
            
            return monitors;
        }
        
        /// <summary>
        /// Get information about all available monitors
        /// </summary>
        public List<MonitorInfo> GetMonitorInfo()
        {
            List<MonitorInfo> monitors = new List<MonitorInfo>();
            
            Screen[] screens = Screen.AllScreens;
            
            for (int i = 0; i < screens.Length; i++)
            {
                var screen = screens[i];
                
                // Get current orientation
                DEVMODE dm = new DEVMODE();
                dm.dmSize = (short)Marshal.SizeOf(typeof(DEVMODE));
                
                if (EnumDisplaySettings(screen.DeviceName, ENUM_CURRENT_SETTINGS, ref dm))
                {
                    Orientation orientation = (Orientation)dm.dmDisplayOrientation;
                    
                    monitors.Add(new MonitorInfo
                    {
                        Index = i,
                        DeviceName = screen.DeviceName,
                        IsPrimary = screen.Primary,
                        CurrentOrientation = orientation,
                        Bounds = screen.Bounds
                    });
                }
            }
            
            return monitors;
        }
        
        /// <summary>
        /// Set the orientation of a specific monitor
        /// </summary>
        /// <param name="monitorDeviceName">The monitor device name (e.g., \\.\DISPLAY1)</param>
        /// <param name="orientation">The desired orientation</param>
        /// <returns>True if successful, false otherwise</returns>
        public bool SetMonitorOrientation(string monitorDeviceName, Orientation orientation)
        {
            // Get current settings
            DEVMODE dm = new DEVMODE();
            dm.dmSize = (short)Marshal.SizeOf(typeof(DEVMODE));
            
            if (!EnumDisplaySettings(monitorDeviceName, ENUM_CURRENT_SETTINGS, ref dm))
            {
                return false;
            }
            
            // Set the new orientation
            dm.dmDisplayOrientation = (int)orientation;
            
            // Update the fields flag to indicate we're changing orientation
            dm.dmFields = DM_DISPLAYORIENTATION;
            
            // Apply the new settings
            int result = ChangeDisplaySettingsEx(
                monitorDeviceName, 
                ref dm, 
                IntPtr.Zero, 
                CDS_UPDATEREGISTRY, 
                IntPtr.Zero
            );
            
            return result == DISP_CHANGE_SUCCESSFUL;
        }
        
        /// <summary>
        /// Get the current orientation of a specific monitor
        /// </summary>
        public Orientation GetMonitorOrientation(string monitorDeviceName)
        {
            DEVMODE dm = new DEVMODE();
            dm.dmSize = (short)Marshal.SizeOf(typeof(DEVMODE));
            
            if (EnumDisplaySettings(monitorDeviceName, ENUM_CURRENT_SETTINGS, ref dm))
            {
                return (Orientation)dm.dmDisplayOrientation;
            }
            
            // Default to landscape if we can't determine
            return Orientation.Landscape;
        }
    }
}