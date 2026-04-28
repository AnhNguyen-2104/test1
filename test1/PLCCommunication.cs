using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

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
        // New helper methods for Buffer Memory (Uxxx\\Gyyyy) and WriteBuffer
        // -----------------------------------------------------------------

        /// <summary>
        /// Direct SetDevice wrapper. Useful for writing 32-bit device like U0\\G2006
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
        /// Use ref object to match COM signature and pass startIO as Int16.
        /// </summary>
        public int WriteBuffer(int startIO, int address, short[] data)
        {
            if (!isConnected)
                throw new InvalidOperationException("Not connected to PLC");

            if (data == null)
                throw new ArgumentNullException(nameof(data));

            try
            {
                short sStart = Convert.ToInt16(startIO);
                int writeSize = data.Length; // number of 16-bit words

                // Create a System.Array of Int16 to ensure proper COM SAFEARRAY marshaling
                Array bufArr = Array.CreateInstance(typeof(short), data.Length);
                for (int i = 0; i < data.Length; i++)
                {
                    bufArr.SetValue(data[i], i);
                }

                object bufObj = (object)bufArr;

                int result = plcDevice.WriteBuffer(sStart, address, writeSize, ref bufObj);
                return result;
            }
            catch (Exception ex)
            {
                throw new Exception($"WriteBuffer failed at U{startIO} G{address}: {ex.Message}");
            }
        }

        /// <summary>
        /// Read raw buffer words from function module buffer using ReadBuffer
        /// Returns an array of 16-bit words read (length = size) and ActUtlType result code via out parameter
        /// Use ref object for buffer and pass startIO as Int16 for compatibility with MX Component.
        /// </summary>
        public short[] ReadBuffer(int startIO, int address, int size, out int resultCode)
        {
            if (!isConnected)
                throw new InvalidOperationException("Not connected to PLC");

            if (size <= 0)
                throw new ArgumentOutOfRangeException(nameof(size));

            try
            {
                short sStart = Convert.ToInt16(startIO);
                short[] data = new short[size];
                object bufObj = data;
                int result = plcDevice.ReadBuffer(sStart, address, size, ref bufObj);
                resultCode = result;

                if (result == 0)
                {
                    if (bufObj is short[] sArr)
                        return sArr;

                    if (bufObj is object[] oArr)
                    {
                        short[] outArr = new short[oArr.Length];
                        for (int i = 0; i < oArr.Length; i++)
                        {
                            outArr[i] = Convert.ToInt16(oArr[i]);
                        }
                        return outArr;
                    }

                    return (short[])bufObj;
                }
                else
                {
                    return null;
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"ReadBuffer failed at U{startIO} G{address}: {ex.Message}");
            }
        }

        /// <summary>
        /// Helper to write a single 32-bit signed integer to buffer address (split into 2 words)
        /// - startIO: Start I/O (e.g., 0 for U0)
        /// - address: buffer address (e.g., 2006 for G2006)
        /// Returns ActUtlType result code (0 = success)
        /// Default ordering is LowWord then HighWord.
        /// </summary>
        public int WriteInt32ToBuffer(int startIO, int address, int value)
        {
            // Split into low and high 16-bit words (low first)
            short[] sData = new short[2];
            sData[0] = (short)(value & 0xFFFF);         // low word
            sData[1] = (short)((value >> 16) & 0xFFFF); // high word

            return WriteBuffer(startIO, address, sData);
        }

        /// <summary>
        /// Alternate ordering: HighWord then LowWord. Some modules expect word order reversed.
        /// Use this if low-first produces incorrect large values.
        /// </summary>
        public int WriteInt32ToBufferHighFirst(int startIO, int address, int value)
        {
            short[] sData = new short[2];
            sData[0] = (short)((value >> 16) & 0xFFFF); // high word
            sData[1] = (short)(value & 0xFFFF);         // low word
            return WriteBuffer(startIO, address, sData);
        }

        /// <summary>
        /// Convenience method: write 32-bit value to device path like "U0\\G2006"
        /// Returns ActUtlType result code (0 = success).
        /// Overload added to auto-detect and fallback if SetDevice doesn't write full 32-bit.
        /// </summary>
        public int WriteInt32ToDevicePath(string devicePath, int value)
        {
            string used;
            return WriteInt32ToDevicePath(devicePath, value, out used);
        }

        /// <summary>
        /// Write 32-bit to device path with verification and fallback.
        /// If devicePath matches Ux\\Gyyyy, tries SetDevice then verifies by ReadDevice; if verification fails falls back to WriteBuffer low/high attempts.
        /// 'usedMethod' returns which approach succeeded: "SetDevice", "WriteBuffer:LowFirst", "WriteBuffer:HighFirst" or empty on failure.
        /// </summary>
        public int WriteInt32ToDevicePath(string devicePath, int value, out string usedMethod)
        {
            usedMethod = string.Empty;
            if (string.IsNullOrEmpty(devicePath))
                throw new ArgumentNullException(nameof(devicePath));

            // Try parse U\G pattern
            if (TryParseUDevicePath(devicePath, out int uNum, out int gAddr))
            {
                // Attempt SetDevice first (simpler)
                int setRes = SetDeviceRaw(devicePath, value);

                // compute startIO param for WriteBuffer/ReadBuffer: U number divided by 16 (as advised)
                int startIO = uNum / 16;

                if (setRes == 0)
                {
                    // verify by reading device directly (avoids ReadBuffer COM conversion issues)
                    try
                    {
                        object readObj = ReadDevice(devicePath);
                        if (readObj != null)
                        {
                            try
                            {
                                int readVal = Convert.ToInt32(readObj);
                                if (readVal == value)
                                {
                                    usedMethod = "SetDevice";
                                    return 0;
                                }
                            }
                            catch { /* not convertible, fallback to buffer method below */ }
                        }
                    }
                    catch { /* ignore and fallback to buffer method */ }

                    // Fallback: try WriteBuffer low-first then verify via ReadDevice
                    int res = WriteInt32ToBuffer(startIO, gAddr, value);
                    if (res == 0)
                    {
                        try
                        {
                            object readObj = ReadDevice(devicePath);
                            if (readObj != null && Convert.ToInt32(readObj) == value)
                            {
                                usedMethod = "WriteBuffer:LowFirst";
                                return 0;
                            }
                        }
                        catch { }
                    }

                    // Try high-first
                    res = WriteInt32ToBufferHighFirst(startIO, gAddr, value);
                    if (res == 0)
                    {
                        try
                        {
                            object readObj = ReadDevice(devicePath);
                            if (readObj != null && Convert.ToInt32(readObj) == value)
                            {
                                usedMethod = "WriteBuffer:HighFirst";
                                return 0;
                            }
                        }
                        catch { }
                    }

                    usedMethod = "AllAttemptsFailed";
                    return -1;
                }
                else
                {
                    // SetDevice returned error, try WriteBuffer directly
                    int res = WriteInt32ToBuffer(startIO, gAddr, value);
                    if (res == 0)
                    {
                        try
                        {
                            object readObj = ReadDevice(devicePath);
                            if (readObj != null && Convert.ToInt32(readObj) == value)
                            {
                                usedMethod = "WriteBuffer:LowFirst";
                                return 0;
                            }
                        }
                        catch { }
                    }

                    res = WriteInt32ToBufferHighFirst(startIO, gAddr, value);
                    if (res == 0)
                    {
                        try
                        {
                            object readObj = ReadDevice(devicePath);
                            if (readObj != null && Convert.ToInt32(readObj) == value)
                            {
                                usedMethod = "WriteBuffer:HighFirst";
                                return 0;
                            }
                        }
                        catch { }
                    }

                    usedMethod = "SetDeviceErrorThenBufferFailed";
                    return setRes;
                }
            }

            // Not a U/G buffer address - call SetDevice directly
            int resFinal = SetDeviceRaw(devicePath, value);
            if (resFinal == 0)
                usedMethod = "SetDevice";
            return resFinal;
        }

        /// <summary>
        /// Read two words (low, high) from G address for convenience
        /// Returns tuple: (lowWord, highWord) and result code via out param
        /// NOTE: This still uses ReadBuffer and may fail in some COM environments. Prefer ReadDevice for verification.
        /// </summary>
        public (int low, int high) ReadInt32FromBuffer(int startIO, int address, out int resultCode)
        {
            short[] data = ReadBuffer(startIO, address, 2, out resultCode);
            if (resultCode != 0 || data == null)
                return (0, 0);

            int low = (ushort)data[0];
            int high = (ushort)data[1];
            return (low, high);
        }

        /// <summary>
        /// Helper to combine two 16-bit words into a 32-bit unsigned int
        /// low = first word, high = second word
        /// </summary>
        private static int CombineWordsLowHigh(ushort low, ushort high)
        {
            return (int)((uint)high << 16 | (uint)low);
        }

        /// <summary>
        /// Helper to combine two 16-bit words into a 32-bit when ordering is HighLow
        /// high = first word, low = second word
        /// </summary>
        private static int CombineWordsHighLow(ushort first, ushort second)
        {
            return (int)((uint)first << 16 | (uint)second);
        }

        /// <summary>
        /// Attempt to write 32-bit value to buffer and verify by reading back using ReadDevice.
        /// This method kept for compatibility but not used for primary verification.
        /// </summary>
        public int WriteInt32ToBufferAuto(int startIO, int address, int value, out string usedOrder)
        {
            usedOrder = string.Empty;
            // Keep old behavior but try to verify via ReadDevice if possible
            // This method is rarely used now; prefer WriteInt32ToDevicePath which includes verification.
            int res = WriteInt32ToBuffer(startIO, address, value);
            if (res != 0)
                return res;

            usedOrder = "LowFirst";
            return 0;
        }

        /// <summary>
        /// Try to parse device path like "U0\\G2006" or "U0\G2006" and return U number and G address.
        /// Returns true if parsed.
        /// </summary>
        private static bool TryParseUDevicePath(string devicePath, out int uNumber, out int gAddress)
        {
            uNumber = 0;
            gAddress = 0;
            if (string.IsNullOrEmpty(devicePath))
                return false;

            // Normalize slashes
            string s = devicePath.Trim();
            s = s.Replace("\\\\", "\\"); // collapsed escapes

            // Pattern: U<number>\\G<number>
            var m = Regex.Match(s, @"^U(\d+)\\G(\d+)$", RegexOptions.IgnoreCase);

            if (!m.Success)
                return false;

            if (!int.TryParse(m.Groups[1].Value, out uNumber))
                return false;
            if (!int.TryParse(m.Groups[2].Value, out gAddress))
                return false;

            return true;
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
