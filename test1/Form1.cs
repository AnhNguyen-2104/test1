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
                int startIO = int.Parse(txtStartIO.Text.Trim());
                int gAddr = int.Parse(txtBufferAddress.Text.Trim()); // Ví dụ: 2006
                int val = int.Parse(txtValue.Text.Trim());

                // Sử dụng hàm ghi 32-bit đã xử lý Low/High
                int res = plcComm.WriteInt32ToBuffer(startIO, gAddr, val);

                if (res == 0) MessageBox.Show($"Ghi thành công: G{gAddr}(L) và G{gAddr + 1}(H)");
                else MessageBox.Show($"Lỗi: {plcComm.GetErrorMessage(res)}");
            }
            catch (Exception ex) { MessageBox.Show("Kiểm tra lại dữ liệu nhập: " + ex.Message); }
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