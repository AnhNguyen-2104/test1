using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Runtime.InteropServices;
using System.Reflection;

namespace test1
{
    /// <summary>
    /// Helper class for communicating with Mitsubishi Q-series PLC via Ethernet.
    /// Optimized for Gantry Robot projects and Buffer Memory (U/G) access.
    /// </summary>
    public class PLCCommunication : IDisposable
    {
        private dynamic plcDevice;
        private bool isConnected = false;

        public string IPAddress { get; set; }
        public int Port { get; set; } = 2000;
        public bool IsConnected => isConnected;

        public PLCCommunication(string ipAddress, int port = 2000)
        {
            IPAddress = ipAddress;
            Port = port;

            try
            {
                // Khởi tạo instance của ActUtlType từ thư viện Mitsubishi
                Type actUtlType = Type.GetTypeFromProgID("ActUtlType.ActUtlType");
                if (actUtlType == null)
                    throw new Exception("MX Component (ActUtlType) is not installed on this system.");

                plcDevice = Activator.CreateInstance(actUtlType);
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to initialize Mitsubishi ActUtlType: {ex.Message}");
            }
        }

        public bool Connect()
        {
            try
            {
                if (isConnected) return true;

                // Cấu hình trạm logic (thường để mặc định là 0 nếu dùng ActUtlType)
                plcDevice.ActLogicalStationNumber = 0;

                int result = plcDevice.Open();
                if (result == 0)
                {
                    isConnected = true;
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to connect to PLC: {ex.Message}");
            }
        }

        public bool Disconnect()
        {
            try
            {
                if (!isConnected) return true;
                int result = plcDevice.Close();
                if (result == 0)
                {
                    isConnected = false;
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to disconnect from PLC: {ex.Message}");
            }
        }

        public object ReadDevice(string deviceName, int count = 1)
        {
            if (!isConnected) throw new InvalidOperationException("Not connected to PLC");
            object readValue = null;
            int result = plcDevice.ReadDevice(deviceName, count, ref readValue);
            if (result == 0) return readValue;
            throw new Exception($"Read failed with error code: {result}");
        }

        public bool WriteDevice(string deviceName, object value)
        {
            if (!isConnected) throw new InvalidOperationException("Not connected to PLC");
            int result = plcDevice.WriteDevice(deviceName, value);
            if (result == 0) return true;
            throw new Exception($"Write failed with error code: {result}");
        }

        // --- PHẦN XỬ LÝ BUFFER MEMORY (U\G) VÀ LỖI ÉP KIỂU ---

        public int SetDeviceRaw(string devicePath, int value)
        {
            if (!isConnected) throw new InvalidOperationException("Not connected to PLC");
            return plcDevice.SetDevice(devicePath, value);
        }

        /// <summary>
        /// Ghi dữ liệu vào Buffer Memory module thông minh.
        /// Giải quyết lỗi "Could not convert argument 0" bằng cách ép kiểu tường minh qua Reflection.
        /// </summary>
        public int WriteBuffer(int startIO, int address, short[] data)
        {
            if (!isConnected) throw new InvalidOperationException("Not connected to PLC");
            if (data == null) throw new ArgumentNullException(nameof(data));

            Type comType = plcDevice.GetType();
            int writeSize = data.Length;

            try
            {
                // Chuẩn bị tham số với kiểu dữ liệu chuẩn (short cho StartIO, SAFEARRAY cho Buffer)
                object s = (short)startIO;
                object addr = (int)address;
                object size = (int)writeSize;
                object buf = data;

                // Sử dụng ParameterModifier để đánh dấu tham số truyền vào là 'ref'
                ParameterModifier pm = new ParameterModifier(4);
                pm[3] = true;
                ParameterModifier[] pms = new ParameterModifier[] { pm };

                object ret = comType.InvokeMember("WriteBuffer",
                    BindingFlags.InvokeMethod, null, plcDevice,
                    new object[] { s, addr, size, buf }, pms, null, null);

                return ret != null ? Convert.ToInt32(ret) : 0;
            }
            catch (Exception ex)
            {
                // Fallback: Một số phiên bản yêu cầu mảng Int32
                try
                {
                    int[] bufInt = Array.ConvertAll(data, x => (int)(ushort)x);
                    object bufObj = bufInt;
                    ParameterModifier pm = new ParameterModifier(4);
                    pm[3] = true;

                    object ret = comType.InvokeMember("WriteBuffer",
                        BindingFlags.InvokeMethod, null, plcDevice,
                        new object[] { (short)startIO, address, writeSize, bufObj },
                        new ParameterModifier[] { pm }, null, null);
                    return ret != null ? Convert.ToInt32(ret) : 0;
                }
                catch
                {
                    throw new Exception($"WriteBuffer failed at U{startIO} G{address}: {ex.Message}");
                }
            }
        }

        public short[] ReadBuffer(int startIO, int address, int size, out int resultCode)
        {
            if (!isConnected) throw new InvalidOperationException("Not connected to PLC");
            Type comType = plcDevice.GetType();
            resultCode = -1;

            try
            {
                int[] ph = new int[size];
                object phObj = ph;
                ParameterModifier pm = new ParameterModifier(4);
                pm[3] = true;

                object ret = comType.InvokeMember("ReadBuffer",
                    BindingFlags.InvokeMethod, null, plcDevice,
                    new object[] { (short)startIO, address, size, phObj },
                    new ParameterModifier[] { pm }, null, null);

                resultCode = ret != null ? Convert.ToInt32(ret) : 0;
                if (resultCode == 0)
                {
                    int[] iArr = (int[])phObj;
                    short[] outArr = new short[iArr.Length];
                    for (int i = 0; i < iArr.Length; i++)
                        outArr[i] = (short)(iArr[i] & 0xFFFF);
                    return outArr;
                }
                return null;
            }
            catch (Exception ex)
            {
                throw new Exception($"ReadBuffer failed at U{startIO} G{address}: {ex.Message}");
            }
        }

        public int WriteInt32ToDevicePath(string devicePath, int value, out string usedMethod)
        {
            usedMethod = string.Empty;
            if (TryParseUDevicePath(devicePath, out int uNum, out int gAddr))
            {
                // Thử dùng SetDevice trực tiếp (nhanh và đơn giản nhất)
                int res = SetDeviceRaw(devicePath, value);
                if (res == 0)
                {
                    usedMethod = "SetDevice";
                    return 0;
                }

                // Nếu SetDevice lỗi, chuyển sang WriteBuffer (chia nhỏ 32-bit thành 2 thanh ghi 16-bit)
                short[] sData = new short[2];
                sData[0] = (short)(value & 0xFFFF);
                sData[1] = (short)((value >> 16) & 0xFFFF);

                int bufRes = WriteBuffer(uNum / 16, gAddr, sData);
                if (bufRes == 0) usedMethod = "WriteBuffer";
                return bufRes;
            }

            return SetDeviceRaw(devicePath, value);
        }

        private static bool TryParseUDevicePath(string devicePath, out int uNumber, out int gAddress)
        {
            uNumber = 0; gAddress = 0;
            if (string.IsNullOrEmpty(devicePath)) return false;
            string s = devicePath.Replace("\\\\", "\\").Trim();
            var m = Regex.Match(s, @"^U(\d+)\\G(\d+)$", RegexOptions.IgnoreCase);
            if (!m.Success) return false;
            return int.TryParse(m.Groups[1].Value, out uNumber) && int.TryParse(m.Groups[2].Value, out gAddress);
        }

        public string GetErrorMessage(int errorCode)
        {
            try { return plcDevice.GetErrorMessage(errorCode); }
            catch { return $"Error code: {errorCode}"; }
        }

        public void Dispose()
        {
            if (isConnected) Disconnect();
            if (plcDevice != null)
            {
                Marshal.ReleaseComObject(plcDevice);
                plcDevice = null;
            }
        }
    }
}