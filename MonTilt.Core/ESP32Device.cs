using System;
using System.IO;
using System.IO.Ports;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace MonTilt.Core
{
    /// <summary>
    /// Represents a connected ESP32 device
    /// </summary>
    public class ESP32Device
    {
        // Constants
        private const int BAUD_RATE = 115200;
        private const int MAX_BUFFER_SIZE = 8192;
        
        // Serial port
        private SerialPort _port;
        private readonly StringBuilder _dataBuffer = new StringBuilder();
        
        /// <summary>
        /// Gets the COM port the device is connected to
        /// </summary>
        public string ComPort { get; }
        
        /// <summary>
        /// Gets the MAC address of the device
        /// </summary>
        public string MacAddress { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets the current orientation of the device
        /// </summary>
        public int CurrentOrientation { get; private set; } = 0;
        
        /// <summary>
        /// Gets or sets the monitor index the device is mapped to
        /// </summary>
        public int MonitorIndex { get; set; } = -1;
        
        /// <summary>
        /// Gets whether the device is identified (has a MAC address)
        /// </summary>
        public bool IsIdentified { get; set; } = false;
        
        /// <summary>
        /// Gets whether the serial port is open
        /// </summary>
        public bool IsOpen => _port?.IsOpen ?? false;

        /// <summary>
        /// Event raised when a MAC address is received from the device
        /// </summary>
        public event EventHandler<string>? MacAddressReceived;
        
        /// <summary>
        /// Event raised when the device's orientation changes
        /// </summary>
        public event EventHandler<OrientationData>? OrientationChanged;
        
        /// <summary>
        /// Event raised when the device is disconnected
        /// </summary>
        public event EventHandler? Disconnected;

        /// <summary>
        /// Constructor for ESP32Device
        /// </summary>
        /// <param name="comPort">The COM port to connect to</param>
        public ESP32Device(string comPort)
        {
            ComPort = comPort;
            
            // Initialize serial port
            _port = new SerialPort(comPort, BAUD_RATE)
            {
                ReadTimeout = 1000,
                WriteTimeout = 1000,
                Encoding = Encoding.UTF8,
                ReadBufferSize = 4096,
                WriteBufferSize = 4096
            };
            
            // Set up data received handler
            _port.DataReceived += Port_DataReceived;
        }

        /// <summary>
        /// Opens the connection to the device
        /// </summary>
        public void Open()
        {
            if (!_port.IsOpen)
            {
                _port.Open();
            }
        }

        /// <summary>
        /// Closes the connection to the device
        /// </summary>
        public void Close()
        {
            if (_port != null)
            {
                try
                {
                    // Unsubscribe event handler
                    _port.DataReceived -= Port_DataReceived;
                    
                    // Close port if open
                    if (_port.IsOpen)
                    {
                        _port.Close();
                    }
                    
                    // Dispose of port
                    _port.Dispose();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error closing port {ComPort}: {ex.Message}");
                }
                
                // Raise disconnected event
                Disconnected?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// Writes a line to the device
        /// </summary>
        /// <param name="data">The data to write</param>
        public void WriteLineToDevice(string data)
        {
            if (_port.IsOpen)
            {
                _port.WriteLine(data);
            }
        }

        #region Private Methods
        
        /// <summary>
        /// Handles data received from the device
        /// </summary>
        private void Port_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            if (_port == null || !_port.IsOpen)
                return;
                
            try
            {
                // Read all available data
                int bytesToRead = _port.BytesToRead;
                if (bytesToRead > 0)
                {
                    byte[] buffer = new byte[bytesToRead];
                    int bytesRead = _port.Read(buffer, 0, bytesToRead);
                    string receivedData = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    
                    // Append to buffer
                    _dataBuffer.Append(receivedData);
                    
                    // Prevent buffer from growing too large
                    if (_dataBuffer.Length > MAX_BUFFER_SIZE)
                    {
                        Console.WriteLine($"Warning: Data buffer for {ComPort} exceeded max size. Clearing buffer.");
                        _dataBuffer.Clear();
                    }
                    
                    // Process complete messages
                    ProcessBuffer();
                }
            }
            catch (TimeoutException)
            {
                // Ignore timeouts
            }
            catch (IOException ex)
            {
                Console.WriteLine($"IO Error reading from {ComPort}: {ex.Message}. Closing port.");
                Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading from {ComPort}: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Processes the data buffer to extract complete messages
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
                
                if (messageStartIndex >= bufferContent.Length)
                    break;
                    
                string? completeMessage = null;
                int messageEndIndex = -1;
                
                // Look for JSON object
                if (bufferContent[messageStartIndex] == '{')
                {
                    int braceBalance = 0;
                    int searchIndex = messageStartIndex;
                    
                    while (searchIndex < bufferContent.Length)
                    {
                        if (bufferContent[searchIndex] == '{')
                            braceBalance++;
                        else if (bufferContent[searchIndex] == '}')
                            braceBalance--;
                            
                        if (braceBalance == 0 && bufferContent[searchIndex] == '}')
                        {
                            // Complete JSON found
                            messageEndIndex = searchIndex;
                            completeMessage = bufferContent.Substring(messageStartIndex, messageEndIndex - messageStartIndex + 1);
                            break;
                        }
                        
                        searchIndex++;
                    }
                }
                else
                {
                    // Non-JSON data, skip to the next potential JSON start
                    messageStartIndex++;
                    continue;
                }
                
                // Process the complete message
                if (completeMessage != null && messageEndIndex != -1)
                {
                    ProcessMessage(completeMessage);
                    
                    // Remove processed message from buffer
                    _dataBuffer.Remove(0, messageEndIndex + 1);
                    
                    // Update buffer content and reset start index
                    bufferContent = _dataBuffer.ToString();
                    messageStartIndex = 0;
                }
                else
                {
                    // Incomplete message, wait for more data
                    break;
                }
            }
            
            // Trim leading whitespace from buffer
            if (_dataBuffer.Length > 0 && char.IsWhiteSpace(_dataBuffer[0]))
            {
                string trimmed = _dataBuffer.ToString().TrimStart();
                _dataBuffer.Clear();
                _dataBuffer.Append(trimmed);
            }
        }
        
        /// <summary>
        /// Processes a complete message from the device
        /// </summary>
        /// <param name="message">The message to process</param>
        private void ProcessMessage(string message)
        {
            Console.WriteLine($"Processing message from {ComPort}: {message}");
            
            try
            {
                if (message.StartsWith("{") && message.EndsWith("}"))
                {
                    OrientationData? orientationData = JsonSerializer.Deserialize<OrientationData>(message);
                    
                    if (orientationData != null && !string.IsNullOrEmpty(orientationData.Mac))
                    {
                        // If not identified yet, update MAC address
                        if (!IsIdentified)
                        {
                            MacAddress = orientationData.Mac;
                            IsIdentified = true;
                            
                            Console.WriteLine($"Identified MAC {MacAddress} for {ComPort}");
                            MacAddressReceived?.Invoke(this, MacAddress);
                        }
                        else if (MacAddress != orientationData.Mac)
                        {
                            // MAC mismatch
                            Console.WriteLine($"Warning: Received MAC {orientationData.Mac} differs from identified MAC {MacAddress} on {ComPort}");
                        }
                        
                        // Check if orientation changed
                        if (orientationData.Orientation != CurrentOrientation)
                        {
                            CurrentOrientation = orientationData.Orientation;
                            OrientationChanged?.Invoke(this, orientationData);
                        }
                    }
                    else if (orientationData == null)
                    {
                        Console.WriteLine($"Failed to deserialize JSON: {message}");
                    }
                    else if (string.IsNullOrEmpty(orientationData.Mac))
                    {
                        Console.WriteLine($"JSON message missing MAC address: {message}");
                    }
                }
            }
            catch (JsonException ex)
            {
                Console.WriteLine($"JSON parsing error on {ComPort}: {ex.Message}. Data: {message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing message from {ComPort}: {ex.Message}. Data: {message}");
            }
        }
        
        #endregion
    }
}