using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

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

        private readonly WebView2 webView;
        private readonly Timer plcPollTimer = new Timer();
        private readonly JavaScriptSerializer serializer = new JavaScriptSerializer
        {
            MaxJsonLength = int.MaxValue,
            RecursionLimit = 256
        };
        private readonly CadDocumentService cadService = new CadDocumentService();
        private readonly List<MonitorRow> monitorRows = new List<MonitorRow>();
        private readonly List<ProcessRow> processRows = new List<ProcessRow>();
        private readonly Dictionary<string, string> assignedPointKeys = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        private PLCCommunication plcComm;
        private CadDocumentService.CadLoadResult activeCadDocument;
        private bool webReady;
        private string currentView = "control";
        private string currentTheme = "dark";
        private string plcIpAddress = "192.168.3.39";
        private int plcPort = 3000;
        private string connectionBanner = "PLC disconnected";
        private int coordinateX;
        private int coordinateY;
        private int coordinateZ;
        private int velocityValue = 15;
        private string integrityState = "IDLE";
        private string integrityDetail = "STOP";
        private string integrityTone = "idle";
        private string selectedCadPointKey;

        public Form1()
        {
            InitializeComponent();

            Text = "Gantry SCADA Robot Control";
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(1440, 860);
            WindowState = FormWindowState.Maximized;
            BackColor = Color.FromArgb(10, 15, 30);

            webView = new WebView2
            {
                Dock = DockStyle.Fill,
                DefaultBackgroundColor = Color.FromArgb(10, 15, 30)
            };
            Controls.Add(webView);
            Controls.SetChildIndex(webView, 0);

            InitializeProcessRows();
            UpdateConnectionState(false, "PLC disconnected");
            UpdateIntegrityState(false);

            plcPollTimer.Interval = 500;
            plcPollTimer.Tick += PlcPollTimer_Tick;

            Shown += async (sender, e) => await InitializeWebViewAsync();
        }

        private async Task InitializeWebViewAsync()
        {
            try
            {
                string userDataFolder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "test1",
                    "WebView2");
                Directory.CreateDirectory(userDataFolder);

                CoreWebView2Environment environment = await CoreWebView2Environment.CreateAsync(null, userDataFolder);
                await webView.EnsureCoreWebView2Async(environment);

                webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
                webView.CoreWebView2.Settings.AreDefaultScriptDialogsEnabled = true;
                webView.CoreWebView2.Settings.AreDevToolsEnabled = true;
                webView.CoreWebView2.Settings.IsStatusBarEnabled = false;
                webView.CoreWebView2.WebMessageReceived += CoreWebView2_WebMessageReceived;

                string uiPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ui", "index.html");
                webView.Source = new Uri(uiPath);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    this,
                    "Không khởi tạo được HTML dashboard. Hãy kiểm tra Microsoft Edge WebView2 Runtime." + Environment.NewLine + Environment.NewLine + ex.Message,
                    "WebView2",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private async void CoreWebView2_WebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                Dictionary<string, object> message = serializer.Deserialize<Dictionary<string, object>>(e.WebMessageAsJson);
                if (message == null)
                {
                    return;
                }

                string action = GetString(message, "action");
                Dictionary<string, object> payload = GetMap(message, "payload");

                switch (action)
                {
                    case "uiReady":
                        webReady = true;
                        await PushAllStateAsync();
                        break;

                    case "switchView":
                        currentView = GetString(payload, "view", currentView);
                        await PushAllStateAsync();
                        break;

                    case "setTheme":
                        currentTheme = GetString(payload, "theme", currentTheme);
                        await PushAllStateAsync();
                        break;

                    case "connectToggle":
                        await HandleConnectToggleAsync(payload);
                        break;

                    case "setVelocity":
                        await HandleSetVelocityAsync(GetInt(payload, "value", velocityValue));
                        break;

                    case "addRegister":
                        await HandleAddRegisterAsync(GetString(payload, "register"));
                        break;

                    case "removeRegister":
                        await HandleRemoveRegisterAsync(GetString(payload, "register"));
                        break;

                    case "jogStart":
                        await HandleJogWriteAsync(GetInt(payload, "offset", -1), true);
                        break;

                    case "jogStop":
                        await HandleJogWriteAsync(GetInt(payload, "offset", -1), false);
                        break;

                    case "emergencyStop":
                        await HandleEmergencyStopAsync();
                        break;

                    case "openDxf":
                        await HandleOpenDxfAsync();
                        break;

                    case "selectCadPoint":
                        selectedCadPointKey = GetString(payload, "key");
                        await PushDxfStateAsync();
                        break;

                    case "assignPoint":
                        await HandleAssignPointAsync(GetString(payload, "slot"), GetString(payload, "key", selectedCadPointKey));
                        break;

                    case "setProcessValue":
                        await HandleProcessValueAsync(GetString(payload, "key"), GetString(payload, "value"));
                        break;

                    case "runAction":
                        await NotifyAsync("info", "DXF RUN", "Các nút Resume, Pause, Start đã có UI HTML. Phần map biến PLC sẽ nối tiếp ở bước sau.");
                        break;
                }
            }
            catch (Exception ex)
            {
                await NotifyAsync("error", "UI bridge", ex.Message);
            }
        }

        private async Task HandleConnectToggleAsync(Dictionary<string, object> payload)
        {
            plcIpAddress = GetString(payload, "ip", plcIpAddress).Trim();
            plcPort = Math.Max(1, GetInt(payload, "port", plcPort));

            if (plcComm != null && plcComm.IsConnected)
            {
                DisconnectPlc();
                await NotifyAsync("info", "PLC", "Đã ngắt kết nối PLC.");
                await PushControlStateAsync();
                return;
            }

            try
            {
                DisconnectPlc(false);

                plcComm = new PLCCommunication(plcIpAddress, plcPort);
                if (!plcComm.Connect())
                {
                    UpdateConnectionState(false, "PLC disconnected");
                    UpdateIntegrityFault("Kết nối PLC trả về lỗi.");
                    await NotifyAsync("error", "PLC", "PLC connect trả về lỗi.");
                    await PushControlStateAsync();
                    return;
                }

                UpdateConnectionState(true, "PLC connected");
                UpdateIntegrityState(true);
                plcPollTimer.Start();
                await PushControlStateAsync();
                await NotifyAsync("success", "PLC", "Kết nối PLC thành công.");
            }
            catch (Exception ex)
            {
                UpdateConnectionState(false, "PLC disconnected");
                UpdateIntegrityFault(ex.Message);
                await PushControlStateAsync();
                await NotifyAsync("error", "PLC", ex.Message);
            }
        }

        private async Task HandleSetVelocityAsync(int value)
        {
            velocityValue = Math.Max(0, Math.Min(50, value));

            if (plcComm != null && plcComm.IsConnected)
            {
                try
                {
                    plcComm.WriteDeviceValue(VelocityRegister, velocityValue);
                    UpdateIntegrityState(true);
                }
                catch (Exception ex)
                {
                    UpdateIntegrityFault(ex.Message);
                    await NotifyAsync("error", "PLC", "Ghi tốc độ thất bại: " + ex.Message);
                }
            }

            await PushControlStateAsync();
        }

        private async Task HandleAddRegisterAsync(string register)
        {
            register = (register ?? string.Empty).Trim().ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(register))
            {
                return;
            }

            if (monitorRows.Any(row => string.Equals(row.Register, register, StringComparison.OrdinalIgnoreCase)))
            {
                await NotifyAsync("info", "Monitor", "Thanh ghi đã tồn tại trong danh sách theo dõi.");
                return;
            }

            monitorRows.Add(new MonitorRow
            {
                Register = register,
                Value = "-",
                Status = plcComm != null && plcComm.IsConnected ? "Pending" : "Disconnected"
            });
            await PushControlStateAsync();
        }

        private async Task HandleRemoveRegisterAsync(string register)
        {
            MonitorRow row = monitorRows.FirstOrDefault(item => string.Equals(item.Register, register, StringComparison.OrdinalIgnoreCase));
            if (row == null)
            {
                return;
            }

            monitorRows.Remove(row);
            await PushControlStateAsync();
        }

        private async Task HandleJogWriteAsync(int offset, bool active)
        {
            if (offset < 0)
            {
                return;
            }

            try
            {
                EnsureConnected();
                string register = GetSequentialDevice(JogBaseRegister, offset);
                plcComm.WriteDeviceValue(register, active ? 1 : 0);
                UpdateIntegrityState(true);
            }
            catch (Exception ex)
            {
                if (active)
                {
                    UpdateIntegrityFault(ex.Message);
                    await NotifyAsync("error", "Jog", ex.Message);
                    await PushControlStateAsync();
                }
            }
        }

        private async Task HandleEmergencyStopAsync()
        {
            try
            {
                EnsureConnected();
                plcComm.WriteDeviceValue(EmergencyStopRegister, 1);
                UpdateIntegrityFault("Emergency stop triggered");
                await PushControlStateAsync();
                await NotifyAsync("error", "PLC", "Đã ghi emergency stop vào " + EmergencyStopRegister + ".");
            }
            catch (Exception ex)
            {
                UpdateIntegrityFault(ex.Message);
                await PushControlStateAsync();
                await NotifyAsync("error", "PLC", ex.Message);
            }
        }

        private async Task HandleOpenDxfAsync()
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Filter = "DXF files (*.dxf)|*.dxf|All files (*.*)|*.*";
                dialog.Title = "Open DXF file";

                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                try
                {
                    LoadCadDocument(dialog.FileName);
                    currentView = "dxf";
                    await PushDxfStateAsync();
                    await NotifyAsync("success", "DXF", "Đã tải file DXF.");
                }
                catch (Exception ex)
                {
                    await NotifyAsync("error", "DXF", ex.Message);
                }
            }
        }

        private async Task HandleAssignPointAsync(string slot, string key)
        {
            if (activeCadDocument == null || activeCadDocument.Points == null || activeCadDocument.Points.Count == 0)
            {
                await NotifyAsync("info", "DXF", "Chưa có dữ liệu DXF.");
                return;
            }

            CadDocumentService.CadPointData point = activeCadDocument.Points.FirstOrDefault(item => string.Equals(item.Key, key, StringComparison.OrdinalIgnoreCase));
            if (point == null)
            {
                await NotifyAsync("info", "DXF", "Hãy chọn một điểm trước khi gán.");
                return;
            }

            ProcessRow row = GetProcessRow(slot);
            if (row == null)
            {
                return;
            }

            assignedPointKeys[slot] = point.Key;
            selectedCadPointKey = point.Key;
            row.EndCoordinate = FormatPoint(point);

            await PushDxfStateAsync();
        }

        private async Task HandleProcessValueAsync(string key, string value)
        {
            ProcessRow row = GetProcessRow(key);
            if (row == null)
            {
                return;
            }

            value = (value ?? string.Empty).Trim();

            switch (key)
            {
                case "zDown":
                case "zSafe":
                    row.MCodeValue = value;
                    break;

                case "speed":
                    row.Speed = value;
                    break;

                default:
                    row.MCodeValue = value;
                    break;
            }

            await PushDxfStateAsync();
        }

        private void LoadCadDocument(string filePath)
        {
            activeCadDocument = cadService.Load(filePath);
            selectedCadPointKey = activeCadDocument.Points.FirstOrDefault()?.Key;
            ResetPointAssignments();
        }

        private void ResetPointAssignments()
        {
            assignedPointKeys.Clear();
            GetProcessRow("start").EndCoordinate = string.Empty;
            GetProcessRow("glueStart").EndCoordinate = string.Empty;
            GetProcessRow("glueEnd").EndCoordinate = string.Empty;
        }

        private async void PlcPollTimer_Tick(object sender, EventArgs e)
        {
            if (plcComm == null || !plcComm.IsConnected)
            {
                return;
            }

            try
            {
                coordinateX = plcComm.ReadDeviceValue(CoordinateXRegister);
                coordinateY = plcComm.ReadDeviceValue(CoordinateYRegister);
                coordinateZ = plcComm.ReadDeviceValue(CoordinateZRegister);
                velocityValue = plcComm.ReadDeviceValue(VelocityRegister);
                UpdateIntegrityState(true);

                foreach (MonitorRow row in monitorRows)
                {
                    int value = plcComm.ReadDeviceValue(row.Register);
                    row.Value = value.ToString(CultureInfo.InvariantCulture);
                    row.Status = "OK";
                }
            }
            catch (Exception ex)
            {
                UpdateIntegrityFault(ex.Message);
                foreach (MonitorRow row in monitorRows)
                {
                    row.Status = ex.Message;
                }
            }

            await PushControlStateAsync();
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

            foreach (MonitorRow row in monitorRows)
            {
                row.Status = "Disconnected";
            }

            if (updateUi)
            {
                UpdateConnectionState(false, "PLC disconnected");
                UpdateIntegrityState(false);
            }
        }

        private void InitializeProcessRows()
        {
            processRows.Clear();
            processRows.Add(new ProcessRow { Key = "start", MotionType = "Điểm bắt đầu" });
            processRows.Add(new ProcessRow { Key = "glueStart", MotionType = "Điểm bắt đầu bơm", MCodeValue = "Bật keo" });
            processRows.Add(new ProcessRow { Key = "glueEnd", MotionType = "Điểm kết thúc bơm", MCodeValue = "Tắt keo" });
            processRows.Add(new ProcessRow { Key = "zDown", MotionType = "Độ cao Z hạ" });
            processRows.Add(new ProcessRow { Key = "zSafe", MotionType = "Độ cao Z an toàn" });
            processRows.Add(new ProcessRow { Key = "speed", MotionType = "Tốc độ" });
        }

        private ProcessRow GetProcessRow(string key)
        {
            return processRows.FirstOrDefault(row => string.Equals(row.Key, key, StringComparison.OrdinalIgnoreCase));
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

        private void UpdateConnectionState(bool connected, string bannerText)
        {
            connectionBanner = bannerText;
        }

        private void UpdateIntegrityState(bool connected)
        {
            integrityState = connected ? "READY" : "IDLE";
            integrityDetail = connected ? "RUN" : "STOP";
            integrityTone = connected ? "ready" : "idle";
        }

        private void UpdateIntegrityFault(string errorMessage)
        {
            integrityState = "FAULT";
            integrityDetail = string.IsNullOrWhiteSpace(errorMessage) ? "PLC error" : errorMessage;
            integrityTone = "fault";
        }

        private Task PushAllStateAsync()
        {
            return Task.WhenAll(PushControlStateAsync(), PushDxfStateAsync());
        }

        private Task PushControlStateAsync()
        {
            bool connected = plcComm != null && plcComm.IsConnected;
            object payload = new
            {
                view = currentView,
                theme = currentTheme,
                connection = new
                {
                    connected,
                    banner = connectionBanner,
                    ip = plcIpAddress,
                    port = plcPort,
                    meta = "MX Component logical station: 0",
                    buttonText = connected ? "DISCONNECT SYSTEM" : "CONNECT SYSTEM"
                },
                coordinates = new[]
                {
                    new
                    {
                        key = "x",
                        label = "COORDINATE X",
                        accent = "blue",
                        display = coordinateX.ToString("0.00", CultureInfo.InvariantCulture),
                        raw = coordinateX,
                        register = CoordinateXRegister
                    },
                    new
                    {
                        key = "y",
                        label = "COORDINATE Y",
                        accent = "green",
                        display = coordinateY.ToString("0.00", CultureInfo.InvariantCulture),
                        raw = coordinateY,
                        register = CoordinateYRegister
                    },
                    new
                    {
                        key = "z",
                        label = "COORDINATE Z",
                        accent = "orange",
                        display = coordinateZ.ToString("0.00", CultureInfo.InvariantCulture),
                        raw = coordinateZ,
                        register = CoordinateZRegister
                    }
                },
                velocity = new
                {
                    value = velocityValue,
                    display = (velocityValue / 10.0).ToString("0.0", CultureInfo.InvariantCulture),
                    register = VelocityRegister,
                    min = 0,
                    max = 50
                },
                integrity = new
                {
                    state = integrityState,
                    detail = integrityDetail,
                    tone = integrityTone
                },
                monitorRows = monitorRows.Select(row => new
                {
                    register = row.Register,
                    value = row.Value,
                    status = row.Status
                }).ToList()
            };

            return PostToUiAsync("controlState", payload);
        }

        private Task PushDxfStateAsync()
        {
            object payload = new
            {
                view = currentView,
                theme = currentTheme,
                filePath = activeCadDocument?.DirectoryPath ?? string.Empty,
                fileName = activeCadDocument?.FileName ?? string.Empty,
                bounds = activeCadDocument == null
                    ? new
                    {
                        left = 0.0,
                        top = 0.0,
                        right = 100.0,
                        bottom = 100.0,
                        width = 100.0,
                        height = 100.0
                    }
                    : new
                    {
                        left = activeCadDocument.Bounds.Left,
                        top = activeCadDocument.Bounds.Top,
                        right = activeCadDocument.Bounds.Right,
                        bottom = activeCadDocument.Bounds.Bottom,
                        width = activeCadDocument.Bounds.Width,
                        height = activeCadDocument.Bounds.Height
                    },
                primitives = activeCadDocument == null
                    ? new List<object>()
                    : activeCadDocument.Primitives.Select(primitive => (object)new
                    {
                        sourceType = primitive.SourceType,
                        points = primitive.Points.Select(point => new
                        {
                            x = point.X,
                            y = point.Y
                        }).ToList()
                    }).ToList(),
                points = activeCadDocument == null
                    ? new List<object>()
                    : activeCadDocument.Points.Select(point => (object)new
                    {
                        index = point.Index,
                        lineType = point.LineType,
                        x = point.X,
                        y = point.Y,
                        xDisplay = point.XDisplay,
                        yDisplay = point.YDisplay,
                        key = point.Key
                    }).ToList(),
                selectedPointKey = selectedCadPointKey ?? string.Empty,
                assignedPointKeys,
                processRows = processRows.Select(row => new
                {
                    key = row.Key,
                    motionType = row.MotionType,
                    mCodeValue = row.MCodeValue ?? string.Empty,
                    dwell = row.Dwell ?? string.Empty,
                    speed = row.Speed ?? string.Empty,
                    endCoordinate = row.EndCoordinate ?? string.Empty,
                    centerCoordinate = row.CenterCoordinate ?? string.Empty
                }).ToList()
            };

            return PostToUiAsync("dxfState", payload);
        }

        private Task NotifyAsync(string kind, string title, string message)
        {
            return PostToUiAsync("notify", new
            {
                kind,
                title,
                message
            });
        }

        private Task PostToUiAsync(string type, object payload)
        {
            if (!webReady || webView.CoreWebView2 == null)
            {
                return Task.CompletedTask;
            }

            string json = serializer.Serialize(new { type, payload });
            return webView.CoreWebView2.ExecuteScriptAsync("window.app && window.app.receive(" + json + ");");
        }

        private static Dictionary<string, object> GetMap(Dictionary<string, object> source, string key)
        {
            object value;
            if (source == null || !source.TryGetValue(key, out value))
            {
                return new Dictionary<string, object>();
            }

            return value as Dictionary<string, object> ?? new Dictionary<string, object>();
        }

        private static string GetString(Dictionary<string, object> source, string key, string fallback = "")
        {
            object value;
            if (source == null || !source.TryGetValue(key, out value) || value == null)
            {
                return fallback;
            }

            return Convert.ToString(value, CultureInfo.InvariantCulture) ?? fallback;
        }

        private static int GetInt(Dictionary<string, object> source, string key, int fallback = 0)
        {
            object value;
            if (source == null || !source.TryGetValue(key, out value) || value == null)
            {
                return fallback;
            }

            if (value is int)
            {
                return (int)value;
            }

            if (value is long)
            {
                return Convert.ToInt32((long)value, CultureInfo.InvariantCulture);
            }

            if (value is double)
            {
                return Convert.ToInt32((double)value, CultureInfo.InvariantCulture);
            }

            int parsed;
            return int.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed)
                ? parsed
                : fallback;
        }

        private static string FormatPoint(CadDocumentService.CadPointData point)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0:0.###}, {1:0.###}", point.X, point.Y);
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

        private sealed class MonitorRow
        {
            public string Register { get; set; }
            public string Value { get; set; }
            public string Status { get; set; }
        }

        private sealed class ProcessRow
        {
            public string Key { get; set; }
            public string MotionType { get; set; }
            public string MCodeValue { get; set; }
            public string Dwell { get; set; }
            public string Speed { get; set; }
            public string EndCoordinate { get; set; }
            public string CenterCoordinate { get; set; }
        }
    }
}
