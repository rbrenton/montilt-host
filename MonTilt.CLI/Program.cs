using System;
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
    }
}