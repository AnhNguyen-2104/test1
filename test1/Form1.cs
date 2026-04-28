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
                string ip = txtIPAddress.Text.Trim();
                int port = int.TryParse(txtPort.Text.Trim(), out int p) ? p : 2000;

                plcComm = new PLCCommunication(ip, port);
                if (plcComm.Connect())
                {
                    UpdateConnectionState(true);
                    MessageBox.Show("Kết nối thành công!");
                }
                else MessageBox.Show("Kết nối thất bại.");
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        private void btnWriteSetDevice32_Click(object sender, EventArgs e)
        {
            try
            {
                string path = txtDeviceName.Text.Trim();
                int val = int.Parse(txtValue.Text.Trim());
                int res = plcComm.WriteInt32ToDevicePath(path, val, out string method);

                if (res == 0) MessageBox.Show($"Ghi thành công qua {method}");
                else MessageBox.Show($"Lỗi: {plcComm.GetErrorMessage(res)}");
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        private void btnWriteBuffer_Click(object sender, EventArgs e)
        {
            try
            {
                int startIO = int.Parse(txtStartIO.Text.Trim());
                int gAddr = int.Parse(txtBufferAddress.Text.Trim());
                int val = int.Parse(txtValue.Text.Trim());

                int res = plcComm.WriteInt32ToBufferAuto(startIO, gAddr, val, out string order);
                if (res == 0) MessageBox.Show($"Ghi Buffer thành công ({order})");
                else MessageBox.Show($"Lỗi: {plcComm.GetErrorMessage(res)}");
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
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