using System;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Runtime.InteropServices;

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

        public int ReadDeviceValue(string deviceName)
        {
            if (!isConnected) throw new InvalidOperationException("Chưa kết nối PLC");

            int value = 0;
            int result = plcDevice.GetDevice(deviceName, out value);
            if (result == 0) return value;

            throw new Exception($"Lỗi GetDevice {deviceName}: {GetErrorMessage(result)}");
        }

        public void WriteDeviceValue(string deviceName, int value)
        {
            if (!isConnected) throw new InvalidOperationException("Chưa kết nối PLC");

            int result = plcDevice.SetDevice(deviceName, value);
            if (result != 0)
            {
                throw new Exception($"Lỗi SetDevice {deviceName}: {GetErrorMessage(result)}");
            }
        }

        /// <summary>
        /// Ghi dữ liệu vào Buffer Memory module thông minh.
        /// Xử lý triệt để lỗi "Could not convert argument 0".
        /// </summary>
        public int WriteBuffer(int startIO, int address, short[] data)
        {
            if (!isConnected) throw new InvalidOperationException("Chưa kết nối PLC");
            if (data == null || data.Length == 0) throw new ArgumentException("Khong co du lieu de ghi.", nameof(data));
            try
            {
                return plcDevice.WriteBuffer(startIO, address, data.Length, ref data[0]);
            }
            catch (Exception ex)
            {
                throw new Exception($"Lỗi WriteBuffer: {GetInnermostMessage(ex)}");
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
            if (TryGetNextWordDevice(devicePath, out string nextWordDevice))
            {
                usedMethod = "SetDevice2 x2 (Low word -> High word)";
                return WriteInt32ByWords(devicePath, nextWordDevice, value);
            }

            usedMethod = "SetDevice";
            return plcDevice.SetDevice(devicePath, value);
        }

        private static bool TryParseUDevicePath(string devicePath, out int uNumber, out int gAddress)
        {
            uNumber = 0; gAddress = 0;
            string s = devicePath.Replace("\\\\", "\\").Trim();
            var m = Regex.Match(s, @"^U([0-9A-F]+)\\G(\d+)$", RegexOptions.IgnoreCase);
            if (!m.Success) return false;
            return int.TryParse(m.Groups[1].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uNumber)
                && int.TryParse(m.Groups[2].Value, out gAddress);
        }

        private int WriteInt32ByWords(string lowWordDevice, string highWordDevice, int value)
        {
            short lowWord = (short)(value & 0xFFFF);
            short highWord = (short)((value >> 16) & 0xFFFF);

            int result = plcDevice.SetDevice2(lowWordDevice, lowWord);
            if (result != 0) return result;

            return plcDevice.SetDevice2(highWordDevice, highWord);
        }

        private static bool TryGetNextWordDevice(string devicePath, out string nextWordDevice)
        {
            nextWordDevice = null;
            var match = Regex.Match(devicePath.Trim(), @"^(?<prefix>.*?)(?<address>\d+)$", RegexOptions.IgnoreCase);
            if (!match.Success) return false;

            if (!int.TryParse(match.Groups["address"].Value, out int address)) return false;

            nextWordDevice = match.Groups["prefix"].Value + (address + 1).ToString(CultureInfo.InvariantCulture);
            return true;
        }

        private static string GetInnermostMessage(Exception ex)
        {
            while (ex.InnerException != null) ex = ex.InnerException;
            return ex.Message;
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
