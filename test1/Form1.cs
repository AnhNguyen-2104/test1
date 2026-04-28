using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Point = System.Drawing.Point;
using netDxf;
using netDxf.Entities;

namespace test1
{
    public partial class Form1 : Form
    {
        private const string CoordinateXRegister = "D2000";
        private const string CoordinateYRegister = "D2002";
        private const string CoordinateZRegister = "D2004";
        private const string VelocityRegister = "D406";
        private const string JogBaseRegister = "M3000";
        private const string EmergencyStopRegister = "M3100";

        private PLCCommunication plcComm;
        private readonly Timer plcPollTimer = new Timer();

        private readonly BindingList<MonitorRow> monitorRows = new BindingList<MonitorRow>();
        private readonly BindingList<CadPointRow> cadPointRows = new BindingList<CadPointRow>();
        private readonly BindingList<ProcessRow> processRows = new BindingList<ProcessRow>();
        private readonly List<CadPrimitive> cadPrimitives = new List<CadPrimitive>();
        private readonly Dictionary<AssignmentSlot, CadPointRow> assignedPoints = new Dictionary<AssignmentSlot, CadPointRow>();

        private RectangleF cadBounds = RectangleF.Empty;
        private CadPointRow selectedPoint;
        private bool suppressPointSelection;

        private AppView currentView = AppView.Control;
        private string lastPollError = string.Empty;

        private ProcessRow startPointRow;
        private ProcessRow glueStartRow;
        private ProcessRow glueEndRow;
        private ProcessRow zDownRow;
        private ProcessRow zSafeRow;
        private ProcessRow speedRow;

        private Panel pnlHeader;
        private Panel pnlSidebar;
        private Panel pnlViewHost;
        private Panel pnlViewControl;
        private Panel pnlViewDxf;
        private Panel pnlControlCanvas;
        private Panel pnlDxfCanvas;

        private Label lblBrand;
        private Button btnHeaderDashboard;
        private Button btnHeaderLogs;
        private Button btnHeaderTelemetry;
        private Button btnHeaderSettings;
        private Button btnHeaderDxfRun;
        private Button btnHeaderTheme;
        private Button btnHeaderBell;
        private Button btnHeaderUser;

        private Panel pnlUnitCard;
        private Label lblUnitTitle;
        private Label lblUnitSubtitle;
        private Button btnSideControl;
        private Button btnSideTelemetry;
        private Button btnSideLogs;
        private Button btnSideSettings;
        private Button btnSideDxfRun;
        private Button btnEmergencyStop;
        private Button btnSupport;
        private Button btnLogout;

        private Panel cardConnection;
        private Label lblConnectionBanner;
        private Label lblConnectionMeta;
        private TextBox txtIpAddress;
        private TextBox txtPort;
        private Button btnConnectSystem;

        private Panel cardCoordinateX;
        private Panel cardCoordinateY;
        private Panel cardCoordinateZ;
        private Label lblCoordinateXValue;
        private Label lblCoordinateYValue;
        private Label lblCoordinateZValue;
        private Label lblCoordinateXRaw;
        private Label lblCoordinateYRaw;
        private Label lblCoordinateZRaw;

        private Panel cardVelocity;
        private TrackBar trkVelocity;
        private Label lblVelocityValue;
        private Label lblVelocityRaw;

        private Panel cardIntegrity;
        private Label lblIntegrityState;
        private Label lblIntegrityDetail;

        private Panel cardJog;
        private Label lblJogBase;
        private Button btnJogUp;
        private Button btnJogDown;
        private Button btnJogLeft;
        private Button btnJogRight;
        private Button btnJogZUp;
        private Button btnJogZDown;

        private Panel cardMonitor;
        private Button btnAddRegister;
        private DataGridView dgvMonitor;
        private Label lblMonitorEmpty;

        private Label lblCadPathCaption;
        private Label lblCadFileCaption;
        private TextBox txtCadPath;
        private TextBox txtCadFileName;
        private Button btnOpenCad;
        private Panel cardCadPreview;
        private Panel pnlCadViewport;
        private Label lblCadHint;
        private Label lblZHint;
        private Panel cardCadPoints;
        private DataGridView dgvCadPoints;
        private Label lblPointTableTitle;
        private Panel cardProcess;
        private DataGridView dgvProcess;
        private Button btnAssignStart;
        private Button btnAssignGlueStart;
        private Button btnAssignGlueEnd;
        private Button btnSetZDown;
        private Button btnSetZSafe;
        private Button btnSetSpeed;
        private Button btnResume;
        private Button btnPause;
        private Button btnStart;

        public Form1()
        {
            InitializeComponent();

            BuildShell();
            BuildControlView();
            BuildDxfView();
            InitializeDashboardData();
            InitializeDxfData();

            plcPollTimer.Interval = 500;
            plcPollTimer.Tick += PlcPollTimer_Tick;

            SwitchView(AppView.Control);
            UpdateConnectionState(false, "PLC disconnected");
        }

        private void BuildShell()
        {
            SuspendLayout();

            Text = "Gantry SCADA Robot Control";
            BackColor = Color.FromArgb(244, 247, 252);
            Font = new Font("Segoe UI", 10F);
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(1820, 980);
            MinimumSize = new Size(1780, 940);

            pnlHeader = new Panel();
            pnlHeader.Dock = DockStyle.Top;
            pnlHeader.Height = 82;
            pnlHeader.BackColor = Color.White;
            pnlHeader.Padding = new Padding(24, 18, 24, 18);
            Controls.Add(pnlHeader);

            pnlSidebar = new Panel();
            pnlSidebar.Dock = DockStyle.Left;
            pnlSidebar.Width = 220;
            pnlSidebar.BackColor = Color.White;
            pnlSidebar.Padding = new Padding(0, 18, 0, 18);
            Controls.Add(pnlSidebar);

            pnlViewHost = new Panel();
            pnlViewHost.Dock = DockStyle.Fill;
            pnlViewHost.BackColor = Color.FromArgb(244, 247, 252);
            Controls.Add(pnlViewHost);

            pnlViewControl = new Panel();
            pnlViewControl.Dock = DockStyle.Fill;
            pnlViewControl.AutoScroll = true;
            pnlViewControl.BackColor = Color.FromArgb(244, 247, 252);
            pnlViewHost.Controls.Add(pnlViewControl);

            pnlViewDxf = new Panel();
            pnlViewDxf.Dock = DockStyle.Fill;
            pnlViewDxf.AutoScroll = true;
            pnlViewDxf.BackColor = Color.FromArgb(244, 247, 252);
            pnlViewHost.Controls.Add(pnlViewDxf);

            BuildHeaderControls();
            BuildSidebarControls();

            ResumeLayout(false);
        }

        private void BuildHeaderControls()
        {
            lblBrand = new Label();
            lblBrand.AutoSize = true;
            lblBrand.Font = new Font("Segoe UI", 20F, FontStyle.Bold);
            lblBrand.ForeColor = Color.FromArgb(31, 108, 214);
            lblBrand.Location = new Point(26, 18);
            lblBrand.Text = "PRECISION SCADA";
            pnlHeader.Controls.Add(lblBrand);

            btnHeaderDashboard = CreateHeaderNavButton("DASHBOARD", 246, SwitchToControlView);
            btnHeaderLogs = CreateHeaderNavButton("LOGS", 346, ShowPlaceholderSection);
            btnHeaderTelemetry = CreateHeaderNavButton("TELEMETRY", 412, ShowPlaceholderSection);
            btnHeaderSettings = CreateHeaderNavButton("SYSTEM SETTINGS", 532, ShowPlaceholderSection);
            btnHeaderDxfRun = CreateHeaderNavButton("DXF RUN", 710, SwitchToDxfView);

            pnlHeader.Controls.Add(btnHeaderDashboard);
            pnlHeader.Controls.Add(btnHeaderLogs);
            pnlHeader.Controls.Add(btnHeaderTelemetry);
            pnlHeader.Controls.Add(btnHeaderSettings);
            pnlHeader.Controls.Add(btnHeaderDxfRun);

            btnHeaderTheme = CreateHeaderIconButton("◔", 1560);
            btnHeaderBell = CreateHeaderIconButton("!", 1620);
            btnHeaderUser = CreateHeaderIconButton("◎", 1680);

            pnlHeader.Controls.Add(btnHeaderTheme);
            pnlHeader.Controls.Add(btnHeaderBell);
            pnlHeader.Controls.Add(btnHeaderUser);
        }

        private void BuildSidebarControls()
        {
            pnlUnitCard = new Panel();
            pnlUnitCard.Location = new Point(18, 18);
            pnlUnitCard.Size = new Size(184, 90);
            pnlUnitCard.BackColor = Color.FromArgb(241, 246, 252);
            pnlUnitCard.BorderStyle = BorderStyle.FixedSingle;
            pnlSidebar.Controls.Add(pnlUnitCard);

            lblUnitTitle = new Label();
            lblUnitTitle.Location = new Point(18, 18);
            lblUnitTitle.Size = new Size(150, 24);
            lblUnitTitle.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
            lblUnitTitle.Text = "Unit 01";
            pnlUnitCard.Controls.Add(lblUnitTitle);

            lblUnitSubtitle = new Label();
            lblUnitSubtitle.Location = new Point(18, 44);
            lblUnitSubtitle.Size = new Size(150, 22);
            lblUnitSubtitle.ForeColor = Color.FromArgb(100, 110, 125);
            lblUnitSubtitle.Text = "PLC-Gantry-Alpha";
            pnlUnitCard.Controls.Add(lblUnitSubtitle);

            btnSideControl = CreateSidebarButton("CONTROL", 130, true, SwitchToControlView);
            btnSideTelemetry = CreateSidebarButton("TELEMETRY", 185, false, ShowPlaceholderSection);
            btnSideLogs = CreateSidebarButton("LOGS", 240, false, ShowPlaceholderSection);
            btnSideSettings = CreateSidebarButton("SETTINGS", 295, false, ShowPlaceholderSection);
            btnSideDxfRun = CreateSidebarButton("DXF RUN", 350, false, SwitchToDxfView);

            pnlSidebar.Controls.Add(btnSideControl);
            pnlSidebar.Controls.Add(btnSideTelemetry);
            pnlSidebar.Controls.Add(btnSideLogs);
            pnlSidebar.Controls.Add(btnSideSettings);
            pnlSidebar.Controls.Add(btnSideDxfRun);

            btnEmergencyStop = new Button();
            btnEmergencyStop.Location = new Point(18, 760);
            btnEmergencyStop.Size = new Size(184, 42);
            btnEmergencyStop.Text = "EMERGENCY STOP";
            btnEmergencyStop.Anchor = AnchorStyles.Left | AnchorStyles.Bottom;
            btnEmergencyStop.Click += btnEmergencyStop_Click;
            ApplyDangerButton(btnEmergencyStop);
            pnlSidebar.Controls.Add(btnEmergencyStop);

            btnSupport = CreateSidebarFooterButton("SUPPORT", 842);
            btnLogout = CreateSidebarFooterButton("LOGOUT", 884);
            pnlSidebar.Controls.Add(btnSupport);
            pnlSidebar.Controls.Add(btnLogout);
        }

        private void BuildControlView()
        {
            pnlControlCanvas = new Panel();
            pnlControlCanvas.Location = new Point(24, 24);
            pnlControlCanvas.Size = new Size(1540, 840);
            pnlControlCanvas.BackColor = Color.Transparent;
            pnlViewControl.Controls.Add(pnlControlCanvas);

            cardConnection = CreateCardPanel(new Rectangle(28, 24, 430, 390));
            pnlControlCanvas.Controls.Add(cardConnection);
            AddCardHeader(cardConnection, "System Connectivity", "Mitsubishi PLC Network Interface");

            lblConnectionBanner = new Label();
            lblConnectionBanner.Location = new Point(22, 86);
            lblConnectionBanner.Size = new Size(386, 40);
            lblConnectionBanner.TextAlign = ContentAlignment.MiddleLeft;
            lblConnectionBanner.Padding = new Padding(14, 0, 0, 0);
            lblConnectionBanner.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            lblConnectionBanner.BackColor = Color.FromArgb(254, 236, 233);
            lblConnectionBanner.ForeColor = Color.FromArgb(177, 70, 59);
            cardConnection.Controls.Add(lblConnectionBanner);

            AddCaption(cardConnection, "IP Address", new Point(22, 146));
            txtIpAddress = CreateInputBox(new Point(22, 174), "192.168.3.39");
            cardConnection.Controls.Add(txtIpAddress);

            AddCaption(cardConnection, "Port", new Point(22, 225));
            txtPort = CreateInputBox(new Point(22, 253), "3000");
            cardConnection.Controls.Add(txtPort);

            btnConnectSystem = new Button();
            btnConnectSystem.Location = new Point(22, 320);
            btnConnectSystem.Size = new Size(386, 46);
            btnConnectSystem.Text = "CONNECT SYSTEM";
            btnConnectSystem.Click += btnConnectSystem_Click;
            ApplyPrimaryButton(btnConnectSystem);
            cardConnection.Controls.Add(btnConnectSystem);

            lblConnectionMeta = new Label();
            lblConnectionMeta.Location = new Point(22, 372);
            lblConnectionMeta.Size = new Size(386, 18);
            lblConnectionMeta.ForeColor = Color.FromArgb(115, 126, 140);
            lblConnectionMeta.Text = "MX Component logical station: 0";
            cardConnection.Controls.Add(lblConnectionMeta);

            cardCoordinateX = CreateMetricCard(new Rectangle(488, 24, 280, 130), "COORDINATE X", CoordinateXRegister, Color.FromArgb(31, 108, 214));
            cardCoordinateY = CreateMetricCard(new Rectangle(790, 24, 280, 130), "COORDINATE Y", CoordinateYRegister, Color.FromArgb(44, 186, 106));
            cardCoordinateZ = CreateMetricCard(new Rectangle(1092, 24, 280, 130), "COORDINATE Z", CoordinateZRegister, Color.FromArgb(255, 140, 39));

            lblCoordinateXValue = FindMetricValueLabel(cardCoordinateX);
            lblCoordinateYValue = FindMetricValueLabel(cardCoordinateY);
            lblCoordinateZValue = FindMetricValueLabel(cardCoordinateZ);
            lblCoordinateXRaw = FindMetricRawLabel(cardCoordinateX);
            lblCoordinateYRaw = FindMetricRawLabel(cardCoordinateY);
            lblCoordinateZRaw = FindMetricRawLabel(cardCoordinateZ);

            pnlControlCanvas.Controls.Add(cardCoordinateX);
            pnlControlCanvas.Controls.Add(cardCoordinateY);
            pnlControlCanvas.Controls.Add(cardCoordinateZ);

            cardVelocity = CreateCardPanel(new Rectangle(488, 180, 884, 158));
            pnlControlCanvas.Controls.Add(cardVelocity);
            AddCardHeader(cardVelocity, "Process Velocity", "Target write velocity (" + VelocityRegister + ")");

            lblVelocityValue = new Label();
            lblVelocityValue.Location = new Point(22, 84);
            lblVelocityValue.Size = new Size(180, 40);
            lblVelocityValue.Font = new Font("Segoe UI", 28F, FontStyle.Bold);
            lblVelocityValue.Text = "1.5";
            cardVelocity.Controls.Add(lblVelocityValue);

            lblVelocityRaw = new Label();
            lblVelocityRaw.Location = new Point(24, 120);
            lblVelocityRaw.Size = new Size(180, 22);
            lblVelocityRaw.ForeColor = Color.FromArgb(115, 126, 140);
            cardVelocity.Controls.Add(lblVelocityRaw);

            trkVelocity = new TrackBar();
            trkVelocity.Location = new Point(200, 84);
            trkVelocity.Size = new Size(660, 45);
            trkVelocity.Minimum = 0;
            trkVelocity.Maximum = 50;
            trkVelocity.TickFrequency = 5;
            trkVelocity.Value = 15;
            trkVelocity.AutoSize = false;
            trkVelocity.Height = 40;
            trkVelocity.Scroll += trkVelocity_Scroll;
            trkVelocity.MouseUp += trkVelocity_MouseUp;
            cardVelocity.Controls.Add(trkVelocity);

            cardIntegrity = CreateCardPanel(new Rectangle(1394, 180, 118, 158));
            pnlControlCanvas.Controls.Add(cardIntegrity);
            AddCardHeader(cardIntegrity, "System Integrity", string.Empty);

            lblIntegrityState = new Label();
            lblIntegrityState.Location = new Point(20, 74);
            lblIntegrityState.Size = new Size(78, 40);
            lblIntegrityState.Font = new Font("Segoe UI", 12F, FontStyle.Bold);
            lblIntegrityState.TextAlign = ContentAlignment.MiddleCenter;
            lblIntegrityState.Text = "IDLE";
            cardIntegrity.Controls.Add(lblIntegrityState);

            lblIntegrityDetail = new Label();
            lblIntegrityDetail.Location = new Point(18, 112);
            lblIntegrityDetail.Size = new Size(82, 30);
            lblIntegrityDetail.TextAlign = ContentAlignment.MiddleCenter;
            lblIntegrityDetail.ForeColor = Color.FromArgb(115, 126, 140);
            lblIntegrityDetail.Text = "STOP";
            cardIntegrity.Controls.Add(lblIntegrityDetail);

            cardJog = CreateCardPanel(new Rectangle(28, 446, 430, 322));
            pnlControlCanvas.Controls.Add(cardJog);
            AddCardHeader(cardJog, "Manual Kinematic Jog", "Direct M-register pulse control");

            Panel jogPad = new Panel();
            jogPad.Location = new Point(88, 92);
            jogPad.Size = new Size(188, 188);
            jogPad.BackColor = Color.FromArgb(240, 245, 253);
            jogPad.BorderStyle = BorderStyle.FixedSingle;
            cardJog.Controls.Add(jogPad);

            btnJogUp = CreateJogButton("↑", new Point(62, 10), 2);
            btnJogLeft = CreateJogButton("←", new Point(10, 62), 0);
            btnJogRight = CreateJogButton("→", new Point(114, 62), 1);
            btnJogDown = CreateJogButton("↓", new Point(62, 114), 3);
            jogPad.Controls.Add(btnJogUp);
            jogPad.Controls.Add(btnJogLeft);
            jogPad.Controls.Add(btnJogRight);
            jogPad.Controls.Add(btnJogDown);

            btnJogZUp = CreateJogButton("Z+", new Point(298, 118), 4);
            btnJogZDown = CreateJogButton("Z-", new Point(298, 192), 5);
            cardJog.Controls.Add(btnJogZUp);
            cardJog.Controls.Add(btnJogZDown);

            lblJogBase = new Label();
            lblJogBase.Location = new Point(22, 286);
            lblJogBase.Size = new Size(386, 22);
            lblJogBase.Text = "Current Jog Base Address: " + JogBaseRegister;
            lblJogBase.ForeColor = Color.FromArgb(75, 87, 104);
            lblJogBase.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            cardJog.Controls.Add(lblJogBase);

            cardMonitor = CreateCardPanel(new Rectangle(488, 364, 1024, 404));
            pnlControlCanvas.Controls.Add(cardMonitor);
            AddCardHeader(cardMonitor, "Real-Time Data Stream", "Live PLC register monitoring");

            btnAddRegister = new Button();
            btnAddRegister.Location = new Point(882, 18);
            btnAddRegister.Size = new Size(120, 28);
            btnAddRegister.Text = "+ ADD REGISTER";
            btnAddRegister.Click += btnAddRegister_Click;
            ApplyTextActionButton(btnAddRegister);
            cardMonitor.Controls.Add(btnAddRegister);

            dgvMonitor = new DataGridView();
            dgvMonitor.Location = new Point(22, 74);
            dgvMonitor.Size = new Size(980, 306);
            dgvMonitor.DataSource = monitorRows;
            dgvMonitor.AutoGenerateColumns = false;
            dgvMonitor.ReadOnly = true;
            dgvMonitor.AllowUserToAddRows = false;
            dgvMonitor.AllowUserToDeleteRows = false;
            dgvMonitor.RowHeadersVisible = false;
            dgvMonitor.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgvMonitor.Columns.Add(CreateTextColumn(nameof(MonitorRow.Register), "Register", 210));
            dgvMonitor.Columns.Add(CreateTextColumn(nameof(MonitorRow.Value), "Value", 220));
            dgvMonitor.Columns.Add(CreateTextColumn(nameof(MonitorRow.Status), "Status", 520));
            ApplyLightGridStyle(dgvMonitor);
            cardMonitor.Controls.Add(dgvMonitor);

            lblMonitorEmpty = new Label();
            lblMonitorEmpty.Location = new Point(220, 176);
            lblMonitorEmpty.Size = new Size(580, 54);
            lblMonitorEmpty.TextAlign = ContentAlignment.MiddleCenter;
            lblMonitorEmpty.ForeColor = Color.FromArgb(145, 153, 168);
            lblMonitorEmpty.Text = "No registers being monitored. Use ADD REGISTER to start live data flow.";
            cardMonitor.Controls.Add(lblMonitorEmpty);
        }

        private void BuildDxfView()
        {
            pnlDxfCanvas = new Panel();
            pnlDxfCanvas.Location = new Point(24, 24);
            pnlDxfCanvas.Size = new Size(1540, 840);
            pnlDxfCanvas.BackColor = Color.Transparent;
            pnlViewDxf.Controls.Add(pnlDxfCanvas);

            lblCadPathCaption = CreateSectionCaption("CAD path", new Rectangle(20, 18, 470, 34));
            lblCadFileCaption = CreateSectionCaption("File name", new Rectangle(504, 18, 170, 34));
            pnlDxfCanvas.Controls.Add(lblCadPathCaption);
            pnlDxfCanvas.Controls.Add(lblCadFileCaption);

            txtCadPath = CreateReadOnlyInput(new Rectangle(20, 56, 470, 34));
            txtCadFileName = CreateReadOnlyInput(new Rectangle(504, 56, 170, 34));
            pnlDxfCanvas.Controls.Add(txtCadPath);
            pnlDxfCanvas.Controls.Add(txtCadFileName);

            btnOpenCad = new Button();
            btnOpenCad.Location = new Point(686, 18);
            btnOpenCad.Size = new Size(112, 72);
            btnOpenCad.Text = "Open";
            btnOpenCad.Click += btnOpenCad_Click;
            ApplyPrimaryButton(btnOpenCad);
            pnlDxfCanvas.Controls.Add(btnOpenCad);

            cardCadPreview = CreateDarkCardPanel(new Rectangle(20, 106, 1018, 642));
            pnlDxfCanvas.Controls.Add(cardCadPreview);

            lblCadHint = new Label();
            lblCadHint.Location = new Point(24, 20);
            lblCadHint.Size = new Size(410, 130);
            lblCadHint.BackColor = Color.FromArgb(101, 197, 235);
            lblCadHint.Padding = new Padding(12);
            lblCadHint.Font = new Font("Segoe UI", 10F);
            lblCadHint.Text = "Load DXF, select intersection points, then mark start, glue start, and glue end." + Environment.NewLine + Environment.NewLine + "These point selections are stored in the process table for later PLC mapping.";
            cardCadPreview.Controls.Add(lblCadHint);

            lblZHint = new Label();
            lblZHint.Location = new Point(484, 20);
            lblZHint.Size = new Size(420, 70);
            lblZHint.BackColor = Color.FromArgb(101, 197, 235);
            lblZHint.Padding = new Padding(12);
            lblZHint.Font = new Font("Segoe UI", 10F);
            lblZHint.Text = "Z down is the glue dispense plane." + Environment.NewLine + "Z safe is the retract plane.";
            cardCadPreview.Controls.Add(lblZHint);

            pnlCadViewport = new Panel();
            pnlCadViewport.Location = new Point(24, 166);
            pnlCadViewport.Size = new Size(970, 450);
            pnlCadViewport.BackColor = Color.FromArgb(31, 108, 141);
            pnlCadViewport.Paint += pnlCadViewport_Paint;
            pnlCadViewport.MouseClick += pnlCadViewport_MouseClick;
            SetDoubleBuffered(pnlCadViewport);
            cardCadPreview.Controls.Add(pnlCadViewport);

            cardCadPoints = CreateDarkCardPanel(new Rectangle(1058, 106, 462, 344));
            pnlDxfCanvas.Controls.Add(cardCadPoints);

            dgvCadPoints = new DataGridView();
            dgvCadPoints.Location = new Point(12, 12);
            dgvCadPoints.Size = new Size(438, 270);
            dgvCadPoints.ReadOnly = true;
            dgvCadPoints.AllowUserToAddRows = false;
            dgvCadPoints.AllowUserToDeleteRows = false;
            dgvCadPoints.RowHeadersVisible = false;
            dgvCadPoints.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgvCadPoints.AutoGenerateColumns = false;
            dgvCadPoints.SelectionChanged += dgvCadPoints_SelectionChanged;
            cardCadPoints.Controls.Add(dgvCadPoints);

            lblPointTableTitle = new Label();
            lblPointTableTitle.Location = new Point(12, 292);
            lblPointTableTitle.Size = new Size(438, 28);
            lblPointTableTitle.TextAlign = ContentAlignment.MiddleCenter;
            lblPointTableTitle.ForeColor = Color.White;
            lblPointTableTitle.Text = "CAD point coordinates";
            cardCadPoints.Controls.Add(lblPointTableTitle);

            cardProcess = CreateDarkCardPanel(new Rectangle(1058, 462, 462, 286));
            pnlDxfCanvas.Controls.Add(cardProcess);

            dgvProcess = new DataGridView();
            dgvProcess.Location = new Point(0, 0);
            dgvProcess.Size = new Size(460, 284);
            dgvProcess.ReadOnly = true;
            dgvProcess.AllowUserToAddRows = false;
            dgvProcess.AllowUserToDeleteRows = false;
            dgvProcess.RowHeadersVisible = false;
            dgvProcess.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgvProcess.AutoGenerateColumns = false;
            cardProcess.Controls.Add(dgvProcess);

            btnAssignStart = CreateBottomActionButton("Start Point", new Rectangle(20, 760, 146, 46), btnAssignStart_Click);
            btnAssignGlueStart = CreateBottomActionButton("Glue Start", new Rectangle(180, 760, 146, 46), btnAssignGlueStart_Click);
            btnAssignGlueEnd = CreateBottomActionButton("Glue End", new Rectangle(340, 760, 146, 46), btnAssignGlueEnd_Click);
            btnSetZDown = CreateBottomActionButton("Z Down", new Rectangle(520, 760, 120, 46), btnSetZDown_Click);
            btnSetZSafe = CreateBottomActionButton("Z Safe", new Rectangle(654, 760, 120, 46), btnSetZSafe_Click);
            btnSetSpeed = CreateBottomActionButton("Speed", new Rectangle(788, 760, 120, 46), btnSetSpeed_Click);
            btnResume = CreateBottomActionButton("Resume", new Rectangle(1160, 760, 100, 46), btnResume_Click);
            btnPause = CreateBottomActionButton("Pause", new Rectangle(1280, 760, 100, 46), btnPause_Click);
            btnStart = CreateBottomActionButton("Start", new Rectangle(1400, 760, 100, 46), btnStart_Click);

            pnlDxfCanvas.Controls.Add(btnAssignStart);
            pnlDxfCanvas.Controls.Add(btnAssignGlueStart);
            pnlDxfCanvas.Controls.Add(btnAssignGlueEnd);
            pnlDxfCanvas.Controls.Add(btnSetZDown);
            pnlDxfCanvas.Controls.Add(btnSetZSafe);
            pnlDxfCanvas.Controls.Add(btnSetSpeed);
            pnlDxfCanvas.Controls.Add(btnResume);
            pnlDxfCanvas.Controls.Add(btnPause);
            pnlDxfCanvas.Controls.Add(btnStart);
        }

        private void InitializeDashboardData()
        {
            UpdateVelocityDisplay();
            UpdateCoordinateCards(0, 0, 0);
            UpdateIntegrityState(false);
            UpdateMonitorEmptyState();
        }

        private void InitializeDxfData()
        {
            ConfigureCadPointGrid();
            ConfigureProcessGrid();
            InitializeProcessRows();
        }

        private Button CreateHeaderNavButton(string text, int left, EventHandler clickHandler)
        {
            Button button = new Button();
            button.Location = new Point(left, 20);
            button.Size = new Size(110, 36);
            button.Text = text;
            button.Tag = text;
            button.Click += clickHandler;
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.BackColor = Color.White;
            button.ForeColor = Color.FromArgb(95, 104, 119);
            button.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            return button;
        }

        private Button CreateHeaderIconButton(string text, int left)
        {
            Button button = new Button();
            button.Location = new Point(left, 16);
            button.Size = new Size(42, 42);
            button.Text = text;
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderColor = Color.FromArgb(214, 223, 235);
            button.BackColor = Color.White;
            button.ForeColor = Color.FromArgb(83, 92, 108);
            button.Enabled = false;
            return button;
        }

        private Button CreateSidebarButton(string text, int top, bool active, EventHandler clickHandler)
        {
            Button button = new Button();
            button.Location = new Point(0, top);
            button.Size = new Size(220, 48);
            button.Text = "    " + text;
            button.TextAlign = ContentAlignment.MiddleLeft;
            button.Tag = text;
            button.Click += clickHandler;
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.BackColor = active ? Color.FromArgb(245, 249, 255) : Color.White;
            button.ForeColor = active ? Color.FromArgb(31, 108, 214) : Color.FromArgb(84, 93, 109);
            button.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            return button;
        }

        private Button CreateSidebarFooterButton(string text, int top)
        {
            Button button = new Button();
            button.Location = new Point(18, top);
            button.Size = new Size(184, 32);
            button.Text = text;
            button.TextAlign = ContentAlignment.MiddleLeft;
            button.Enabled = false;
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.BackColor = Color.White;
            button.ForeColor = Color.FromArgb(110, 120, 136);
            button.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            return button;
        }

        private Panel CreateCardPanel(Rectangle bounds)
        {
            Panel panel = new Panel();
            panel.Location = bounds.Location;
            panel.Size = bounds.Size;
            panel.BackColor = Color.White;
            panel.BorderStyle = BorderStyle.FixedSingle;
            return panel;
        }

        private Panel CreateDarkCardPanel(Rectangle bounds)
        {
            Panel panel = new Panel();
            panel.Location = bounds.Location;
            panel.Size = bounds.Size;
            panel.BackColor = Color.FromArgb(31, 108, 141);
            panel.BorderStyle = BorderStyle.FixedSingle;
            return panel;
        }

        private void AddCardHeader(Control parent, string title, string subtitle)
        {
            Label titleLabel = new Label();
            titleLabel.Location = new Point(22, 16);
            titleLabel.Size = new Size(parent.Width - 44, 24);
            titleLabel.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
            titleLabel.Text = title;
            parent.Controls.Add(titleLabel);

            if (!string.IsNullOrWhiteSpace(subtitle))
            {
                Label subtitleLabel = new Label();
                subtitleLabel.Location = new Point(22, 42);
                subtitleLabel.Size = new Size(parent.Width - 44, 20);
                subtitleLabel.ForeColor = Color.FromArgb(115, 126, 140);
                subtitleLabel.Text = subtitle;
                parent.Controls.Add(subtitleLabel);
            }
        }

        private void AddCaption(Control parent, string text, Point location)
        {
            Label label = new Label();
            label.Location = location;
            label.Size = new Size(160, 22);
            label.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            label.Text = text;
            parent.Controls.Add(label);
        }

        private TextBox CreateInputBox(Point location, string defaultValue)
        {
            TextBox textBox = new TextBox();
            textBox.Location = location;
            textBox.Size = new Size(386, 30);
            textBox.Text = defaultValue;
            textBox.BackColor = Color.FromArgb(237, 244, 255);
            textBox.BorderStyle = BorderStyle.FixedSingle;
            return textBox;
        }

        private Panel CreateMetricCard(Rectangle bounds, string title, string register, Color accentColor)
        {
            Panel panel = CreateCardPanel(bounds);

            Panel accent = new Panel();
            accent.Location = new Point(0, 0);
            accent.Size = new Size(5, bounds.Height);
            accent.BackColor = accentColor;
            panel.Controls.Add(accent);

            Label titleLabel = new Label();
            titleLabel.Location = new Point(20, 16);
            titleLabel.Size = new Size(200, 22);
            titleLabel.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            titleLabel.ForeColor = Color.FromArgb(95, 104, 119);
            titleLabel.Text = title;
            panel.Controls.Add(titleLabel);

            Label valueLabel = new Label();
            valueLabel.Location = new Point(20, 40);
            valueLabel.Size = new Size(220, 42);
            valueLabel.Font = new Font("Segoe UI", 28F, FontStyle.Bold);
            valueLabel.Text = "0.00";
            valueLabel.Name = "value";
            panel.Controls.Add(valueLabel);

            Label unitLabel = new Label();
            unitLabel.Location = new Point(210, 56);
            unitLabel.Size = new Size(48, 20);
            unitLabel.ForeColor = Color.FromArgb(95, 104, 119);
            unitLabel.Text = "um";
            panel.Controls.Add(unitLabel);

            Label rawLabel = new Label();
            rawLabel.Location = new Point(22, 92);
            rawLabel.Size = new Size(240, 20);
            rawLabel.ForeColor = Color.FromArgb(115, 126, 140);
            rawLabel.Text = "Raw: 0 (" + register + ")";
            rawLabel.Name = "raw";
            panel.Controls.Add(rawLabel);

            return panel;
        }

        private Label FindMetricValueLabel(Control card)
        {
            return card.Controls.OfType<Label>().First(l => l.Name == "value");
        }

        private Label FindMetricRawLabel(Control card)
        {
            return card.Controls.OfType<Label>().First(l => l.Name == "raw");
        }

        private Button CreateJogButton(string text, Point location, int offset)
        {
            Button button = new Button();
            button.Location = location;
            button.Size = new Size(64, 52);
            button.Text = text;
            button.Tag = offset;
            button.MouseDown += btnJog_MouseDown;
            button.MouseUp += btnJog_MouseUp;
            button.MouseLeave += btnJog_MouseUp;
            ApplySecondaryButton(button);
            return button;
        }

        private Label CreateSectionCaption(string text, Rectangle bounds)
        {
            Label label = new Label();
            label.Location = bounds.Location;
            label.Size = bounds.Size;
            label.BackColor = Color.FromArgb(28, 96, 128);
            label.ForeColor = Color.White;
            label.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
            label.TextAlign = ContentAlignment.MiddleCenter;
            label.Text = text;
            return label;
        }

        private TextBox CreateReadOnlyInput(Rectangle bounds)
        {
            TextBox textBox = new TextBox();
            textBox.Location = bounds.Location;
            textBox.Size = bounds.Size;
            textBox.ReadOnly = true;
            textBox.BackColor = Color.White;
            textBox.BorderStyle = BorderStyle.FixedSingle;
            return textBox;
        }

        private Button CreateBottomActionButton(string text, Rectangle bounds, EventHandler clickHandler)
        {
            Button button = new Button();
            button.Location = bounds.Location;
            button.Size = bounds.Size;
            button.Text = text;
            button.Click += clickHandler;
            ApplyPrimaryButton(button);
            return button;
        }

        private void ApplyPrimaryButton(Button button)
        {
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.BackColor = Color.FromArgb(31, 108, 214);
            button.ForeColor = Color.White;
            button.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
        }

        private void ApplySecondaryButton(Button button)
        {
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderColor = Color.FromArgb(211, 223, 240);
            button.BackColor = Color.FromArgb(233, 241, 254);
            button.ForeColor = Color.FromArgb(33, 44, 63);
            button.Font = new Font("Segoe UI", 12F, FontStyle.Bold);
        }

        private void ApplyTextActionButton(Button button)
        {
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.BackColor = Color.White;
            button.ForeColor = Color.FromArgb(73, 82, 98);
            button.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        }

        private void ApplyDangerButton(Button button)
        {
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.BackColor = Color.FromArgb(204, 43, 43);
            button.ForeColor = Color.White;
            button.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
        }

        private void ApplyLightGridStyle(DataGridView grid)
        {
            grid.BackgroundColor = Color.White;
            grid.BorderStyle = BorderStyle.None;
            grid.EnableHeadersVisualStyles = false;
            grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
            grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            grid.GridColor = Color.FromArgb(228, 233, 241);
            grid.RowTemplate.Height = 30;
            grid.DefaultCellStyle.BackColor = Color.White;
            grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(247, 250, 255);
            grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(227, 238, 255);
            grid.DefaultCellStyle.SelectionForeColor = Color.Black;
            grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(249, 251, 255);
            grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(70, 80, 98);
            grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        }

        private void ConfigureCadPointGrid()
        {
            dgvCadPoints.DataSource = cadPointRows;
            dgvCadPoints.Columns.Clear();
            dgvCadPoints.Columns.Add(CreateTextColumn(nameof(CadPointRow.Index), "STT", 58));
            dgvCadPoints.Columns.Add(CreateTextColumn(nameof(CadPointRow.LineType), "Line type", 180));
            dgvCadPoints.Columns.Add(CreateTextColumn(nameof(CadPointRow.XDisplay), "X", 92));
            dgvCadPoints.Columns.Add(CreateTextColumn(nameof(CadPointRow.YDisplay), "Y", 92));
            ApplyLightGridStyle(dgvCadPoints);
        }

        private void ConfigureProcessGrid()
        {
            dgvProcess.DataSource = processRows;
            dgvProcess.Columns.Clear();
            dgvProcess.Columns.Add(CreateTextColumn(nameof(ProcessRow.MotionType), "Motion type", 132));
            dgvProcess.Columns.Add(CreateTextColumn(nameof(ProcessRow.MCodeValue), "M code", 74));
            dgvProcess.Columns.Add(CreateTextColumn(nameof(ProcessRow.Dwell), "Dwell", 70));
            dgvProcess.Columns.Add(CreateTextColumn(nameof(ProcessRow.Speed), "Speed", 70));
            dgvProcess.Columns.Add(CreateTextColumn(nameof(ProcessRow.EndCoordinate), "End point", 98));
            dgvProcess.Columns.Add(CreateTextColumn(nameof(ProcessRow.CenterCoordinate), "Center", 98));
            ApplyLightGridStyle(dgvProcess);
        }

        private static DataGridViewTextBoxColumn CreateTextColumn(string dataPropertyName, string headerText, int width)
        {
            DataGridViewTextBoxColumn column = new DataGridViewTextBoxColumn();
            column.DataPropertyName = dataPropertyName;
            column.HeaderText = headerText;
            column.Width = width;
            column.SortMode = DataGridViewColumnSortMode.NotSortable;
            return column;
        }

        private void InitializeProcessRows()
        {
            processRows.Clear();
            startPointRow = new ProcessRow { MotionType = "Start point" };
            glueStartRow = new ProcessRow { MotionType = "Glue start", MCodeValue = "Glue on" };
            glueEndRow = new ProcessRow { MotionType = "Glue end", MCodeValue = "Glue off" };
            zDownRow = new ProcessRow { MotionType = "Z down" };
            zSafeRow = new ProcessRow { MotionType = "Z safe" };
            speedRow = new ProcessRow { MotionType = "Speed" };
            processRows.Add(startPointRow);
            processRows.Add(glueStartRow);
            processRows.Add(glueEndRow);
            processRows.Add(zDownRow);
            processRows.Add(zSafeRow);
            processRows.Add(speedRow);
        }

        private void SwitchView(AppView view)
        {
            currentView = view;
            pnlViewControl.Visible = view == AppView.Control;
            pnlViewDxf.Visible = view == AppView.DxfRun;
            UpdateNavigationState();
        }

        private void UpdateNavigationState()
        {
            UpdateNavButton(btnHeaderDashboard, currentView == AppView.Control);
            UpdateNavButton(btnHeaderDxfRun, currentView == AppView.DxfRun);
            UpdateSidebarButton(btnSideControl, currentView == AppView.Control);
            UpdateSidebarButton(btnSideDxfRun, currentView == AppView.DxfRun);
        }

        private static void UpdateNavButton(Button button, bool active)
        {
            button.ForeColor = active ? Color.FromArgb(31, 108, 214) : Color.FromArgb(95, 104, 119);
            button.BackColor = Color.White;
        }

        private static void UpdateSidebarButton(Button button, bool active)
        {
            button.BackColor = active ? Color.FromArgb(245, 249, 255) : Color.White;
            button.ForeColor = active ? Color.FromArgb(31, 108, 214) : Color.FromArgb(84, 93, 109);
        }

        private void SwitchToControlView(object sender, EventArgs e)
        {
            SwitchView(AppView.Control);
        }

        private void SwitchToDxfView(object sender, EventArgs e)
        {
            SwitchView(AppView.DxfRun);
        }

        private void ShowPlaceholderSection(object sender, EventArgs e)
        {
            MessageBox.Show("This section is a layout placeholder. CONTROL and DXF RUN are currently active.", "Section", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void btnConnectSystem_Click(object sender, EventArgs e)
        {
            if (plcComm != null && plcComm.IsConnected)
            {
                DisconnectPlc();
                return;
            }

            try
            {
                DisconnectPlc(false);

                string ipAddress = txtIpAddress.Text.Trim();
                int port = 2000;
                int.TryParse(txtPort.Text.Trim(), out port);
                if (port <= 0)
                {
                    port = 2000;
                }

                plcComm = new PLCCommunication(ipAddress, port);
                if (plcComm.Connect())
                {
                    UpdateConnectionState(true, "PLC connected");
                    UpdateIntegrityState(true);
                    plcPollTimer.Start();
                }
                else
                {
                    UpdateConnectionState(false, "PLC disconnected");
                    MessageBox.Show("PLC connect returned an error.", "PLC", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            catch (Exception ex)
            {
                UpdateConnectionState(false, "PLC disconnected");
                UpdateIntegrityFault(ex.Message);
                MessageBox.Show("PLC connection failed: " + ex.Message, "PLC", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void DisconnectPlc(bool updateUi = true)
        {
            plcPollTimer.Stop();

            if (plcComm != null)
            {
                try
                {
                    plcComm.Dispose();
                }
                catch
                {
                }

                plcComm = null;
            }

            if (updateUi)
            {
                UpdateConnectionState(false, "PLC disconnected");
                UpdateIntegrityState(false);
            }
        }

        private void UpdateConnectionState(bool connected, string bannerText)
        {
            lblConnectionBanner.Text = connected ? "  " + bannerText.ToUpperInvariant() : "  " + bannerText.ToUpperInvariant();
            lblConnectionBanner.BackColor = connected ? Color.FromArgb(231, 249, 236) : Color.FromArgb(254, 236, 233);
            lblConnectionBanner.ForeColor = connected ? Color.FromArgb(52, 131, 74) : Color.FromArgb(177, 70, 59);
            btnConnectSystem.Text = connected ? "DISCONNECT SYSTEM" : "CONNECT SYSTEM";
            lblUnitSubtitle.Text = connected ? "PLC connected" : "PLC disconnected";
        }

        private void PlcPollTimer_Tick(object sender, EventArgs e)
        {
            if (plcComm == null || !plcComm.IsConnected)
            {
                return;
            }

            try
            {
                int coordX = plcComm.ReadDeviceValue(CoordinateXRegister);
                int coordY = plcComm.ReadDeviceValue(CoordinateYRegister);
                int coordZ = plcComm.ReadDeviceValue(CoordinateZRegister);

                UpdateCoordinateCards(coordX, coordY, coordZ);
                UpdateIntegrityState(true);

                foreach (MonitorRow row in monitorRows)
                {
                    int value = plcComm.ReadDeviceValue(row.Register);
                    row.Value = value.ToString(CultureInfo.InvariantCulture);
                    row.Status = "OK";
                }

                lastPollError = string.Empty;
                dgvMonitor.Refresh();
                UpdateMonitorEmptyState();
            }
            catch (Exception ex)
            {
                lastPollError = ex.Message;
                UpdateIntegrityFault(ex.Message);

                foreach (MonitorRow row in monitorRows)
                {
                    row.Status = ex.Message;
                }

                dgvMonitor.Refresh();
                UpdateMonitorEmptyState();
            }
        }

        private void UpdateCoordinateCards(int x, int y, int z)
        {
            lblCoordinateXValue.Text = ((double)x).ToString("0.00", CultureInfo.InvariantCulture);
            lblCoordinateYValue.Text = ((double)y).ToString("0.00", CultureInfo.InvariantCulture);
            lblCoordinateZValue.Text = ((double)z).ToString("0.00", CultureInfo.InvariantCulture);

            lblCoordinateXRaw.Text = "Raw: " + x.ToString(CultureInfo.InvariantCulture) + " (" + CoordinateXRegister + ")";
            lblCoordinateYRaw.Text = "Raw: " + y.ToString(CultureInfo.InvariantCulture) + " (" + CoordinateYRegister + ")";
            lblCoordinateZRaw.Text = "Raw: " + z.ToString(CultureInfo.InvariantCulture) + " (" + CoordinateZRegister + ")";
        }

        private void UpdateVelocityDisplay()
        {
            double displayValue = trkVelocity.Value / 10.0;
            lblVelocityValue.Text = displayValue.ToString("0.0", CultureInfo.InvariantCulture);
            lblVelocityRaw.Text = "Raw: " + trkVelocity.Value.ToString(CultureInfo.InvariantCulture) + " (" + VelocityRegister + ")";
        }

        private void trkVelocity_Scroll(object sender, EventArgs e)
        {
            UpdateVelocityDisplay();
        }

        private void trkVelocity_MouseUp(object sender, MouseEventArgs e)
        {
            if (plcComm == null || !plcComm.IsConnected)
            {
                return;
            }

            try
            {
                plcComm.WriteDeviceValue(VelocityRegister, trkVelocity.Value);
            }
            catch (Exception ex)
            {
                UpdateIntegrityFault(ex.Message);
                MessageBox.Show("Velocity write failed: " + ex.Message, "PLC", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void UpdateIntegrityState(bool connected)
        {
            lblIntegrityState.Text = connected ? "READY" : "IDLE";
            lblIntegrityDetail.Text = connected ? "RUN" : "STOP";
            lblIntegrityState.ForeColor = connected ? Color.FromArgb(44, 186, 106) : Color.FromArgb(120, 129, 144);
            lblIntegrityDetail.ForeColor = connected ? Color.FromArgb(44, 186, 106) : Color.FromArgb(120, 129, 144);
        }

        private void UpdateIntegrityFault(string errorMessage)
        {
            lblIntegrityState.Text = "FAULT";
            lblIntegrityDetail.Text = "PLC error";
            lblIntegrityState.ForeColor = Color.FromArgb(214, 74, 74);
            lblIntegrityDetail.ForeColor = Color.FromArgb(214, 74, 74);
        }

        private void btnAddRegister_Click(object sender, EventArgs e)
        {
            string register = ShowInputDialog("Add register", "Enter a PLC register to monitor:", string.Empty);
            if (string.IsNullOrWhiteSpace(register))
            {
                return;
            }

            register = register.Trim().ToUpperInvariant();
            if (monitorRows.Any(r => string.Equals(r.Register, register, StringComparison.OrdinalIgnoreCase)))
            {
                MessageBox.Show("Register already exists in monitor list.", "Monitor", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            monitorRows.Add(new MonitorRow { Register = register, Value = "-", Status = "Pending" });
            UpdateMonitorEmptyState();
        }

        private void UpdateMonitorEmptyState()
        {
            lblMonitorEmpty.Visible = monitorRows.Count == 0;
        }

        private void btnJog_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left)
            {
                return;
            }

            try
            {
                EnsureConnected();
                Button button = (Button)sender;
                int offset = Convert.ToInt32(button.Tag, CultureInfo.InvariantCulture);
                string register = GetSequentialDevice(JogBaseRegister, offset);
                plcComm.WriteDeviceValue(register, 1);
            }
            catch (Exception ex)
            {
                UpdateIntegrityFault(ex.Message);
                MessageBox.Show("Jog command failed: " + ex.Message, "PLC", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnJog_MouseUp(object sender, EventArgs e)
        {
            if (plcComm == null || !plcComm.IsConnected)
            {
                return;
            }

            try
            {
                Button button = (Button)sender;
                int offset = Convert.ToInt32(button.Tag, CultureInfo.InvariantCulture);
                string register = GetSequentialDevice(JogBaseRegister, offset);
                plcComm.WriteDeviceValue(register, 0);
            }
            catch
            {
            }
        }

        private void btnEmergencyStop_Click(object sender, EventArgs e)
        {
            try
            {
                EnsureConnected();
                plcComm.WriteDeviceValue(EmergencyStopRegister, 1);
                UpdateIntegrityFault("Emergency stop triggered");
                MessageBox.Show("Emergency stop bit was written to " + EmergencyStopRegister + ".", "PLC", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            catch (Exception ex)
            {
                UpdateIntegrityFault(ex.Message);
                MessageBox.Show("Emergency stop failed: " + ex.Message, "PLC", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void EnsureConnected()
        {
            if (plcComm == null || !plcComm.IsConnected)
            {
                throw new InvalidOperationException("PLC is not connected.");
            }
        }

        private static string GetSequentialDevice(string baseDevice, int offset)
        {
            System.Text.RegularExpressions.Match match = System.Text.RegularExpressions.Regex.Match(baseDevice, @"^(?<prefix>[A-Za-z]+)(?<address>\d+)$");
            if (!match.Success)
            {
                throw new InvalidOperationException("Invalid base device: " + baseDevice);
            }

            string prefix = match.Groups["prefix"].Value;
            int address = int.Parse(match.Groups["address"].Value, CultureInfo.InvariantCulture);
            return prefix + (address + offset).ToString(CultureInfo.InvariantCulture);
        }

        private void btnOpenCad_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Filter = "DXF files (*.dxf)|*.dxf|All files (*.*)|*.*";
                dialog.Title = "Open DXF file";
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    LoadCadDocument(dialog.FileName);
                }
            }
        }

        private void LoadCadDocument(string filePath)
        {
            Cursor previousCursor = Cursor.Current;
            Cursor.Current = Cursors.WaitCursor;

            try
            {
                DxfDocument document = DxfDocument.Load(filePath);
                CadExtractionContext context = new CadExtractionContext();

                foreach (EntityObject entity in document.Entities.All)
                {
                    ExtractEntity(entity, CadTransform.Identity, context);
                }

                cadPrimitives.Clear();
                cadPrimitives.AddRange(context.Primitives);
                cadBounds = context.GetBounds();

                cadPointRows.Clear();
                foreach (CadPointRow row in context.BuildPointRows())
                {
                    cadPointRows.Add(row);
                }

                txtCadPath.Text = Path.GetDirectoryName(filePath) ?? string.Empty;
                txtCadFileName.Text = Path.GetFileName(filePath);

                ResetPointAssignments();
                SelectCadPoint(cadPointRows.FirstOrDefault(), true);
                pnlCadViewport.Invalidate();
            }
            catch (Exception ex)
            {
                cadPrimitives.Clear();
                cadPointRows.Clear();
                cadBounds = RectangleF.Empty;
                ResetPointAssignments();
                SelectCadPoint(null, true);
                pnlCadViewport.Invalidate();
                MessageBox.Show("Could not read DXF file: " + ex.Message, "DXF", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                Cursor.Current = previousCursor;
            }
        }

        private void ExtractEntity(EntityObject entity, CadTransform transform, CadExtractionContext context)
        {
            if (entity == null || !entity.IsVisible)
            {
                return;
            }

            Line line = entity as Line;
            if (line != null)
            {
                PointF start = transform.Apply(line.StartPoint);
                PointF end = transform.Apply(line.EndPoint);
                context.AddPrimitive("Line", new[] { start, end }, false);
                context.AddLinearSegment(start, end, "Line");
                context.AddCandidatePoint(start, "Line point", "Line", 1);
                context.AddCandidatePoint(end, "Line point", "Line", 1);
                return;
            }

            Polyline2D polyline2D = entity as Polyline2D;
            if (polyline2D != null)
            {
                List<PointF> points = polyline2D.Vertexes.Select(v => transform.Apply(v.Position.X, v.Position.Y)).ToList();
                AddPolylineGeometry(context, points, polyline2D.IsClosed, "Polyline2D");
                return;
            }

            Polyline3D polyline3D = entity as Polyline3D;
            if (polyline3D != null)
            {
                List<PointF> points = polyline3D.Vertexes.Select(transform.Apply).ToList();
                AddPolylineGeometry(context, points, polyline3D.IsClosed, "Polyline3D");
                return;
            }

            Arc arc = entity as Arc;
            if (arc != null)
            {
                List<PointF> arcPoints = SampleArc(arc, transform);
                context.AddPrimitive("Arc", arcPoints, false);
                if (arcPoints.Count > 0)
                {
                    context.AddCandidatePoint(arcPoints[0], "Arc start", "Arc", 2);
                    context.AddCandidatePoint(arcPoints[arcPoints.Count - 1], "Arc end", "Arc", 2);
                }
                return;
            }

            Circle circle = entity as Circle;
            if (circle != null)
            {
                context.AddPrimitive("Circle", SampleCircle(circle, transform), true);
                context.AddCandidatePoint(transform.Apply(circle.Center), "Circle center", "Circle", 3);
                return;
            }

            Insert insert = entity as Insert;
            if (insert != null && insert.Block != null)
            {
                CadTransform child = transform.Append(CadTransform.FromInsert(insert));
                foreach (EntityObject childEntity in insert.Block.Entities)
                {
                    ExtractEntity(childEntity, child, context);
                }
            }
        }

        private void AddPolylineGeometry(CadExtractionContext context, List<PointF> points, bool isClosed, string sourceType)
        {
            if (points.Count == 0)
            {
                return;
            }

            context.AddPrimitive(sourceType, points, isClosed);

            for (int i = 0; i < points.Count - 1; i++)
            {
                context.AddLinearSegment(points[i], points[i + 1], sourceType);
            }

            if (isClosed && points.Count > 2)
            {
                context.AddLinearSegment(points[points.Count - 1], points[0], sourceType);
            }

            foreach (PointF point in points)
            {
                context.AddCandidatePoint(point, "Polyline vertex", sourceType, 1);
            }
        }

        private static List<PointF> SampleArc(Arc arc, CadTransform transform)
        {
            List<PointF> points = new List<PointF>();
            double startAngle = NormalizeAngle(arc.StartAngle);
            double endAngle = NormalizeAngle(arc.EndAngle);
            double sweep = endAngle - startAngle;
            if (sweep <= 0)
            {
                sweep += 360.0;
            }

            int steps = Math.Max(18, (int)Math.Ceiling(sweep / 10.0));
            for (int i = 0; i <= steps; i++)
            {
                double angle = startAngle + sweep * i / steps;
                double radians = angle * Math.PI / 180.0;
                double x = arc.Center.X + arc.Radius * Math.Cos(radians);
                double y = arc.Center.Y + arc.Radius * Math.Sin(radians);
                points.Add(transform.Apply(x, y));
            }

            return points;
        }

        private static List<PointF> SampleCircle(Circle circle, CadTransform transform)
        {
            List<PointF> points = new List<PointF>();
            const int steps = 72;

            for (int i = 0; i <= steps; i++)
            {
                double angle = 360.0 * i / steps;
                double radians = angle * Math.PI / 180.0;
                double x = circle.Center.X + circle.Radius * Math.Cos(radians);
                double y = circle.Center.Y + circle.Radius * Math.Sin(radians);
                points.Add(transform.Apply(x, y));
            }

            return points;
        }

        private static double NormalizeAngle(double angle)
        {
            while (angle < 0) angle += 360.0;
            while (angle >= 360.0) angle -= 360.0;
            return angle;
        }

        private void pnlCadViewport_Paint(object sender, PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.Clear(pnlCadViewport.BackColor);

            if (cadPrimitives.Count == 0)
            {
                DrawViewportPlaceholder(e.Graphics);
                return;
            }

            using (Pen primitivePen = new Pen(Color.FromArgb(210, 244, 255), 1.5f))
            {
                foreach (CadPrimitive primitive in cadPrimitives)
                {
                    if (primitive.Points.Count < 2)
                    {
                        continue;
                    }

                    PointF[] points = primitive.Points.Select(WorldToScreen).ToArray();
                    e.Graphics.DrawLines(primitivePen, points);
                }
            }

            DrawPointMarkers(e.Graphics);
        }

        private void DrawViewportPlaceholder(Graphics graphics)
        {
            using (SolidBrush brush = new SolidBrush(Color.White))
            using (Font font = new Font("Segoe UI", 22F))
            using (StringFormat format = new StringFormat())
            {
                format.Alignment = StringAlignment.Center;
                format.LineAlignment = StringAlignment.Center;
                graphics.DrawString("View CAD", font, brush, pnlCadViewport.ClientRectangle, format);
            }
        }

        private void DrawPointMarkers(Graphics graphics)
        {
            foreach (CadPointRow point in cadPointRows)
            {
                PointF screen = WorldToScreen(point.ModelPoint);
                RectangleF rect = new RectangleF(screen.X - 4, screen.Y - 4, 8, 8);
                using (SolidBrush brush = new SolidBrush(Color.FromArgb(255, 249, 189)))
                {
                    graphics.FillEllipse(brush, rect);
                }
                using (Pen pen = new Pen(Color.FromArgb(19, 60, 80), 1f))
                {
                    graphics.DrawEllipse(pen, rect);
                }
            }

            foreach (KeyValuePair<AssignmentSlot, CadPointRow> entry in assignedPoints)
            {
                DrawAssignedMarker(graphics, entry.Key, entry.Value);
            }

            if (selectedPoint != null)
            {
                PointF screen = WorldToScreen(selectedPoint.ModelPoint);
                using (Pen pen = new Pen(Color.Orange, 2f))
                {
                    graphics.DrawEllipse(pen, screen.X - 8, screen.Y - 8, 16, 16);
                }
            }
        }

        private void DrawAssignedMarker(Graphics graphics, AssignmentSlot slot, CadPointRow row)
        {
            PointF screen = WorldToScreen(row.ModelPoint);
            RectangleF rect = new RectangleF(screen.X - 7, screen.Y - 7, 14, 14);
            using (SolidBrush brush = new SolidBrush(GetAssignmentColor(slot)))
            {
                graphics.FillEllipse(brush, rect);
            }
            using (Pen pen = new Pen(Color.White, 1.5f))
            {
                graphics.DrawEllipse(pen, rect);
            }
            using (SolidBrush textBrush = new SolidBrush(Color.White))
            using (Font font = new Font("Segoe UI", 7.5F, FontStyle.Bold))
            using (StringFormat format = new StringFormat())
            {
                format.Alignment = StringAlignment.Center;
                format.LineAlignment = StringAlignment.Center;
                graphics.DrawString(GetAssignmentLabel(slot), font, textBrush, rect, format);
            }
        }

        private PointF WorldToScreen(PointF worldPoint)
        {
            if (pnlCadViewport.ClientRectangle.Width <= 0 || pnlCadViewport.ClientRectangle.Height <= 0)
            {
                return PointF.Empty;
            }

            float width = Math.Max(cadBounds.Width, 1f);
            float height = Math.Max(cadBounds.Height, 1f);
            float padding = 24f;
            float scaleX = (pnlCadViewport.ClientRectangle.Width - padding * 2f) / width;
            float scaleY = (pnlCadViewport.ClientRectangle.Height - padding * 2f) / height;
            float scale = Math.Max(Math.Min(scaleX, scaleY), 0.0001f);
            float usedWidth = width * scale;
            float usedHeight = height * scale;
            float offsetX = (pnlCadViewport.ClientRectangle.Width - usedWidth) * 0.5f;
            float offsetY = (pnlCadViewport.ClientRectangle.Height - usedHeight) * 0.5f;

            float x = offsetX + (worldPoint.X - cadBounds.Left) * scale;
            float y = pnlCadViewport.ClientRectangle.Height - offsetY - (worldPoint.Y - cadBounds.Top) * scale;
            return new PointF(x, y);
        }

        private void pnlCadViewport_MouseClick(object sender, MouseEventArgs e)
        {
            if (cadPointRows.Count == 0)
            {
                return;
            }

            CadPointRow closest = null;
            double bestDistance = 14.0;

            foreach (CadPointRow row in cadPointRows)
            {
                PointF screen = WorldToScreen(row.ModelPoint);
                double distance = Math.Sqrt(Math.Pow(screen.X - e.X, 2) + Math.Pow(screen.Y - e.Y, 2));
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    closest = row;
                }
            }

            if (closest != null)
            {
                SelectCadPoint(closest, true);
            }
        }

        private void dgvCadPoints_SelectionChanged(object sender, EventArgs e)
        {
            if (suppressPointSelection)
            {
                return;
            }

            if (dgvCadPoints.CurrentRow != null)
            {
                CadPointRow row = dgvCadPoints.CurrentRow.DataBoundItem as CadPointRow;
                if (row != null)
                {
                    SelectCadPoint(row, false);
                    return;
                }
            }

            SelectCadPoint(null, false);
        }

        private void SelectCadPoint(CadPointRow row, bool syncGrid)
        {
            selectedPoint = row;

            if (syncGrid)
            {
                suppressPointSelection = true;
                try
                {
                    dgvCadPoints.ClearSelection();
                    if (row != null)
                    {
                        foreach (DataGridViewRow gridRow in dgvCadPoints.Rows)
                        {
                            if (ReferenceEquals(gridRow.DataBoundItem, row))
                            {
                                gridRow.Selected = true;
                                dgvCadPoints.CurrentCell = gridRow.Cells[0];
                                break;
                            }
                        }
                    }
                }
                finally
                {
                    suppressPointSelection = false;
                }
            }

            pnlCadViewport.Invalidate();
        }

        private void btnAssignStart_Click(object sender, EventArgs e)
        {
            AssignSelectedPoint(AssignmentSlot.Start, startPointRow);
        }

        private void btnAssignGlueStart_Click(object sender, EventArgs e)
        {
            AssignSelectedPoint(AssignmentSlot.GlueStart, glueStartRow);
        }

        private void btnAssignGlueEnd_Click(object sender, EventArgs e)
        {
            AssignSelectedPoint(AssignmentSlot.GlueEnd, glueEndRow);
        }

        private void AssignSelectedPoint(AssignmentSlot slot, ProcessRow row)
        {
            if (selectedPoint == null)
            {
                MessageBox.Show("Select a CAD point first.", "DXF", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            assignedPoints[slot] = selectedPoint;
            row.EndCoordinate = FormatPoint(selectedPoint);
            dgvProcess.Refresh();
            pnlCadViewport.Invalidate();
        }

        private void btnSetZDown_Click(object sender, EventArgs e)
        {
            string value = ShowInputDialog("Z Down", "Enter Z down value:", zDownRow.MCodeValue);
            if (value != null)
            {
                zDownRow.MCodeValue = value;
                dgvProcess.Refresh();
            }
        }

        private void btnSetZSafe_Click(object sender, EventArgs e)
        {
            string value = ShowInputDialog("Z Safe", "Enter Z safe value:", zSafeRow.MCodeValue);
            if (value != null)
            {
                zSafeRow.MCodeValue = value;
                dgvProcess.Refresh();
            }
        }

        private void btnSetSpeed_Click(object sender, EventArgs e)
        {
            string value = ShowInputDialog("Speed", "Enter process speed:", speedRow.Speed);
            if (value != null)
            {
                speedRow.Speed = value;
                dgvProcess.Refresh();
            }
        }

        private void btnResume_Click(object sender, EventArgs e)
        {
            MessageBox.Show("Resume action is available. Map the PLC variables you want to drive here.", "DXF run", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void btnPause_Click(object sender, EventArgs e)
        {
            MessageBox.Show("Pause action is available. Map the PLC variables you want to drive here.", "DXF run", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void btnStart_Click(object sender, EventArgs e)
        {
            MessageBox.Show("Start action is available. Map the PLC variables you want to drive here.", "DXF run", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void ResetPointAssignments()
        {
            assignedPoints.Clear();
            startPointRow.EndCoordinate = string.Empty;
            glueStartRow.EndCoordinate = string.Empty;
            glueEndRow.EndCoordinate = string.Empty;
            dgvProcess.Refresh();
        }

        private static string FormatPoint(CadPointRow row)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0:0.###}, {1:0.###}", row.X, row.Y);
        }

        private string ShowInputDialog(string title, string promptText, string currentValue)
        {
            using (Form dialog = new Form())
            using (Label prompt = new Label())
            using (TextBox input = new TextBox())
            using (Button ok = new Button())
            using (Button cancel = new Button())
            {
                dialog.Text = title;
                dialog.FormBorderStyle = FormBorderStyle.FixedDialog;
                dialog.StartPosition = FormStartPosition.CenterParent;
                dialog.ClientSize = new Size(400, 150);
                dialog.MaximizeBox = false;
                dialog.MinimizeBox = false;
                dialog.Font = new Font("Segoe UI", 10F);
                dialog.BackColor = Color.WhiteSmoke;

                prompt.Location = new Point(18, 16);
                prompt.Size = new Size(360, 22);
                prompt.Text = promptText;
                dialog.Controls.Add(prompt);

                input.Location = new Point(18, 46);
                input.Size = new Size(360, 30);
                input.Text = currentValue ?? string.Empty;
                dialog.Controls.Add(input);

                ok.Location = new Point(202, 98);
                ok.Size = new Size(84, 34);
                ok.Text = "OK";
                ok.DialogResult = DialogResult.OK;
                ApplyPrimaryButton(ok);
                dialog.Controls.Add(ok);

                cancel.Location = new Point(294, 98);
                cancel.Size = new Size(84, 34);
                cancel.Text = "Cancel";
                cancel.DialogResult = DialogResult.Cancel;
                ApplySecondaryButton(cancel);
                dialog.Controls.Add(cancel);

                dialog.AcceptButton = ok;
                dialog.CancelButton = cancel;

                return dialog.ShowDialog(this) == DialogResult.OK ? input.Text.Trim() : null;
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            plcPollTimer.Stop();
            if (plcComm != null)
            {
                plcComm.Dispose();
                plcComm = null;
            }

            base.OnFormClosing(e);
        }

        private static void SetDoubleBuffered(Control control)
        {
            PropertyInfo property = typeof(Control).GetProperty("DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic);
            if (property != null)
            {
                property.SetValue(control, true, null);
            }
        }

        private static Color GetAssignmentColor(AssignmentSlot slot)
        {
            switch (slot)
            {
                case AssignmentSlot.Start:
                    return Color.FromArgb(43, 190, 107);
                case AssignmentSlot.GlueStart:
                    return Color.FromArgb(255, 183, 55);
                case AssignmentSlot.GlueEnd:
                    return Color.FromArgb(244, 81, 81);
                default:
                    return Color.White;
            }
        }

        private static string GetAssignmentLabel(AssignmentSlot slot)
        {
            switch (slot)
            {
                case AssignmentSlot.Start:
                    return "S";
                case AssignmentSlot.GlueStart:
                    return "B";
                case AssignmentSlot.GlueEnd:
                    return "E";
                default:
                    return string.Empty;
            }
        }

        private enum AppView
        {
            Control,
            DxfRun
        }

        private enum AssignmentSlot
        {
            Start,
            GlueStart,
            GlueEnd
        }

        private sealed class MonitorRow
        {
            public string Register { get; set; }
            public string Value { get; set; }
            public string Status { get; set; }
        }

        private sealed class CadPointRow
        {
            public int Index { get; set; }
            public string LineType { get; set; }
            public double X { get; set; }
            public double Y { get; set; }
            public string XDisplay { get; set; }
            public string YDisplay { get; set; }
            public string Key { get; set; }

            public PointF ModelPoint
            {
                get { return new PointF((float)X, (float)Y); }
            }
        }

        private sealed class ProcessRow
        {
            public string MotionType { get; set; }
            public string MCodeValue { get; set; }
            public string Dwell { get; set; }
            public string Speed { get; set; }
            public string EndCoordinate { get; set; }
            public string CenterCoordinate { get; set; }
        }

        private sealed class CadPrimitive
        {
            public CadPrimitive(string sourceType, List<PointF> points)
            {
                SourceType = sourceType;
                Points = points;
            }

            public string SourceType { get; private set; }
            public List<PointF> Points { get; private set; }
        }

        private sealed class CadExtractionContext
        {
            private readonly Dictionary<string, CadPointAccumulator> pointAccumulators = new Dictionary<string, CadPointAccumulator>();
            private int sequence;
            private float minX = float.MaxValue;
            private float minY = float.MaxValue;
            private float maxX = float.MinValue;
            private float maxY = float.MinValue;

            public List<CadPrimitive> Primitives { get; } = new List<CadPrimitive>();
            public List<LineSegment> LineSegments { get; } = new List<LineSegment>();

            public void AddPrimitive(string sourceType, IEnumerable<PointF> points, bool closeLoop)
            {
                List<PointF> list = points.ToList();
                if (list.Count == 0)
                {
                    return;
                }

                if (closeLoop && list.Count > 2 && !AreClose(list[0], list[list.Count - 1]))
                {
                    list.Add(list[0]);
                }

                IncludeBounds(list);

                if (list.Count > 1)
                {
                    Primitives.Add(new CadPrimitive(sourceType, list));
                }
            }

            public void AddLinearSegment(PointF start, PointF end, string sourceType)
            {
                if (AreClose(start, end))
                {
                    return;
                }

                IncludeBounds(new[] { start, end });
                LineSegments.Add(new LineSegment(start, end, sourceType));
            }

            public void AddCandidatePoint(PointF point, string category, string sourceType, int priority)
            {
                IncludeBounds(new[] { point });

                string key = MakePointKey(point);
                CadPointAccumulator accumulator;
                if (!pointAccumulators.TryGetValue(key, out accumulator))
                {
                    accumulator = new CadPointAccumulator(point, category, priority, sequence++);
                    pointAccumulators.Add(key, accumulator);
                }

                accumulator.Merge(point, category, sourceType, priority);
            }

            public List<CadPointRow> BuildPointRows()
            {
                AddIntersectionPoints();

                List<CadPointAccumulator> ordered = pointAccumulators.Values
                    .OrderBy(p => p.Priority)
                    .ThenBy(p => p.Order)
                    .ThenBy(p => p.Point.X)
                    .ThenBy(p => p.Point.Y)
                    .ToList();

                List<CadPointRow> rows = new List<CadPointRow>();
                for (int i = 0; i < ordered.Count; i++)
                {
                    CadPointAccumulator point = ordered[i];
                    rows.Add(new CadPointRow
                    {
                        Index = i + 1,
                        LineType = point.DisplayType,
                        X = point.Point.X,
                        Y = point.Point.Y,
                        XDisplay = point.Point.X.ToString("0.###", CultureInfo.InvariantCulture),
                        YDisplay = point.Point.Y.ToString("0.###", CultureInfo.InvariantCulture),
                        Key = MakePointKey(point.Point)
                    });
                }

                return rows;
            }

            public RectangleF GetBounds()
            {
                if (minX == float.MaxValue)
                {
                    return new RectangleF(0, 0, 100, 100);
                }

                return RectangleF.FromLTRB(minX, minY, maxX, maxY);
            }

            private void IncludeBounds(IEnumerable<PointF> points)
            {
                foreach (PointF point in points)
                {
                    minX = Math.Min(minX, point.X);
                    minY = Math.Min(minY, point.Y);
                    maxX = Math.Max(maxX, point.X);
                    maxY = Math.Max(maxY, point.Y);
                }
            }

            private void AddIntersectionPoints()
            {
                for (int i = 0; i < LineSegments.Count; i++)
                {
                    for (int j = i + 1; j < LineSegments.Count; j++)
                    {
                        PointF intersection;
                        if (TryGetIntersection(LineSegments[i], LineSegments[j], out intersection))
                        {
                            AddCandidatePoint(intersection, "Intersection", LineSegments[i].SourceType + "/" + LineSegments[j].SourceType, 0);
                        }
                    }
                }
            }

            private static bool TryGetIntersection(LineSegment first, LineSegment second, out PointF intersection)
            {
                const double epsilon = 0.000001;

                double x1 = first.Start.X;
                double y1 = first.Start.Y;
                double x2 = first.End.X;
                double y2 = first.End.Y;
                double x3 = second.Start.X;
                double y3 = second.Start.Y;
                double x4 = second.End.X;
                double y4 = second.End.Y;

                double dx1 = x2 - x1;
                double dy1 = y2 - y1;
                double dx2 = x4 - x3;
                double dy2 = y4 - y3;
                double denominator = dx1 * dy2 - dy1 * dx2;

                if (Math.Abs(denominator) < epsilon)
                {
                    if (AreClose(first.Start, second.Start))
                    {
                        intersection = first.Start;
                        return true;
                    }

                    if (AreClose(first.Start, second.End))
                    {
                        intersection = first.Start;
                        return true;
                    }

                    if (AreClose(first.End, second.Start))
                    {
                        intersection = first.End;
                        return true;
                    }

                    if (AreClose(first.End, second.End))
                    {
                        intersection = first.End;
                        return true;
                    }

                    intersection = PointF.Empty;
                    return false;
                }

                double ua = ((x3 - x1) * dy2 - (y3 - y1) * dx2) / denominator;
                double ub = ((x3 - x1) * dy1 - (y3 - y1) * dx1) / denominator;

                if (ua < -epsilon || ua > 1 + epsilon || ub < -epsilon || ub > 1 + epsilon)
                {
                    intersection = PointF.Empty;
                    return false;
                }

                intersection = new PointF((float)(x1 + ua * dx1), (float)(y1 + ua * dy1));
                return true;
            }

            private static string MakePointKey(PointF point)
            {
                return string.Format(CultureInfo.InvariantCulture, "{0:0.###}|{1:0.###}", point.X, point.Y);
            }

            private static bool AreClose(PointF first, PointF second)
            {
                return Math.Abs(first.X - second.X) < 0.001f && Math.Abs(first.Y - second.Y) < 0.001f;
            }
        }

        private sealed class CadPointAccumulator
        {
            private readonly HashSet<string> sourceTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            public CadPointAccumulator(PointF point, string category, int priority, int order)
            {
                Point = point;
                Category = category;
                Priority = priority;
                Order = order;
            }

            public PointF Point { get; private set; }
            public string Category { get; private set; }
            public int Priority { get; private set; }
            public int Order { get; private set; }

            public string DisplayType
            {
                get
                {
                    if (sourceTypes.Count == 0)
                    {
                        return Category;
                    }

                    return Category + " (" + string.Join("/", sourceTypes.OrderBy(x => x)) + ")";
                }
            }

            public void Merge(PointF point, string category, string sourceType, int priority)
            {
                Point = point;
                if (priority < Priority)
                {
                    Category = category;
                    Priority = priority;
                }

                if (!string.IsNullOrWhiteSpace(sourceType))
                {
                    sourceTypes.Add(sourceType);
                }
            }
        }

        private struct LineSegment
        {
            public LineSegment(PointF start, PointF end, string sourceType)
            {
                Start = start;
                End = end;
                SourceType = sourceType;
            }

            public PointF Start { get; }
            public PointF End { get; }
            public string SourceType { get; }
        }

        private struct CadTransform
        {
            public static CadTransform Identity => new CadTransform(1, 0, 0, 1, 0, 0);

            public CadTransform(double a, double b, double c, double d, double tx, double ty)
            {
                A = a;
                B = b;
                C = c;
                D = d;
                Tx = tx;
                Ty = ty;
            }

            public double A { get; }
            public double B { get; }
            public double C { get; }
            public double D { get; }
            public double Tx { get; }
            public double Ty { get; }

            public PointF Apply(netDxf.Vector3 value)
            {
                return Apply(value.X, value.Y);
            }

            public PointF Apply(double x, double y)
            {
                double outX = A * x + B * y + Tx;
                double outY = C * x + D * y + Ty;
                return new PointF((float)outX, (float)outY);
            }

            public CadTransform Append(CadTransform local)
            {
                return new CadTransform(
                    A * local.A + B * local.C,
                    A * local.B + B * local.D,
                    C * local.A + D * local.C,
                    C * local.B + D * local.D,
                    A * local.Tx + B * local.Ty + Tx,
                    C * local.Tx + D * local.Ty + Ty);
            }

            public static CadTransform FromInsert(Insert insert)
            {
                double radians = insert.Rotation * Math.PI / 180.0;
                double cos = Math.Cos(radians);
                double sin = Math.Sin(radians);
                return new CadTransform(
                    cos * insert.Scale.X,
                    -sin * insert.Scale.Y,
                    sin * insert.Scale.X,
                    cos * insert.Scale.Y,
                    insert.Position.X,
                    insert.Position.Y);
            }
        }
    }
}
