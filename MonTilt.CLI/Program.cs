using System;
using System.Diagnostics;
using System.Security.Principal;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using MonTilt.Core;
using MonTilt.Driver;

namespace MonTilt.CLI
{
    /// <summary>
    /// Main program entry point
    /// </summary>
    class Program
    {
        static void Main(string[] args)
        {
            // Check if running as administrator
            if (!IsRunningAsAdministrator())
            {
                // Restart program as administrator
                RestartAsAdministrator();
                return;
            }

            // Print header
            Console.WriteLine("MonTilt CLI v1.0");
            Console.WriteLine("Automatic monitor orientation manager");
            Console.WriteLine();
            
            // Check if running on Windows
            if (!System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
            {
                Console.WriteLine("Error: This application is only supported on Windows.");
                return;
            }
            
            try
            {
                // Create command handler
                CommandHandler handler = new CommandHandler();
                
                // Start device discovery
                handler.Start();
                
                // Display help
                handler.HandleCommand("help");
                
                // Command loop
                bool running = true;
                
                while (running)
                {
                    Console.Write("> ");
                    string command = Console.ReadLine()?.Trim() ?? "";
                    
                    if (command.ToLower() == "exit")
                    {
                        running = false;
                    }
                    else if (!string.IsNullOrEmpty(command))
                    {
                        handler.HandleCommand(command);
                    }
                }
                
                // Stop device discovery and save config
                handler.HandleCommand("save");
                handler.Stop();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Fatal error: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
        }

        /// <summary>
        /// Checks if the application is running with administrator privileges
        /// </summary>
        /// <returns>True if running as administrator, false otherwise</returns>
        private static bool IsRunningAsAdministrator()
        {
            try
            {
                using var identity = WindowsIdentity.GetCurrent();
                var principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Restarts the application with administrator privileges
        /// </summary>
        private static void RestartAsAdministrator()
        {
            try
            {
                // Use AppContext.BaseDirectory instead of Assembly.Location
                string applicationPath = AppContext.BaseDirectory;
                string executablePath = Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty;
                    
                if (string.IsNullOrEmpty(executablePath))
                {
                    Console.WriteLine("Error: Could not determine executable path.");
                    Console.WriteLine("Please run the application as administrator manually.");
                    Console.WriteLine("Press any key to exit...");
                    Console.ReadKey();
                    return;
                }

                if (executablePath.EndsWith("dotnet.exe") || executablePath.EndsWith("dotnet"))
                {
                    // Get the entry assembly name
                    string? assemblyName = System.Reflection.Assembly.GetEntryAssembly()?.GetName()?.Name;
                    if (string.IsNullOrEmpty(assemblyName))
                    {
                        Console.WriteLine("Error: Could not determine assembly name.");
                        Console.WriteLine("Please run the application as administrator manually.");
                        Console.WriteLine("Press any key to exit...");
                        Console.ReadKey();
                        return;
                    }

                    string dllPath = Path.Combine(applicationPath, $"{assemblyName}.dll");
                        
                    var psi = new ProcessStartInfo
                    {
                        FileName = executablePath,
                        Arguments = $"exec \"{dllPath}\"",
                        UseShellExecute = true,
                        Verb = "runas" // This is what prompts for UAC elevation
                    };
                        
                    try
                    {
                        Process.Start(psi);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Error restarting as administrator: " + ex.Message);
                        Console.WriteLine("Please run the application as administrator manually.");
                        Console.WriteLine("Press any key to exit...");
                        Console.ReadKey();
                    }
                    return;
                }

                // For compiled executable, this is simpler
                var processInfo = new ProcessStartInfo
                {
                    FileName = executablePath,
                    UseShellExecute = true,
                    Verb = "runas" // This is what prompts for UAC elevation
                };
                    
                try
                {
                    Process.Start(processInfo);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error restarting as administrator: " + ex.Message);
                    Console.WriteLine("Please run the application as administrator manually.");
                    Console.WriteLine("Press any key to exit...");
                    Console.ReadKey();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error attempting to restart with admin rights: {ex.Message}");
                Console.WriteLine("Please run the application as administrator manually.");
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey();
            }
        }
    }
}