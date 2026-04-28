using System;
using System.Text.RegularExpressions;
using System.Runtime.InteropServices;
using System.Reflection;

namespace test1
{
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
                Type actUtlType = Type.GetTypeFromProgID("ActUtlType.ActUtlType");
                if (actUtlType == null) throw new Exception("MX Component chưa được cài đặt.");
                plcDevice = Activator.CreateInstance(actUtlType);
            }
            catch (Exception ex)
            {
                throw new Exception($"Lỗi khởi tạo ActUtlType: {ex.Message}");
            }
        }

        public bool Connect()
        {
            try
            {
                if (isConnected) return true;
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
                throw new Exception($"Lỗi kết nối PLC: {ex.Message}");
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
                throw new Exception($"Lỗi ngắt kết nối: {ex.Message}");
            }
        }

        public object ReadDevice(string deviceName, int count = 1)
        {
            if (!isConnected) throw new InvalidOperationException("Chưa kết nối PLC");
            object readValue = null;
            int result = plcDevice.ReadDevice(deviceName, count, ref readValue);
            if (result == 0) return readValue;
            throw new Exception($"Lỗi ReadDevice: {result}");
        }

        /// <summary>
        /// Ghi dữ liệu vào Buffer Memory module thông minh.
        /// Xử lý triệt để lỗi "Could not convert argument 0".
        /// </summary>
        public int WriteBuffer(int startIO, int address, short[] data)
        {
            if (!isConnected) throw new InvalidOperationException("Chưa kết nối PLC");
            Type comType = plcDevice.GetType();
            try
            {
                object s = (short)startIO; // Ép kiểu short bắt buộc cho Argument 0
                object addr = (int)address;
                object size = (int)data.Length;
                object buf = data;

                ParameterModifier pm = new ParameterModifier(4);
                pm[3] = true; // Truyền SAFEARRAY bằng ref

                object ret = comType.InvokeMember("WriteBuffer", BindingFlags.InvokeMethod, null,
                    plcDevice, new object[] { s, addr, size, buf }, new ParameterModifier[] { pm }, null, null);
                return ret != null ? Convert.ToInt32(ret) : 0;
            }
            catch (Exception ex)
            {
                throw new Exception($"Lỗi WriteBuffer: {ex.Message}");
            }
        }

        /// <summary>
        /// Ghi số 32-bit theo thứ tự Low Word -> High Word.
        /// Ví dụ: G2006 (L) và G2007 (H).
        /// </summary>
        public int WriteInt32ToBuffer(int startIO, int address, int value)
        {
            short[] sData = new short[2];
            // Tách giá trị 32-bit thành 2 thanh ghi 16-bit
            sData[0] = (short)(value & 0xFFFF);         // Gửi vào G2006 (Low)
            sData[1] = (short)((value >> 16) & 0xFFFF); // Gửi vào G2007 (High)

            return WriteBuffer(startIO, address, sData);
        }

        public int WriteInt32ToDevicePath(string devicePath, int value, out string usedMethod)
        {
            usedMethod = "Unknown";
            if (TryParseUDevicePath(devicePath, out int uNum, out int gAddr))
            {
                // Thử ghi trực tiếp bằng SetDevice
                int res = plcDevice.SetDevice(devicePath, value);
                if (res == 0)
                {
                    usedMethod = "SetDevice";
                    return 0;
                }

                // Nếu SetDevice lỗi, dùng cơ chế tách Word thủ công
                usedMethod = "WriteBuffer (Tách Word)";
                return WriteInt32ToBuffer(uNum / 16, gAddr, value);
            }
            usedMethod = "SetDevice";
            return plcDevice.SetDevice(devicePath, value);
        }

        private static bool TryParseUDevicePath(string devicePath, out int uNumber, out int gAddress)
        {
            uNumber = 0; gAddress = 0;
            string s = devicePath.Replace("\\\\", "\\").Trim();
            var m = Regex.Match(s, @"^U(\d+)\\G(\d+)$", RegexOptions.IgnoreCase);
            if (!m.Success) return false;
            return int.TryParse(m.Groups[1].Value, out uNumber) && int.TryParse(m.Groups[2].Value, out gAddress);
        }

        public string GetErrorMessage(int errorCode)
        {
            try { return plcDevice.GetErrorMessage(errorCode); }
            catch { return $"Mã lỗi: {errorCode}"; }
        }

        public void Dispose()
        {
            if (isConnected) Disconnect();
            if (plcDevice != null) Marshal.ReleaseComObject(plcDevice);
        }
    }
}