using System;
using System.Drawing;
using System.Windows.Forms;

namespace test1
{
    public partial class Form1 : Form
    {
        private PLCCommunication plcComm;

        public Form1()
        {
            InitializeComponent();
            UpdateConnectionState(false);
        }

        private void UpdateConnectionState(bool connected)
        {
            lblConnectionState.Text = connected ? "PLC CONNECTED" : "PLC DISCONNECTED";
            lblConnectionState.BackColor = connected ? Color.LightGreen : Color.LightCoral;
        }

        private void btnConnectSystem_Click(object sender, EventArgs e)
        {
            try
            {
                plcComm = new PLCCommunication(txtIPAddress.Text.Trim());
                if (plcComm.Connect())
                {
                    UpdateConnectionState(true);
                    MessageBox.Show("Kết nối hệ thống bôi keo thành công!");
                }
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        private void btnWriteBuffer_Click(object sender, EventArgs e)
        {
            try
            {
                if (!EnsureConnected()) return;
                if (!TryParseInput(txtStartIO.Text, "Start IO", out int startIO)) return;
                if (!TryParseInput(txtBufferAddress.Text, "G Addr", out int address)) return;
                if (!TryParseInput(txtValue.Text, "Gia tri", out int val)) return;

                int res = plcComm.WriteInt32ToBuffer(startIO, address, val);

                if (res == 0)
                    MessageBox.Show("Ghi thanh cong qua WriteBuffer.");
                else
                    MessageBox.Show($"Loi PLC: {plcComm.GetErrorMessage(res)}");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Lỗi: " + ex.Message);
            }
        }

        private void btnWriteSetDevice32_Click(object sender, EventArgs e)
        {
            try
            {
                if (!EnsureConnected()) return;
                string path = txtDeviceName.Text.Trim();
                if (string.IsNullOrWhiteSpace(path))
                {
                    MessageBox.Show("Device Path khong duoc de trong.");
                    return;
                }

                if (!TryParseInput(txtValue.Text, "Gia tri", out int val)) return;

                string method;
                int res = plcComm.WriteInt32ToDevicePath(path, val, out method);

                if (res == 0)
                    MessageBox.Show($"Ghi thanh cong qua {method}");
                else
                    MessageBox.Show($"Loi PLC: {plcComm.GetErrorMessage(res)}");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Loi: " + ex.Message);
            }
        }

        private bool EnsureConnected()
        {
            if (plcComm != null && plcComm.IsConnected) return true;

            MessageBox.Show("Chua ket noi PLC.");
            return false;
        }

        private bool TryParseInput(string rawValue, string fieldName, out int value)
        {
            if (int.TryParse(rawValue.Trim(), out value)) return true;

            MessageBox.Show($"{fieldName} khong hop le.");
            return false;
        }

        private void btnRead_Click(object sender, EventArgs e)
        {
            try
            {
                object val = plcComm.ReadDevice(txtDeviceName.Text.Trim());
                if (val != null) txtValue.Text = val.ToString();
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (plcComm != null) plcComm.Dispose();
            base.OnFormClosing(e);
        }
    }
}
