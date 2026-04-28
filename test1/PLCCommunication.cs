using System;
using System.Collections.Generic;

namespace test1
{
    /// <summary>
    /// Helper class for communicating with Mitsubishi Q-series PLC via Ethernet
    /// </summary>
    public class PLCCommunication : IDisposable
    {
        private dynamic plcDevice;
        private bool isConnected = false;

        public string IPAddress { get; set; }
        public int Port { get; set; } = 2000; // Default MELSEC port
        public bool IsConnected => isConnected;

        public PLCCommunication(string ipAddress, int port = 2000)
        {
            IPAddress = ipAddress;
            Port = port;
            
            try
            {
                // Create instance of ActUtlType from the Mitsubishi library
                Type actUtlType = Type.GetTypeFromProgID("ActUtlType.ActUtlType");
                plcDevice = Activator.CreateInstance(actUtlType);
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to initialize Mitsubishi ActUtlType: {ex.Message}");
            }
        }

        /// <summary>
        /// Connect to the PLC
        /// </summary>
        public bool Connect()
        {
            try
            {
                if (isConnected)
                    return true;

                // Set communication parameters
                plcDevice.ActLogicalStationNumber = 0; // Logical station number

                // Connect to PLC via Ethernet
                int result = plcDevice.Open();

                if (result == 0)
                {
                    isConnected = true;
                    return true;
                }
                else
                {
                    return false;
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to connect to PLC: {ex.Message}");
            }
        }

        /// <summary>
        /// Disconnect from the PLC
        /// </summary>
        public bool Disconnect()
        {
            try
            {
                if (!isConnected)
                    return true;

                int result = plcDevice.Close();

                if (result == 0)
                {
                    isConnected = false;
                    return true;
                }
                else
                {
                    return false;
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to disconnect from PLC: {ex.Message}");
            }
        }

        /// <summary>
        /// Read a value from a specific device in the PLC
        /// </summary>
        public object ReadDevice(string deviceName, int count = 1)
        {
            if (!isConnected)
                throw new InvalidOperationException("Not connected to PLC");

            try
            {
                object readValue = null;
                int result = plcDevice.ReadDevice(deviceName, count, ref readValue);

                if (result == 0)
                {
                    return readValue;
                }
                else
                {
                    throw new Exception($"Read failed with error code: {result}");
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to read device {deviceName}: {ex.Message}");
            }
        }

        /// <summary>
        /// Write a value to a specific device in the PLC
        /// </summary>
        public bool WriteDevice(string deviceName, object value)
        {
            if (!isConnected)
                throw new InvalidOperationException("Not connected to PLC");

            try
            {
                int result = plcDevice.WriteDevice(deviceName, value);

                if (result == 0)
                {
                    return true;
                }
                else
                {
                    throw new Exception($"Write failed with error code: {result}");
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to write device {deviceName}: {ex.Message}");
            }
        }

        /// <summary>
        /// Read multiple devices at once
        /// </summary>
        public object ReadDeviceBlock(string deviceName, int count)
        {
            if (!isConnected)
                throw new InvalidOperationException("Not connected to PLC");

            try
            {
                object readValue = null;
                int result = plcDevice.ReadDevice(deviceName, count, ref readValue);

                if (result == 0)
                {
                    return readValue;
                }
                else
                {
                    throw new Exception($"Block read failed with error code: {result}");
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to read device block starting at {deviceName}: {ex.Message}");
            }
        }

        /// <summary>
        /// Write multiple devices at once
        /// </summary>
        public bool WriteDeviceBlock(string deviceName, object[] values)
        {
            if (!isConnected)
                throw new InvalidOperationException("Not connected to PLC");

            try
            {
                int result = plcDevice.WriteDevice(deviceName, values);

                if (result == 0)
                {
                    return true;
                }
                else
                {
                    throw new Exception($"Block write failed with error code: {result}");
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to write device block starting at {deviceName}: {ex.Message}");
            }
        }

        // -----------------------------------------------------------------
        // New helper methods for Buffer Memory (Uxxx\Gyyyy) and WriteBuffer
        // -----------------------------------------------------------------

        /// <summary>
        /// Direct SetDevice wrapper. Useful for writing 32-bit device like U0\G2006
        /// Returns the ActUtlType error code (0 = success).
        /// </summary>
        public int SetDeviceRaw(string devicePath, int value)
        {
            if (!isConnected)
                throw new InvalidOperationException("Not connected to PLC");

            try
            {
                // ActUtlType.SetDevice returns int
                int result = plcDevice.SetDevice(devicePath, value);
                return result;
            }
            catch (Exception ex)
            {
                throw new Exception($"SetDevice failed for {devicePath}: {ex.Message}");
            }
        }

        /// <summary>
        /// Write raw buffer words to function module buffer using WriteBuffer
        /// startIO: Start I/O number divided by 16 (e.g., U0 -> 0)
        /// address: Buffer memory address (e.g., 2006 for G2006)
        /// data: array of 16-bit words (short[]) to write
        /// Returns ActUtlType result code (0 = success)
        /// </summary>
        public int WriteBuffer(int startIO, int address, short[] data)
        {
            if (!isConnected)
                throw new InvalidOperationException("Not connected to PLC");

            if (data == null)
                throw new ArgumentNullException(nameof(data));

            try
            {
                int writeSize = data.Length; // number of 16-bit words
                int result = plcDevice.WriteBuffer(startIO, address, writeSize, ref data);
                return result;
            }
            catch (Exception ex)
            {
                throw new Exception($"WriteBuffer failed at U{startIO} G{address}: {ex.Message}");
            }
        }

        /// <summary>
        /// Helper to write a single 32-bit signed integer to buffer address (split into 2 words)
        /// - startIO: Start I/O (e.g., 0 for U0)
        /// - address: buffer address (e.g., 2006 for G2006)
        /// Returns ActUtlType result code (0 = success)
        /// </summary>
        public int WriteInt32ToBuffer(int startIO, int address, int value)
        {
            // Split into low and high 16-bit words
            short[] sData = new short[2];
            sData[0] = (short)(value & 0xFFFF);         // low word
            sData[1] = (short)((value >> 16) & 0xFFFF); // high word

            return WriteBuffer(startIO, address, sData);
        }

        /// <summary>
        /// Convenience method: write 32-bit value to device path like "U0\\G2006"
        /// Returns ActUtlType result code (0 = success).
        /// </summary>
        public int WriteInt32ToDevicePath(string devicePath, int value)
        {
            if (string.IsNullOrEmpty(devicePath))
                throw new ArgumentNullException(nameof(devicePath));

            // If device path like "U0\\G2006", try SetDevice directly which supports 32-bit
            return SetDeviceRaw(devicePath, value);
        }

        /// <summary>
        /// Get error message from error code
        /// </summary>
        public string GetErrorMessage(int errorCode)
        {
            try
            {
                return plcDevice.GetErrorMessage(errorCode);
            }
            catch
            {
                return $"Error code: {errorCode}";
            }
        }

        /// <summary>
        /// Dispose resources
        /// </summary>
        public void Dispose()
        {
            if (isConnected)
            {
                try
                {
                    Disconnect();
                }
                catch { }
            }

            if (plcDevice != null)
            {
                try
                {
                    System.Runtime.InteropServices.Marshal.ReleaseComObject(plcDevice);
                }
                catch { }
            }
        }
    }
}
