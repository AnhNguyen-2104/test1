using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace test1
{
    public partial class Form1 : Form
    {
        private PLCCommunication plcComm;
        private string plcIPAddress = "192.168.1.100"; // default
        private int plcPort = 2000; // Default MELSEC Ethernet port

        public Form1()
        {
            InitializeComponent();
            InitializePLCFromFields();
            UpdateConnectionState(false);
        }

        private void InitializePLCFromFields()
        {
            try
            {
                plcIPAddress = txtIPAddress.Text.Trim();
                if (!int.TryParse(txtPort.Text.Trim(), out plcPort))
                    plcPort = 2000;

                plcComm = new PLCCommunication(plcIPAddress, plcPort);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error initializing PLC: {ex.Message}", "Error");
            }
        }

        private void UpdateConnectionState(bool connected)
        {
            if (connected)
            {
                lblConnectionState.Text = "PLC CONNECTED";
                lblConnectionState.BackColor = Color.LightGreen;
            }
            else
            {
                lblConnectionState.Text = "PLC DISCONNECTED";
                lblConnectionState.BackColor = Color.LightCoral;
            }
        }

        private void btnConnectSystem_Click(object sender, EventArgs e)
        {
            // Reinitialize with current fields
            InitializePLCFromFields();

            try
            {
                if (plcComm.Connect())
                {
                    UpdateConnectionState(true);
                    MessageBox.Show("Connected to PLC successfully!", "Success");
                }
                else
                {
                    UpdateConnectionState(false);
                    MessageBox.Show("Failed to connect to PLC", "Error");
                }
            }
            catch (Exception ex)
            {
                UpdateConnectionState(false);
                MessageBox.Show($"Connection error: {ex.Message}", "Error");
            }
        }

        private void btnWriteSetDevice32_Click(object sender, EventArgs e)
        {
            string devicePath = txtDeviceName.Text.Trim();
            if (string.IsNullOrEmpty(devicePath))
            {
                MessageBox.Show("Please enter device path (e.g., U0\\G2006)", "Input Required");
                return;
            }

            if (!int.TryParse(txtValue.Text.Trim(), out int value))
            {
                MessageBox.Show("Please enter a valid integer value", "Input Required");
                return;
            }

            try
            {
                int result = plcComm.WriteInt32ToDevicePath(devicePath, value);
                if (result == 0)
                {
                    MessageBox.Show($"SetDevice write successful to {devicePath}", "Success");
                }
                else
                {
                    MessageBox.Show($"SetDevice write failed: {plcComm.GetErrorMessage(result)} (0x{result:X8})", "Error");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Write error: {ex.Message}", "Error");
            }
        }

        private void btnWriteBuffer_Click(object sender, EventArgs e)
        {
            if (!int.TryParse(txtStartIO.Text.Trim(), out int startIO))
            {
                MessageBox.Show("Invalid Start IO", "Input Required");
                return;
            }

            if (!int.TryParse(txtBufferAddress.Text.Trim(), out int gaddr))
            {
                MessageBox.Show("Invalid G address", "Input Required");
                return;
            }

            if (!int.TryParse(txtValue.Text.Trim(), out int value))
            {
                MessageBox.Show("Invalid integer value", "Input Required");
                return;
            }

            try
            {
                int result = plcComm.WriteInt32ToBuffer(startIO, gaddr, value);
                if (result == 0)
                {
                    MessageBox.Show($"WriteBuffer write successful to U{startIO} G{gaddr}", "Success");
                }
                else
                {
                    MessageBox.Show($"WriteBuffer failed: {plcComm.GetErrorMessage(result)} (0x{result:X8})", "Error");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"WriteBuffer error: {ex.Message}", "Error");
            }
        }

        /// <summary>
        /// Disconnect from the PLC
        /// </summary>
        public void DisconnectFromPLC()
        {
            try
            {
                if (plcComm != null && plcComm.Disconnect())
                {
                    UpdateConnectionState(false);
                    MessageBox.Show("Disconnected from PLC successfully!", "Success");
                }
                else
                {
                    MessageBox.Show("Failed to disconnect from PLC", "Error");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Disconnection error: {ex.Message}", "Error");
            }
        }

        /// <summary>
        /// Example: Read a value from the PLC
        /// Device naming examples: "D100" (Data register), "M100" (Relay), "Y0" (Output)
        /// </summary>
        public object ReadValueFromPLC(string deviceName)
        {
            try
            {
                if (plcComm == null || !plcComm.IsConnected)
                {
                    MessageBox.Show("Not connected to PLC", "Error");
                    return null;
                }

                object value = plcComm.ReadDevice(deviceName);
                return value;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Read error: {ex.Message}", "Error");
                return null;
            }
        }

        /// <summary>
        /// Example: Write a value to the PLC
        /// Device naming examples: "D100" (Data register), "M100" (Relay), "Y0" (Output)
        /// </summary>
        public bool WriteValueToPLC(string deviceName, object value)
        {
            try
            {
                if (plcComm == null || !plcComm.IsConnected)
                {
                    MessageBox.Show("Not connected to PLC", "Error");
                    return false;
                }

                plcComm.WriteDevice(deviceName, value);
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Write error: {ex.Message}", "Error");
                return false;
            }
        }

        private void btnConnect_Click(object sender, EventArgs e)
        {
            // Hidden small button
            btnConnectSystem_Click(sender, e);
        }

        private void btnDisconnect_Click(object sender, EventArgs e)
        {
            DisconnectFromPLC();
        }

        private void btnRead_Click(object sender, EventArgs e)
        {
            string deviceName = txtDeviceName.Text.Trim();
            if (string.IsNullOrEmpty(deviceName))
            {
                MessageBox.Show("Please enter a device name (e.g., D100, M100, Y0)", "Input Required");
                return;
            }

            object value = ReadValueFromPLC(deviceName);
            if (value != null)
            {
                txtValue.Text = value.ToString();
            }
        }

        private void btnWrite_Click(object sender, EventArgs e)
        {
            string deviceName = txtDeviceName.Text.Trim();
            string value = txtValue.Text.Trim();

            if (string.IsNullOrEmpty(deviceName))
            {
                MessageBox.Show("Please enter a device name (e.g., D100, M100, Y0)", "Input Required");
                return;
            }

            if (string.IsNullOrEmpty(value))
            {
                MessageBox.Show("Please enter a value to write", "Input Required");
                return;
            }

            // Try to convert value to appropriate type
            object writeValue;
            if (int.TryParse(value, out int intValue))
            {
                writeValue = intValue;
            }
            else if (double.TryParse(value, out double doubleValue))
            {
                writeValue = doubleValue;
            }
            else
            {
                writeValue = value;
            }

            if (WriteValueToPLC(deviceName, writeValue))
            {
                MessageBox.Show($"Successfully wrote {writeValue} to {deviceName}", "Success");
            }
        }

        /// <summary>
        /// Clean up resources on form closing
        /// </summary>
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);

            if (plcComm != null)
            {
                plcComm.Dispose();
            }
        }
    }
}
