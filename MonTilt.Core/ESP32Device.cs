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
    /// Represents a connected ESP32 device
    /// </summary>
    public class ESP32Device : IDisposable
    {
        // Constants
        private const int BAUD_RATE = 115200;
        private const int MAX_BUFFER_SIZE = 8192;
        
        // Properties
        public string ComPort { get; private set; }
        public string MacAddress { get; set; }
        public bool IsIdentified => !string.IsNullOrEmpty(MacAddress);
        public SerialPort Port { get; private set; }
        public Orientation CurrentOrientation { get; private set; }
        
        // Buffer for incoming data
        private StringBuilder _dataBuffer = new StringBuilder();
        
        // Events
        public event EventHandler<string> MacAddressReceived;
        public event EventHandler<OrientationData> OrientationChanged;
        
        /// <summary>
        /// Constructor
        /// </summary>
        public ESP32Device(string comPort)
        {
            ComPort = comPort;
            MacAddress = string.Empty;
            
            // Create and configure the serial port
            Port = new SerialPort(comPort, BAUD_RATE)
            {
                Encoding = Encoding.UTF8,
                ReadBufferSize = 4096,
                WriteBufferSize = 4096
            };
            
            // Set up the data received event handler
            Port.DataReceived += Port_DataReceived;
        }
        
        /// <summary>
        /// Open the connection to the device
        /// </summary>
        public bool Open()
        {
            try
            {
                if (!Port.IsOpen)
                {
                    Port.Open();
                }
                return true;
            }
            catch
            {
                return false;
            }
        }
        
        /// <summary>
        /// Close the connection to the device
        /// </summary>
        public void Close()
        {
            if (Port != null)
            {
                // Unsubscribe event handler to prevent issues during closing
                Port.DataReceived -= Port_DataReceived;
                
                if (Port.IsOpen)
                {
                    try
                    {
                        Port.Close();
                    }
                    catch (IOException)
                    {
                        // Ignore IO exceptions during close
                    }
                }
                
                Port.Dispose();
            }
        }
        
        /// <summary>
        /// Dispose the device
        /// </summary>
        public void Dispose()
        {
            Close();
        }
        
        /// <summary>
        /// Handle data received from the device
        /// </summary>
        private void Port_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            if (sender is SerialPort sp && sp.IsOpen)
            {
                try
                {
                    // Read all available data
                    int bytesToRead = sp.BytesToRead;
                    if (bytesToRead > 0)
                    {
                        byte[] buffer = new byte[bytesToRead];
                        int bytesRead = sp.Read(buffer, 0, bytesToRead);
                        string receivedData = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                        
                        // Append to the internal buffer
                        _dataBuffer.Append(receivedData);
                        
                        // Prevent buffer overflow
                        if (_dataBuffer.Length > MAX_BUFFER_SIZE)
                        {
                            _dataBuffer.Clear();
                        }
                        
                        // Process the buffer
                        ProcessBuffer();
                    }
                }
                catch (Exception)
                {
                    // Handle errors
                    Close();
                }
            }
        }
        
        /// <summary>
        /// Process the buffer for complete JSON messages
        /// </summary>
        private void ProcessBuffer()
        {
            string bufferContent = _dataBuffer.ToString();
            int messageStartIndex = 0;
            
            while (messageStartIndex < bufferContent.Length)
            {
                // Skip leading whitespace
                while (messageStartIndex < bufferContent.Length && char.IsWhiteSpace(bufferContent[messageStartIndex]))
                {
                    messageStartIndex++;
                }
                
                if (messageStartIndex >= bufferContent.Length) break;
                
                // Look for JSON object
                if (bufferContent[messageStartIndex] == '{')
                {
                    // Find the end of the JSON object
                    int braceBalance = 0;
                    int searchIndex = messageStartIndex;
                    
                    while (searchIndex < bufferContent.Length)
                    {
                        if (bufferContent[searchIndex] == '{') braceBalance++;
                        else if (bufferContent[searchIndex] == '}') braceBalance--;
                        
                        if (braceBalance == 0 && bufferContent[searchIndex] == '}')
                        {
                            // Found a complete JSON object
                            int messageEndIndex = searchIndex;
                            string completeMessage = bufferContent.Substring(messageStartIndex, messageEndIndex - messageStartIndex + 1);
                            
                            // Process the message
                            ProcessMessage(completeMessage);
                            
                            // Remove processed message from the buffer
                            _dataBuffer.Remove(0, messageEndIndex + 1);
                            bufferContent = _dataBuffer.ToString();
                            messageStartIndex = 0;
                            break;
                        }
                        
                        searchIndex++;
                    }
                    
                    // If no complete JSON object was found, wait for more data
                    if (braceBalance != 0) break;
                }
                else
                {
                    // Skip non-JSON characters
                    messageStartIndex++;
                }
            }
            
            // Trim leading whitespace from the buffer
            if (_dataBuffer.Length > 0 && char.IsWhiteSpace(_dataBuffer[0]))
            {
                string trimmed = _dataBuffer.ToString().TrimStart();
                _dataBuffer.Clear().Append(trimmed);
            }
        }
        
        /// <summary>
        /// Process a complete JSON message
        /// </summary>
        private void ProcessMessage(string message)
        {
            try
            {
                // Parse the JSON message
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };
                
                OrientationData data = JsonSerializer.Deserialize<OrientationData>(message, options);
                
                if (data != null && !string.IsNullOrEmpty(data.Mac))
                {
                    // If the device wasn't identified yet, raise the MacAddressReceived event
                    if (!IsIdentified)
                    {
                        MacAddress = data.Mac;
                        MacAddressReceived?.Invoke(this, data.Mac);
                    }
                    
                    // If the orientation changed, raise the OrientationChanged event
                    if ((int)CurrentOrientation != data.Orientation)
                    {
                        CurrentOrientation = (Orientation)data.Orientation;
                        OrientationChanged?.Invoke(this, data);
                    }
                }
            }
            catch (JsonException)
            {
                // Ignore invalid JSON
            }
        }
    }
}