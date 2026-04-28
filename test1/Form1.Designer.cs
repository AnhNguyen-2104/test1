namespace test1
{
    partial class Form1
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        private System.Windows.Forms.Button btnConnect;
        private System.Windows.Forms.Button btnDisconnect;
        private System.Windows.Forms.Button btnRead;
        private System.Windows.Forms.Button btnWrite;
        private System.Windows.Forms.TextBox txtDeviceName;
        private System.Windows.Forms.TextBox txtValue;
        private System.Windows.Forms.Label lblStatus;
        private System.Windows.Forms.Label lblDeviceName;
        private System.Windows.Forms.Label lblValue;

        // New controls for connection UI
        private System.Windows.Forms.Label lblConnectionState;
        private System.Windows.Forms.Label lblIP;
        private System.Windows.Forms.TextBox txtIPAddress;
        private System.Windows.Forms.Label lblPort;
        private System.Windows.Forms.TextBox txtPort;
        private System.Windows.Forms.Button btnConnectSystem;

        // New controls for buffer write
        private System.Windows.Forms.Label lblStartIO;
        private System.Windows.Forms.TextBox txtStartIO;
        private System.Windows.Forms.Label lblBufferAddress;
        private System.Windows.Forms.TextBox txtBufferAddress;
        private System.Windows.Forms.Button btnWriteBuffer;
        private System.Windows.Forms.Button btnWriteSetDevice32;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.btnConnect = new System.Windows.Forms.Button();
            this.btnDisconnect = new System.Windows.Forms.Button();
            this.btnRead = new System.Windows.Forms.Button();
            this.btnWrite = new System.Windows.Forms.Button();
            this.txtDeviceName = new System.Windows.Forms.TextBox();
            this.txtValue = new System.Windows.Forms.TextBox();
            this.lblStatus = new System.Windows.Forms.Label();
            this.lblDeviceName = new System.Windows.Forms.Label();
            this.lblValue = new System.Windows.Forms.Label();

            // connection UI
            this.lblConnectionState = new System.Windows.Forms.Label();
            this.lblIP = new System.Windows.Forms.Label();
            this.txtIPAddress = new System.Windows.Forms.TextBox();
            this.lblPort = new System.Windows.Forms.Label();
            this.txtPort = new System.Windows.Forms.TextBox();
            this.btnConnectSystem = new System.Windows.Forms.Button();

            // buffer write UI
            this.lblStartIO = new System.Windows.Forms.Label();
            this.txtStartIO = new System.Windows.Forms.TextBox();
            this.lblBufferAddress = new System.Windows.Forms.Label();
            this.txtBufferAddress = new System.Windows.Forms.TextBox();
            this.btnWriteBuffer = new System.Windows.Forms.Button();
            this.btnWriteSetDevice32 = new System.Windows.Forms.Button();

            this.SuspendLayout();
            // 
            // lblConnectionState
            // 
            this.lblConnectionState.AutoSize = false;
            this.lblConnectionState.Location = new System.Drawing.Point(12, 12);
            this.lblConnectionState.Name = "lblConnectionState";
            this.lblConnectionState.Size = new System.Drawing.Size(360, 25);
            this.lblConnectionState.TabIndex = 0;
            this.lblConnectionState.Text = "PLC DISCONNECTED";
            this.lblConnectionState.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.lblConnectionState.BackColor = System.Drawing.Color.LightCoral;

            // 
            // lblIP
            // 
            this.lblIP.AutoSize = true;
            this.lblIP.Location = new System.Drawing.Point(12, 50);
            this.lblIP.Name = "lblIP";
            this.lblIP.Size = new System.Drawing.Size(58, 13);
            this.lblIP.TabIndex = 1;
            this.lblIP.Text = "IP Address:";

            // 
            // txtIPAddress
            // 
            this.txtIPAddress.Location = new System.Drawing.Point(12, 66);
            this.txtIPAddress.Name = "txtIPAddress";
            this.txtIPAddress.Size = new System.Drawing.Size(200, 20);
            this.txtIPAddress.TabIndex = 2;
            this.txtIPAddress.Text = "192.168.1.100";

            // 
            // lblPort
            // 
            this.lblPort.AutoSize = true;
            this.lblPort.Location = new System.Drawing.Point(12, 95);
            this.lblPort.Name = "lblPort";
            this.lblPort.Size = new System.Drawing.Size(29, 13);
            this.lblPort.TabIndex = 3;
            this.lblPort.Text = "Port:";

            // 
            // txtPort
            // 
            this.txtPort.Location = new System.Drawing.Point(12, 111);
            this.txtPort.Name = "txtPort";
            this.txtPort.Size = new System.Drawing.Size(200, 20);
            this.txtPort.TabIndex = 4;
            this.txtPort.Text = "2000";

            // 
            // btnConnectSystem
            // 
            this.btnConnectSystem.Location = new System.Drawing.Point(12, 145);
            this.btnConnectSystem.Name = "btnConnectSystem";
            this.btnConnectSystem.Size = new System.Drawing.Size(360, 35);
            this.btnConnectSystem.TabIndex = 5;
            this.btnConnectSystem.Text = "CONNECT SYSTEM";
            this.btnConnectSystem.UseVisualStyleBackColor = true;
            this.btnConnectSystem.BackColor = System.Drawing.Color.DodgerBlue;
            this.btnConnectSystem.ForeColor = System.Drawing.Color.White;
            this.btnConnectSystem.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnConnectSystem.Click += new System.EventHandler(this.btnConnectSystem_Click);

            // Keep existing small connect/disconnect buttons but place them out of the way
            this.btnConnect.Location = new System.Drawing.Point(400, 12);
            this.btnConnect.Name = "btnConnectSmall";
            this.btnConnect.Size = new System.Drawing.Size(75, 23);
            this.btnConnect.TabIndex = 20;
            this.btnConnect.Text = "Connect";
            this.btnConnect.UseVisualStyleBackColor = true;
            this.btnConnect.Visible = false;

            this.btnDisconnect.Location = new System.Drawing.Point(480, 12);
            this.btnDisconnect.Name = "btnDisconnectSmall";
            this.btnDisconnect.Size = new System.Drawing.Size(75, 23);
            this.btnDisconnect.TabIndex = 21;
            this.btnDisconnect.Text = "Disconnect";
            this.btnDisconnect.UseVisualStyleBackColor = true;
            this.btnDisconnect.Visible = false;

            // 
            // lblDeviceName
            // 
            this.lblDeviceName.AutoSize = true;
            this.lblDeviceName.Location = new System.Drawing.Point(12, 200);
            this.lblDeviceName.Name = "lblDeviceName";
            this.lblDeviceName.Size = new System.Drawing.Size(75, 13);
            this.lblDeviceName.TabIndex = 6;
            this.lblDeviceName.Text = "Device Path:";

            // 
            // txtDeviceName
            // 
            this.txtDeviceName.Location = new System.Drawing.Point(93, 197);
            this.txtDeviceName.Name = "txtDeviceName";
            this.txtDeviceName.Size = new System.Drawing.Size(125, 20);
            this.txtDeviceName.TabIndex = 7;
            this.txtDeviceName.Text = "U0\\G2006";

            // 
            // lblValue
            // 
            this.lblValue.AutoSize = true;
            this.lblValue.Location = new System.Drawing.Point(12, 230);
            this.lblValue.Name = "lblValue";
            this.lblValue.Size = new System.Drawing.Size(37, 13);
            this.lblValue.TabIndex = 8;
            this.lblValue.Text = "Value:";

            // 
            // txtValue
            // 
            this.txtValue.Location = new System.Drawing.Point(93, 227);
            this.txtValue.Name = "txtValue";
            this.txtValue.Size = new System.Drawing.Size(125, 20);
            this.txtValue.TabIndex = 9;

            // 
            // btnWriteSetDevice32
            // 
            this.btnWriteSetDevice32.Location = new System.Drawing.Point(230, 195);
            this.btnWriteSetDevice32.Name = "btnWriteSetDevice32";
            this.btnWriteSetDevice32.Size = new System.Drawing.Size(142, 25);
            this.btnWriteSetDevice32.TabIndex = 10;
            this.btnWriteSetDevice32.Text = "Write (SetDevice 32-bit)";
            this.btnWriteSetDevice32.UseVisualStyleBackColor = true;
            this.btnWriteSetDevice32.Click += new System.EventHandler(this.btnWriteSetDevice32_Click);


            // 
            // lblStartIO
            // 
            this.lblStartIO.AutoSize = true;
            this.lblStartIO.Location = new System.Drawing.Point(12, 270);
            this.lblStartIO.Name = "lblStartIO";
            this.lblStartIO.Size = new System.Drawing.Size(47, 13);
            this.lblStartIO.TabIndex = 11;
            this.lblStartIO.Text = "Start IO:";

            // 
            // txtStartIO
            // 
            this.txtStartIO.Location = new System.Drawing.Point(93, 267);
            this.txtStartIO.Name = "txtStartIO";
            this.txtStartIO.Size = new System.Drawing.Size(60, 20);
            this.txtStartIO.TabIndex = 12;
            this.txtStartIO.Text = "0";

            // 
            // lblBufferAddress
            // 
            this.lblBufferAddress.AutoSize = true;
            this.lblBufferAddress.Location = new System.Drawing.Point(160, 270);
            this.lblBufferAddress.Name = "lblBufferAddress";
            this.lblBufferAddress.Size = new System.Drawing.Size(46, 13);
            this.lblBufferAddress.TabIndex = 13;
            this.lblBufferAddress.Text = "G Addr:";

            // 
            // txtBufferAddress
            // 
            this.txtBufferAddress.Location = new System.Drawing.Point(210, 267);
            this.txtBufferAddress.Name = "txtBufferAddress";
            this.txtBufferAddress.Size = new System.Drawing.Size(60, 20);
            this.txtBufferAddress.TabIndex = 14;
            this.txtBufferAddress.Text = "2006";

            // 
            // btnWriteBuffer
            // 
            this.btnWriteBuffer.Location = new System.Drawing.Point(280, 265);
            this.btnWriteBuffer.Name = "btnWriteBuffer";
            this.btnWriteBuffer.Size = new System.Drawing.Size(155, 25);
            this.btnWriteBuffer.TabIndex = 15;
            this.btnWriteBuffer.Text = "Write (WriteBuffer)";
            this.btnWriteBuffer.UseVisualStyleBackColor = true;
            this.btnWriteBuffer.Click += new System.EventHandler(this.btnWriteBuffer_Click);

            // 
            // btnRead
            // 
            this.btnRead.Location = new System.Drawing.Point(12, 310);
            this.btnRead.Name = "btnRead";
            this.btnRead.Size = new System.Drawing.Size(100, 30);
            this.btnRead.TabIndex = 16;
            this.btnRead.Text = "Read";
            this.btnRead.UseVisualStyleBackColor = true;
            this.btnRead.Click += new System.EventHandler(this.btnRead_Click);

            // 
            // btnWrite (generic)
            // 
            this.btnWrite.Location = new System.Drawing.Point(118, 310);
            this.btnWrite.Name = "btnWriteGeneric";
            this.btnWrite.Size = new System.Drawing.Size(100, 30);
            this.btnWrite.TabIndex = 17;
            this.btnWrite.Text = "Write";
            this.btnWrite.UseVisualStyleBackColor = true;


            // 
            // Form1
            // 
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(800, 600);
            this.Controls.Add(this.btnWrite);
            this.Controls.Add(this.btnRead);
            this.Controls.Add(this.btnWriteBuffer);
            this.Controls.Add(this.txtBufferAddress);
            this.Controls.Add(this.lblBufferAddress);
            this.Controls.Add(this.txtStartIO);
            this.Controls.Add(this.lblStartIO);
            this.Controls.Add(this.btnWriteSetDevice32);
            this.Controls.Add(this.txtValue);
            this.Controls.Add(this.lblValue);
            this.Controls.Add(this.txtDeviceName);
            this.Controls.Add(this.lblDeviceName);
            this.Controls.Add(this.btnConnectSystem);
            this.Controls.Add(this.txtPort);
            this.Controls.Add(this.lblPort);
            this.Controls.Add(this.txtIPAddress);
            this.Controls.Add(this.lblIP);
            this.Controls.Add(this.lblConnectionState);
            this.Controls.Add(this.btnDisconnect);
            this.Controls.Add(this.btnConnect);
            this.Name = "Form1";
            this.Text = "Precision SCADA - PLC Control";
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        #endregion
    }
}

