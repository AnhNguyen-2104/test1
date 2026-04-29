using System;
using S7.Net;

namespace test1
{
    public class SiemensCommunication : IDisposable
    {
        private Plc plc;
        private string ipAddress;
        private short rack;
        private short slot;
        private bool isConnected = false;

        public bool IsConnected => isConnected;

        public SiemensCommunication(string ip, short rack, short slot)
        {
            this.ipAddress = ip;
            this.rack = rack;
            this.slot = slot;
        }

        public int Connect()
        {
            try
            {
                plc = new Plc(CpuType.S71200, ipAddress, rack, slot);
                plc.Open();
                isConnected = plc.IsConnected;
                return isConnected ? 0 : -1;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Siemens Connect Error: " + ex.Message);
                return -1;
            }
        }

        public void Disconnect()
        {
            if (plc != null)
            {
                try { plc.Close(); } catch { }
                isConnected = false;
            }
        }

        public void Dispose()
        {
            Disconnect();
        }

        public int ReadInt32FromDevicePath(string devicePath)
        {
            if (!isConnected) throw new InvalidOperationException("Siemens PLC chưa kết nối");
            
            // S7.Net Read có thể parse string như DB1.DBD100
            object result = plc.Read(devicePath);
            if (result == null) throw new Exception($"Siemens Read failed for {devicePath}");
            
            if (result is uint) return (int)(uint)result;
            return Convert.ToInt32(result);
        }

        public int WriteInt32ToDevicePath(string devicePath, int value, out string usedMethod)
        {
            usedMethod = "S7.Net Write";
            if (!isConnected) throw new InvalidOperationException("Siemens PLC chưa kết nối");
            
            try
            {
                // Nếu địa chỉ là kiểu Real/Float nhưng UI truyền Int, ta ghi Int. S7.Net tự map nếu dùng đúng syntax.
                // Đối với DBD, truyền value (int) là chuẩn nhất.
                plc.Write(devicePath, value);
                return 0;
            }
            catch (Exception ex)
            {
                throw new Exception($"S7 Write Error: {ex.Message}");
            }
        }
    }
}
