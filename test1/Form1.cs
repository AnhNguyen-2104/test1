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
using System.Text;

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

        // telemetry configuration: list of D registers and list of device ranges (e.g. "U0\\G2006" length 2)
        private readonly List<string> telemetryRegisters = new List<string> { "D2000", "D2002", "D2004" };
        private readonly List<TelemetryBuffer> telemetryBuffers = new List<TelemetryBuffer> { new TelemetryBuffer { Path = "U0\\G2006", Length = 2 } };

        // simple in-memory log store for PLC I/O operations
        private readonly List<LogEntry> logs = new List<LogEntry>();

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

                    // telemetry control from UI
                    case "addTelemetryRegister":
                        await HandleAddTelemetryRegisterAsync(GetString(payload, "register"));
                        break;

                    case "removeTelemetryRegister":
                        await HandleRemoveTelemetryRegisterAsync(GetString(payload, "register"));
                        break;

                    case "addTelemetryBuffer":
                        await HandleAddTelemetryBufferAsync(GetString(payload, "path"), GetInt(payload, "length", 1));
                        break;

                    case "removeTelemetryBuffer":
                        await HandleRemoveTelemetryBufferAsync(GetString(payload, "path"));
                        break;

                    case "writeBufferRequest":
                        await HandleWriteBufferRequestAsync(GetString(payload, "path"), GetInt(payload, "value", 0));
                        break;

                    case "sendCadX":
                        await HandleSendCadXAsync();
                        break;

                    case "importCadToProcess":
                        await HandleImportCadToProcessAsync();
                        break;

                    case "clearLogs":
                        await HandleClearLogsAsync();
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
                    AddLogEntry(VelocityRegister, velocityValue.ToString(CultureInfo.InvariantCulture), "Write", "OK", "SetVelocity");
                }
                catch (Exception ex)
                {
                    UpdateIntegrityFault(ex.Message);
                    AddLogEntry(VelocityRegister, velocityValue.ToString(CultureInfo.InvariantCulture), "Write", "Error", ex.Message);
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
                int v = active ? 1 : 0;
                plcComm.WriteDeviceValue(register, v);
                UpdateIntegrityState(true);
                AddLogEntry(register, v.ToString(CultureInfo.InvariantCulture), "Write", "OK", "Jog");
            }
            catch (Exception ex)
            {
                if (active)
                {
                    UpdateIntegrityFault(ex.Message);
                    AddLogEntry(JogBaseRegister, (active ? 1 : 0).ToString(CultureInfo.InvariantCulture), "Write", "Error", ex.Message);
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
                AddLogEntry(EmergencyStopRegister, "1", "Write", "OK", "EmergencyStop");
                UpdateIntegrityFault("Emergency stop triggered");
                await PushControlStateAsync();
                await NotifyAsync("error", "PLC", "Đã ghi emergency stop vào " + EmergencyStopRegister + ".");
            }
            catch (Exception ex)
            {
                UpdateIntegrityFault(ex.Message);
                AddLogEntry(EmergencyStopRegister, "1", "Write", "Error", ex.Message);
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
            await PushTelemetryStateAsync();
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
            return Task.WhenAll(PushControlStateAsync(), PushDxfStateAsync(), PushTelemetryStateAsync(), PushLogsStateAsync());
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

        private Task PushTelemetryStateAsync()
        {
            bool connected = plcComm != null && plcComm.IsConnected;
            var dValues = new System.Collections.Generic.List<object>();
            var buffers = new System.Collections.Generic.List<object>();

            if (connected)
            {
                foreach (var reg in telemetryRegisters)
                {
                    try
                    {
                        int v = plcComm.ReadDeviceValue(reg);
                        dValues.Add(new { register = reg, value = v, ok = true });
                    }
                    catch (Exception ex)
                    {
                        dValues.Add(new { register = reg, value = (int?)null, ok = false, error = ex.Message });
                    }
                }

                foreach (var buf in telemetryBuffers)
                {
                    try
                    {
                        int[] arr = plcComm.ReadDeviceRange(buf.Path, buf.Length);
                        buffers.Add(new { path = buf.Path, values = arr, ok = true });
                    }
                    catch (Exception ex)
                    {
                        buffers.Add(new { path = buf.Path, values = new int[0], ok = false, error = ex.Message });
                    }
                }
            }

            var payload = new
            {
                view = currentView,
                theme = currentTheme,
                connected,
                dValues,
                buffers
            };

            return PostToUiAsync("telemetry", payload);
        }

        private Task PushLogsStateAsync()
        {
            var outLogs = logs.Select(l => new
            {
                timestamp = l.Timestamp.ToString("o"),
                direction = l.Direction,
                address = l.Address,
                value = l.Value,
                status = l.Status,
                message = l.Message
            }).ToList();

            var payload = new { view = currentView, theme = currentTheme, logs = outLogs };
            return PostToUiAsync("logsState", payload);
        }

        private void AddLogEntry(string address, string value, string direction = "Write", string status = "OK", string message = null)
        {
            try
            {
                logs.Insert(0, new LogEntry
                {
                    Timestamp = DateTime.UtcNow,
                    Direction = direction,
                    Address = address,
                    Value = value,
                    Status = status,
                    Message = message
                });

                // keep recent 500 entries
                if (logs.Count > 500) logs.RemoveRange(500, logs.Count - 500);

                // fire-and-forget push to UI
                _ = PushLogsStateAsync();
            }
            catch
            {
                // ignore logging errors
            }
        }

        private Task HandleClearLogsAsync()
        {
            logs.Clear();
            return PushLogsStateAsync();
        }

        private async Task HandleAddTelemetryRegisterAsync(string register)
        {
            if (string.IsNullOrWhiteSpace(register)) return;
            register = register.Trim().ToUpperInvariant();
            if (telemetryRegisters.Exists(r => string.Equals(r, register, StringComparison.OrdinalIgnoreCase)))
            {
                await NotifyAsync("info", "Telemetry", "Register đã tồn tại.");
                return;
            }

            telemetryRegisters.Add(register);
            await PushTelemetryStateAsync();
        }

        private async Task HandleRemoveTelemetryRegisterAsync(string register)
        {
            if (string.IsNullOrWhiteSpace(register)) return;
            var item = telemetryRegisters.Find(r => string.Equals(r, register, StringComparison.OrdinalIgnoreCase));
            if (item != null) telemetryRegisters.Remove(item);
            await PushTelemetryStateAsync();
        }

        private async Task HandleAddTelemetryBufferAsync(string path, int length)
        {
            if (string.IsNullOrWhiteSpace(path) || length <= 0) return;
            telemetryBuffers.Add(new TelemetryBuffer { Path = path.Trim(), Length = Math.Max(1, length) });
            await PushTelemetryStateAsync();
        }

        private async Task HandleRemoveTelemetryBufferAsync(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return;
            var buf = telemetryBuffers.FirstOrDefault(b => string.Equals(b.Path, path, StringComparison.OrdinalIgnoreCase));
            if (buf != null) telemetryBuffers.Remove(buf);
            await PushTelemetryStateAsync();
        }

        private async Task HandleWriteBufferRequestAsync(string path, int value)
        {
            if (plcComm == null || !plcComm.IsConnected)
            {
                await NotifyAsync("error", "Telemetry", "PLC is not connected.");
                return;
            }

            try
            {
                // For testing, attempt to write 32-bit as two words if possible
                string used;
                int result = plcComm.WriteInt32ToDevicePath(path, value, out used);
                AddLogEntry(path, value.ToString(CultureInfo.InvariantCulture), "Write", result == 0 ? "OK" : $"Error({result})", used);
                if (result == 0)
                {
                    await NotifyAsync("success", "Telemetry", $"Ghi thành công {path} bằng {used}.");
                }
                else
                {
                    await NotifyAsync("error", "Telemetry", $"Ghi thất bại ({result}) bằng {used}.");
                }
            }
            catch (Exception ex)
            {
                AddLogEntry(path, value.ToString(CultureInfo.InvariantCulture), "Write", "Error", ex.Message);
                await NotifyAsync("error", "Telemetry", ex.Message);
            }
        }

        private int MapMotionTypeToCode(string motionType)
        {
            if (string.IsNullOrWhiteSpace(motionType)) return 0;
            string s = motionType.Trim().ToLowerInvariant();
            
            // Theo bảng lệnh Hex:
            // Line (Đường thẳng): 0x100A (Positioning complete)
            // Arc CW (Cung thuận chiều): 0x100F
            // Arc CCW (Cung ngược chiều): 0x1010
            
            if (s.Contains("line") || s.Contains("đường") || s.Contains("duong")) return 0x100A;
            if (s.Contains("arc") && s.Contains("ccw")) return 0x1010;
            if (s.Contains("arc") && s.Contains("cw")) return 0x100F;
            if (s.Contains("arc") || s.Contains("cung")) return 0x100F; // Mặc định cung thuận chiều
            if (s.Contains("circle") || s.Contains("tâm") || s.Contains("tam") || s.Contains("tròn") || s.Contains("tron")) return 0x100F;
            
            return 0;
        }

        private async Task HandleSendCadXAsync()
        {
            if (plcComm == null || !plcComm.IsConnected)
            {
                await NotifyAsync("error", "Telemetry", "PLC is not connected.");
                return;
            }

            // prefer to send all CAD points if available, otherwise fall back to processRows
            List<ProcessRow> rowsToSend = new List<ProcessRow>();

            if (activeCadDocument != null && activeCadDocument.Points != null && activeCadDocument.Points.Count > 0)
            {
                // build rows from CAD points
                var pts = activeCadDocument.Points;

                // try to locate a center points list (points marked as center/circle)
                List<CadDocumentService.CadPointData> centers = pts.Where(p => !string.IsNullOrWhiteSpace(p.LineType) &&
                    (p.LineType.IndexOf("circle", StringComparison.OrdinalIgnoreCase) >= 0 || p.LineType.IndexOf("tâm", StringComparison.OrdinalIgnoreCase) >= 0 || p.LineType.IndexOf("tam", StringComparison.OrdinalIgnoreCase) >= 0 || p.LineType.IndexOf("center", StringComparison.OrdinalIgnoreCase) >= 0)).ToList();

                for (int i = 0; i < pts.Count; i++)
                {
                    var p = pts[i];
                    string lt = p.LineType ?? string.Empty;
                    var row = new ProcessRow();

                    if (lt.IndexOf("arc", StringComparison.OrdinalIgnoreCase) >= 0 || lt.IndexOf("cung", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        row.MotionType = "Arc";
                        // choose next point as end if available, else use current point
                        CadDocumentService.CadPointData endPt = (i + 1 < pts.Count) ? pts[i + 1] : p;
                        row.EndCoordinate = string.Format(CultureInfo.InvariantCulture, "{0:0.###};{1:0.###}", endPt.X, endPt.Y);

                        // try to find a center (first center found)
                        var c = centers.FirstOrDefault();
                        if (c != null)
                        {
                            row.CenterCoordinate = string.Format(CultureInfo.InvariantCulture, "{0:0.###};{1:0.###}", c.X, c.Y);
                        }
                        else
                        {
                            row.CenterCoordinate = string.Empty;
                        }
                    }
                    else if (lt.IndexOf("circle", StringComparison.OrdinalIgnoreCase) >= 0 || lt.IndexOf("tâm", StringComparison.OrdinalIgnoreCase) >= 0 || lt.IndexOf("tam", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        row.MotionType = "Circle";
                        // center is this point; choose a circumference point if possible
                        row.CenterCoordinate = string.Format(CultureInfo.InvariantCulture, "{0:0.###};{1:0.###}", p.X, p.Y);
                        var circ = FindCircumferencePointFromPrimitives(activeCadDocument, p);
                        if (circ != null)
                        {
                            row.EndCoordinate = string.Format(CultureInfo.InvariantCulture, "{0:0.###};{1:0.###}", circ.X, circ.Y);
                        }
                        else
                        {
                            row.EndCoordinate = string.Empty;
                        }
                    }
                    else
                    {
                        // line / point / intersection
                        row.MotionType = "Line";
                        row.EndCoordinate = string.Format(CultureInfo.InvariantCulture, "{0:0.###};{1:0.###}", p.X, p.Y);
                        row.CenterCoordinate = string.Empty;
                    }

                    // default other fields left empty (MCodeValue, Dwell, Speed) so they will be written as 0
                    row.MCodeValue = row.MCodeValue ?? string.Empty;
                    row.Dwell = row.Dwell ?? string.Empty;
                    row.Speed = row.Speed ?? string.Empty;

                    rowsToSend.Add(row);
                }
            }
            else
            {
                // fallback to existing processRows
                rowsToSend.AddRange(processRows);
            }

            if (rowsToSend.Count == 0)
            {
                await NotifyAsync("info", "Telemetry", "Không có điểm để gửi.");
                return;
            }

            const int baseG = 2000;
            const int stride = 10;
            // follow buffer mapping exactly for X axis
            const int offsetMoveCode = 0; // U0\G(2000 + (n-1)*10 + 0)
            const int offsetMCode = 1;    // U0\G(2000 + (n-1)*10 + 1)
            const int offsetDwell = 2;    // U0\G(2000 + (n-1)*10 + 2)
            const int offsetSpeed = 4;    // U0\G(2000 + (n-1)*10 + 4)
            const int offsetPosX = 6;     // U0\G(2000 + (n-1)*10 + 6)  <-- Position X
            const int offsetCenterX = 8;  // U0\G(2000 + (n-1)*10 + 8)  <-- Center X

            int n = 1;
            foreach (var row in rowsToSend)
            {
                // parse EndCoordinate and CenterCoordinate formatted as "X;Y"
                int endX = 0;
                int centerX = 0;
                bool hasEnd = false;
                bool hasCenter = false;

                if (!string.IsNullOrWhiteSpace(row.EndCoordinate))
                {
                    var parts = row.EndCoordinate.Split(';');
                    if (parts.Length >= 1 && double.TryParse(parts[0], NumberStyles.Any, CultureInfo.InvariantCulture, out double ex)) { endX = Convert.ToInt32(Math.Round(ex)); hasEnd = true; }
                }
                if (!string.IsNullOrWhiteSpace(row.CenterCoordinate))
                {
                    var parts = row.CenterCoordinate.Split(';');
                    if (parts.Length >= 1 && double.TryParse(parts[0], NumberStyles.Any, CultureInfo.InvariantCulture, out double cx)) { centerX = Convert.ToInt32(Math.Round(cx)); hasCenter = true; }
                }

                // parse M code, dwell, speed
                int mcodeVal = 0;
                if (!string.IsNullOrWhiteSpace(row.MCodeValue))
                {
                    int parsed;
                    if (int.TryParse(row.MCodeValue, NumberStyles.Any, CultureInfo.InvariantCulture, out parsed)) mcodeVal = parsed;
                }

                int dwellVal = 0;
                if (!string.IsNullOrWhiteSpace(row.Dwell))
                {
                    int parsed;
                    if (int.TryParse(row.Dwell, NumberStyles.Any, CultureInfo.InvariantCulture, out parsed)) dwellVal = parsed;
                }

                int speedVal = 0;
                if (!string.IsNullOrWhiteSpace(row.Speed))
                {
                    int parsed;
                    if (int.TryParse(row.Speed, NumberStyles.Any, CultureInfo.InvariantCulture, out parsed)) speedVal = parsed;
                }

                int moveCode = MapMotionTypeToCode(row.MotionType);

                string deviceBase = $"U0\\G{baseG + (n - 1) * stride}";

                try
                {
                    // write move code
                    string deviceMove = $"U0\\G{baseG + (n - 1) * stride + offsetMoveCode}";
                    string usedMove;
                    int rMove = plcComm.WriteInt32ToDevicePath(deviceMove, moveCode, out usedMove);
                    AddLogEntry(deviceMove, moveCode.ToString(CultureInfo.InvariantCulture), "Write", rMove == 0 ? "OK" : $"Error({rMove})", "MoveCode:" + usedMove);

                    // write M code
                    string deviceM = $"U0\\G{baseG + (n - 1) * stride + offsetMCode}";
                    string usedM;
                    int rM = plcComm.WriteInt32ToDevicePath(deviceM, mcodeVal, out usedM);
                    AddLogEntry(deviceM, mcodeVal.ToString(CultureInfo.InvariantCulture), "Write", rM == 0 ? "OK" : $"Error({rM})", "MCode:" + usedM);

                    // write dwell
                    string deviceDwell = $"U0\\G{baseG + (n - 1) * stride + offsetDwell}";
                    string usedD;
                    int rD = plcComm.WriteInt32ToDevicePath(deviceDwell, dwellVal, out usedD);
                    AddLogEntry(deviceDwell, dwellVal.ToString(CultureInfo.InvariantCulture), "Write", rD == 0 ? "OK" : $"Error({rD})", "Dwell:" + usedD);

                    // write speed
                    string deviceSpeed = $"U0\\G{baseG + (n - 1) * stride + offsetSpeed}";
                    string usedS;
                    int rS = plcComm.WriteInt32ToDevicePath(deviceSpeed, speedVal, out usedS);
                    AddLogEntry(deviceSpeed, speedVal.ToString(CultureInfo.InvariantCulture), "Write", rS == 0 ? "OK" : $"Error({rS})", "Speed:" + usedS);

                    // write position X
                    if (hasEnd)
                    {
                        string devicePosX = $"U0\\G{baseG + (n - 1) * stride + offsetPosX}";
                        string usedX;
                        int rX = plcComm.WriteInt32ToDevicePath(devicePosX, endX, out usedX);
                        AddLogEntry(devicePosX, endX.ToString(CultureInfo.InvariantCulture), "Write", rX == 0 ? "OK" : $"Error({rX})", usedX);
                        if (rX != 0) await NotifyAsync("error", "Telemetry", $"Ghi End X thất bại {devicePosX}: {rX}");
                    }

                    // write center X
                    if (hasCenter)
                    {
                        string deviceCenterX = $"U0\\G{baseG + (n - 1) * stride + offsetCenterX}";
                        string usedCx;
                        int rCx = plcComm.WriteInt32ToDevicePath(deviceCenterX, centerX, out usedCx);
                        AddLogEntry(deviceCenterX, centerX.ToString(CultureInfo.InvariantCulture), "Write", rCx == 0 ? "OK" : $"Error({rCx})", usedCx);
                        if (rCx != 0) await NotifyAsync("error", "Telemetry", $"Ghi Center X thất bại {deviceCenterX}: {rCx}");
                    }
                }
                catch (Exception ex)
                {
                    AddLogEntry(deviceBase, string.Empty, "Write", "Error", ex.Message);
                    await NotifyAsync("error", "Telemetry", ex.Message);
                }

                n++;
            }

            await NotifyAsync("success", "Telemetry", "Đã gửi tọa độ trục X các điểm CAD xuống PLC.");
        }

        private async Task HandleImportCadToProcessAsync()
        {
            if (activeCadDocument == null || activeCadDocument.Points == null || activeCadDocument.Points.Count == 0)
            {
                await NotifyAsync("info", "DXF", "Chưa có dữ liệu CAD.");
                return;
            }

            // We'll scan CAD points and try to map logical items to processRows.
            var pts = activeCadDocument.Points;
            var used = new bool[pts.Count];

            int assignIndex = 0;

            for (int i = 0; i < pts.Count && assignIndex < processRows.Count; i++)
            {
                if (used[i]) continue;
                var p = pts[i];
                var lt = (p.LineType ?? string.Empty).ToLowerInvariant();

                // Intersection / polyline vertex mapped as Line
                if (lt.Contains("polyline") || lt.Contains("line") || lt.Contains("giao"))
                {
                    var row = processRows[assignIndex++];
                    row.MotionType = "Line";
                    row.EndCoordinate = string.Format(CultureInfo.InvariantCulture, "{0:0.###};{1:0.###}", p.X, p.Y);
                    row.CenterCoordinate = string.Empty;
                    used[i] = true;
                    continue;
                }

                // Arc or circle handling
                if (lt.Contains("arc") || lt.Contains("cung") || lt.Contains("circle") || lt.Contains("tâm") || lt.Contains("tam"))
                {
                    // find center point (if any)
                    int centerIdx = -1;
                    for (int j = 0; j < pts.Count; j++)
                    {
                        var lj = (pts[j].LineType ?? string.Empty).ToLowerInvariant();
                        if (lj.Contains("circle") || lj.Contains("tâm") || lj.Contains("tam") || lj.Contains("center"))
                        {
                            centerIdx = j; break;
                        }
                    }

                    // find endpoint pair: current and next arc point
                    int startIdx = i;
                    int endIdx = -1;
                    for (int j = i + 1; j < pts.Count; j++)
                    {
                        var lj = (pts[j].LineType ?? string.Empty).ToLowerInvariant();
                        if (lj.Contains("arc") || lj.Contains("cung")) { endIdx = j; break; }
                    }

                    // If no end found, maybe this is a circle center or a single point representing arc.
                    var row = processRows[assignIndex++];
                    row.MotionType = lt.Contains("circle") || lt.Contains("tâm") || lt.Contains("tam") ? "Circle" : "Arc";

                    // Determine representative end point
                    CadDocumentService.CadPointData startPt = pts[startIdx];
                    CadDocumentService.CadPointData endPt = endIdx >= 0 ? pts[endIdx] : startPt;

                    // If it's a full circle (start equals end) or only center exists, try to pick a circumference point
                    if (endIdx < 0 || (Math.Abs(startPt.X - endPt.X) < 1e-3 && Math.Abs(startPt.Y - endPt.Y) < 1e-3))
                    {
                        // try to choose a point on circumference from primitives if available
                        var circ = FindCircumferencePointFromPrimitives(activeCadDocument, centerIdx >= 0 ? pts[centerIdx] : startPt);
                        if (circ != null)
                        {
                            startPt = circ;
                            endPt = circ;
                        }
                    }

                    row.EndCoordinate = string.Format(CultureInfo.InvariantCulture, "{0:0.###};{1:0.###}", endPt.X, endPt.Y);
                    if (centerIdx >= 0)
                    {
                        var c = pts[centerIdx];
                        row.CenterCoordinate = string.Format(CultureInfo.InvariantCulture, "{0:0.###};{1:0.###}", c.X, c.Y);
                        used[centerIdx] = true;
                    }
                    else
                    {
                        row.CenterCoordinate = string.Empty;
                    }

                    used[startIdx] = true;
                    if (endIdx >= 0) used[endIdx] = true;

                    continue;
                }

                // fallback: treat as point
                if (!used[i])
                {
                    var row = processRows[assignIndex++];
                    row.MotionType = p.LineType ?? "Point";
                    row.EndCoordinate = string.Format(CultureInfo.InvariantCulture, "{0:0.###};{1:0.###}", p.X, p.Y);
                    row.CenterCoordinate = string.Empty;
                    used[i] = true;
                }
            }

            await PushDxfStateAsync();
            await NotifyAsync("success", "DXF", "Đã import tọa độ CAD vào bảng process.");
        }

        private CadDocumentService.CadPointData FindCircumferencePointFromPrimitives(CadDocumentService.CadLoadResult doc, CadDocumentService.CadPointData center)
        {
            if (doc == null || doc.Primitives == null) return null;
            // choose a point from primitives that is not equal to center and at reasonable distance
            foreach (var prim in doc.Primitives)
            {
                if (prim?.Points == null) continue;
                foreach (var pt in prim.Points)
                {
                    if (pt == null) continue;
                    double dx = pt.X - (center?.X ?? 0);
                    double dy = pt.Y - (center?.Y ?? 0);
                    double d = Math.Sqrt(dx * dx + dy * dy);
                    if (d > 1e-3) // found a circumference-like point
                    {
                        return new CadDocumentService.CadPointData
                        {
                            X = pt.X,
                            Y = pt.Y,
                            XDisplay = pt.X.ToString(CultureInfo.InvariantCulture),
                            YDisplay = pt.Y.ToString(CultureInfo.InvariantCulture),
                            Key = Guid.NewGuid().ToString(),
                            Index = -1,
                            LineType = "circum"
                        };
                    }
                }
            }
            return null;
        }

        private async Task PostToUiAsync(string type, object payload)
        {
            if (!webReady || webView.CoreWebView2 == null)
            {
                return;
            }

            string json = serializer.Serialize(new { type, payload });
            await webView.CoreWebView2.ExecuteScriptAsync("window.app && window.app.receive(" + json + ");");
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

        private sealed class TelemetryBuffer
        {
            public string Path { get; set; }
            public int Length { get; set; }
        }

        private sealed class LogEntry
        {
            public DateTime Timestamp { get; set; }
            public string Direction { get; set; }
            public string Address { get; set; }
            public string Value { get; set; }
            public string Status { get; set; }
            public string Message { get; set; }
        }
    }
}
