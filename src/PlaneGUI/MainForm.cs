using System.Diagnostics;
using System.IO.Ports;
using PlaneGUI.Instruments;

namespace PlaneGUI;

enum SourceMode { Simulator, Serial, Manual }

public sealed class MainForm : Form
{
    // The layout is designed at this logical size. Every font and gap is
    // multiplied by uiScale = min(width / DesignWidth, height / DesignHeight),
    // so the whole cockpit grows and shrinks together and text never overflows.
    const float DesignWidth = 1440f, DesignHeight = 900f;

    // ---- State ----
    readonly FlightModel model = new();
    readonly Simulator simulator = new();
    readonly Stopwatch clock = Stopwatch.StartNew();
    readonly HashSet<Keys> heldKeys = new();
    readonly System.Windows.Forms.Timer frameTimer = new() { Interval = 33 };
    SerialLink? link;
    SourceMode mode = SourceMode.Simulator;
    double lastFrame, lastChartSample, gearTransitUntil;
    float prevAltitude = float.NaN, prevHeading = float.NaN, prevRoll;
    float verticalSpeed, turnRate, slip;

    // ---- Scaling ----
    readonly List<(Control Control, float Points, FontStyle Style, string Family)> scaledFonts = new();
    readonly List<Action> scaleActions = new();
    float uiScale = 1f;
    bool scaleApplied;

    // ---- Controls ----
    readonly RadioButton simRadio = new(), serialRadio = new(), manualRadio = new();
    readonly ComboBox portBox = new(), baudBox = new();
    readonly CheckBox invertRoll = new(), invertPitch = new();
    readonly Label statusLabel = new();
    readonly TextBox logBox = new();
    Button connectButton = null!;

    readonly AirspeedIndicator airspeedGauge = new();
    readonly AttitudeIndicator attitude = new();
    readonly Altimeter altimeter = new();
    readonly TurnCoordinator turnCoordinator = new();
    readonly HeadingIndicator heading = new();
    readonly VerticalSpeedIndicator vsi = new();

    readonly RadioButton gearUpRadio = new(), gearDownRadio = new();
    readonly FitLabel gearStatus = new();

    readonly BarGauge throttleLever = new() { Title = "Throttle", Unit = " %", Interactive = true, ScaleLabels = new[] { "Idle", "", "Max" } };
    readonly BarGauge fuelGauge = new() { Title = "Fuel level", Unit = " %", WarnBelow = 20, ScaleLabels = new[] { "Empty", "½", "Full" } };
    readonly BarGauge tempGauge = new() { Title = "Engine temp (EGT)", Unit = " °C", Maximum = 1000, WarnAbove = 950, ScaleLabels = new[] { "0", "500", "1000" } };
    readonly AltitudeChart altitudeChart = new();
    readonly Annunciator annunciator = new();
    readonly Dictionary<string, FitLabel> readouts = new();

    public MainForm()
    {
        Text = "Plane GUI – Artificial Horizon   (F11: full screen)";
        BackColor = Theme.Background;
        ForeColor = Theme.Text;
        Font = new Font(Theme.FontFamily, 10f);
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = true;
        StartPosition = FormStartPosition.CenterScreen;
        KeyPreview = true;
        DoubleBuffered = true;
        float dpi = DeviceDpi / 96f;
        MinimumSize = new Size((int)(720 * dpi), (int)(460 * dpi));
        Size = new Size((int)(DesignWidth * dpi), (int)(DesignHeight * dpi));

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 2, BackColor = Theme.Background };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 19));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 59));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 22));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 66));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 34));
        scaleActions.Add(() => root.Padding = new Padding(Px(7)));
        Controls.Add(root);

        var connection = Card("Data source");
        root.Controls.Add(connection, 0, 0);
        root.SetRowSpan(connection, 2);
        BuildConnectionPanel(connection);

        var instruments = Card("Flight instruments");
        root.Controls.Add(instruments, 1, 0);
        BuildInstrumentPanel(instruments);

        var data = Card("Flight data");
        root.Controls.Add(data, 2, 0);
        BuildReadoutPanel(data);

        var chart = Card("Altitude – last 60 s");
        root.Controls.Add(chart, 1, 1);
        altitudeChart.Dock = DockStyle.Fill;
        altitudeChart.Capacity = 240; // 4 samples/s * 60 s
        chart.Controls.Add(altitudeChart);

        var engine = Card("Engine & fuel");
        root.Controls.Add(engine, 2, 1);
        BuildEnginePanel(engine);

        frameTimer.Tick += (_, _) => OnFrame();
        Load += (_, _) =>
        {
            // Don't open larger than the screen we're on.
            var area = Screen.FromControl(this).WorkingArea;
            if (Width > area.Width || Height > area.Height)
            {
                Size = new Size(Math.Max(MinimumSize.Width, area.Width * 9 / 10),
                                Math.Max(MinimumSize.Height, area.Height * 9 / 10));
                CenterToScreen();
            }
            ApplyScale(force: true);
            RefreshPorts();
            SetMode(SourceMode.Simulator);
            frameTimer.Start();
        };
        Resize += (_, _) => ApplyScale();
        Deactivate += (_, _) => heldKeys.Clear();
        FormClosing += (_, _) => { frameTimer.Stop(); Disconnect(); };
    }

    // ===================================================================
    // Scaling helpers
    // ===================================================================

    /// <summary>Converts a size in design pixels to real pixels at the current window scale and DPI.</summary>
    int Px(float designPixels) => Math.Max(0, (int)Math.Round(designPixels * uiScale * DeviceDpi / 96f));

    /// <summary>Gives a control a font that is resized with the window.</summary>
    T Scaled<T>(T control, float points, FontStyle style = FontStyle.Regular, string family = Theme.FontFamily) where T : Control
    {
        scaledFonts.Add((control, points, style, family));
        control.Font = new Font(family, points, style);
        return control;
    }

    void ApplyScale(bool force = false)
    {
        if (ClientSize.Width <= 0 || ClientSize.Height <= 0) return; // minimised
        float dpi = DeviceDpi / 96f;
        float k = Math.Clamp(Math.Min(ClientSize.Width / dpi / DesignWidth, ClientSize.Height / dpi / DesignHeight), 0.45f, 2.5f);
        if (!force && scaleApplied && Math.Abs(k - uiScale) < 0.015f) return;
        uiScale = k;
        scaleApplied = true;

        SuspendLayout();
        foreach (var (control, points, style, family) in scaledFonts)
        {
            var old = control.Font;
            control.Font = new Font(family, points * k, style);
            old.Dispose();
        }
        foreach (var action in scaleActions) action();
        ResumeLayout(true);
        Invalidate(true);
    }

    // ===================================================================
    // Layout
    // ===================================================================

    Panel Card(string title)
    {
        var card = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Panel };
        var titleFont = new Font(Theme.FontFamily, 9f, FontStyle.Bold);
        scaleActions.Add(() =>
        {
            titleFont.Dispose();
            titleFont = new Font(Theme.FontFamily, 9f * uiScale, FontStyle.Bold);
            card.Margin = new Padding(Px(5));
            card.Padding = new Padding(Px(12), Px(10) + titleFont.Height + Px(8), Px(12), Px(12));
        });
        card.Paint += (_, e) =>
        {
            using var pen = new Pen(Theme.PanelBorder);
            e.Graphics.DrawRectangle(pen, 0, 0, card.Width - 1, card.Height - 1);
            TextRenderer.DrawText(e.Graphics, title.ToUpperInvariant(), titleFont, new Point(Px(12), Px(10)), Theme.Muted);
        };
        card.Resize += (_, _) => card.Invalidate();
        return card;
    }

    Button StyledButton(string text, EventHandler onClick, float points = 9.5f)
    {
        var b = Scaled(new Button
        {
            Text = text,
            FlatStyle = FlatStyle.Flat,
            BackColor = Theme.Bezel,
            ForeColor = Theme.Text,
            UseVisualStyleBackColor = false,
            TabStop = false,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
        }, points, FontStyle.Bold);
        b.FlatAppearance.BorderColor = Theme.PanelBorder;
        scaleActions.Add(() =>
        {
            b.Padding = new Padding(Px(6), Px(2), Px(6), Px(2));
            b.Margin = new Padding(Px(3));
        });
        b.Click += onClick;
        return b;
    }

    Label SectionLabel(string text)
    {
        var label = Scaled(new Label { Text = text, AutoSize = true, ForeColor = Theme.Muted }, 9f, FontStyle.Bold);
        scaleActions.Add(() => label.Margin = new Padding(0, Px(10), 0, Px(3)));
        return label;
    }

    static void StyleInput(Control c)
    {
        c.BackColor = Theme.BezelDark;
        c.ForeColor = Theme.Text;
    }

    static TableLayoutPanel Columns(params float[] percents)
    {
        var t = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, Margin = Padding.Empty, ColumnCount = percents.Length };
        foreach (var p in percents) t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, p));
        return t;
    }

    void BuildConnectionPanel(Panel card)
    {
        // One column: settings rows size to their (scaled) content, the serial monitor takes the rest.
        var t = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1 };
        t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        void Row(Control c, bool fill = false)
        {
            t.RowStyles.Add(fill ? new RowStyle(SizeType.Percent, 100) : new RowStyle(SizeType.AutoSize));
            t.Controls.Add(c);
        }

        foreach (var (rb, text, m) in new[]
        {
            (simRadio, "Simulator (no hardware)", SourceMode.Simulator),
            (serialRadio, "Serial port (IMU / Arduino)", SourceMode.Serial),
            (manualRadio, "Manual (buttons / keys)", SourceMode.Manual),
        })
        {
            rb.Text = text;
            rb.AutoSize = true;
            rb.ForeColor = Theme.Text;
            rb.TabStop = false;
            rb.CheckedChanged += (_, _) => { if (rb.Checked) SetMode(m); };
            Row(Scaled(rb, 10f));
        }

        Row(SectionLabel("Port"));
        var portRow = Columns(62, 38);
        portBox.DropDownStyle = ComboBoxStyle.DropDownList;
        portBox.Dock = DockStyle.Fill;
        portBox.DropDown += (_, _) => RefreshPorts();
        StyleInput(portBox);
        var refresh = StyledButton("Refresh", (_, _) => RefreshPorts(), 9f);
        refresh.Dock = DockStyle.Fill;
        portRow.Controls.Add(Scaled(portBox, 10f), 0, 0);
        portRow.Controls.Add(refresh, 1, 0);
        Row(portRow);

        Row(SectionLabel("Baud rate"));
        baudBox.DropDownStyle = ComboBoxStyle.DropDownList;
        baudBox.Dock = DockStyle.Fill;
        baudBox.Items.AddRange(new object[] { 9600, 19200, 38400, 57600, 115200 });
        baudBox.SelectedItem = 9600;
        StyleInput(baudBox);
        Row(Scaled(baudBox, 10f));

        var invertRow = Columns(50, 50);
        invertRoll.Text = "Invert roll";
        invertPitch.Text = "Invert pitch";
        foreach (var cb in new[] { invertRoll, invertPitch })
        {
            cb.AutoSize = true;
            cb.ForeColor = Theme.Text;
            cb.TabStop = false;
            invertRow.Controls.Add(Scaled(cb, 9.5f));
        }
        scaleActions.Add(() => invertRow.Margin = new Padding(0, Px(8), 0, 0));
        Row(invertRow);

        connectButton = StyledButton("Connect", (_, _) => ToggleConnection(), 10f);
        connectButton.Dock = DockStyle.Fill;
        scaleActions.Add(() => connectButton.Margin = new Padding(0, Px(10), 0, Px(6)));
        Row(connectButton);

        // AutoSize + Left|Right anchor makes the label wrap to the column width.
        statusLabel.AutoSize = true;
        statusLabel.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        statusLabel.ForeColor = Theme.Muted;
        Row(Scaled(statusLabel, 9.5f));

        Row(SectionLabel("SERIAL MONITOR"));
        logBox.Dock = DockStyle.Fill;
        logBox.Multiline = true;
        logBox.ReadOnly = true;
        logBox.ScrollBars = ScrollBars.Both;
        logBox.WordWrap = false;
        logBox.BorderStyle = BorderStyle.None;
        logBox.TabStop = false;
        StyleInput(logBox);
        Row(Scaled(logBox, 9f, FontStyle.Regular, Theme.MonoFontFamily), fill: true);

        card.Controls.Add(t);
    }

    void BuildInstrumentPanel(Panel card)
    {
        // Classic "six-pack": airspeed, attitude, altimeter / turn coordinator, heading, vertical speed.
        var grid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 3 };
        for (int i = 0; i < 3; i++) grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
        grid.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        grid.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        card.Controls.Add(grid);

        Control[] sixPack = { airspeedGauge, attitude, altimeter, turnCoordinator, heading, vsi };
        for (int i = 0; i < sixPack.Length; i++)
        {
            sixPack[i].Dock = DockStyle.Fill;
            sixPack[i].Margin = Padding.Empty;
            grid.Controls.Add(sixPack[i], i % 3, i / 3);
        }

        // Landing gear (bottom left)
        var gear = new TableLayoutPanel { AutoSize = true, ColumnCount = 2, Anchor = AnchorStyles.None };
        var gearTitle = SectionLabel("LANDING GEAR");
        gear.Controls.Add(gearTitle, 0, 0);
        gear.SetColumnSpan(gearTitle, 2);
        gearUpRadio.Text = "Up";
        gearDownRadio.Text = "Down";
        int col = 0;
        foreach (var rb in new[] { gearUpRadio, gearDownRadio })
        {
            rb.AutoSize = true;
            rb.ForeColor = Theme.Text;
            rb.TabStop = false;
            rb.CheckedChanged += (_, _) => { if (rb.Checked) SetGear(rb == gearDownRadio); };
            gear.Controls.Add(Scaled(rb, 10f), col++, 1);
        }
        gearUpRadio.Checked = true;
        gearStatus.Dock = DockStyle.Fill;
        gear.Controls.Add(Scaled(gearStatus, 10f, FontStyle.Bold, Theme.MonoFontFamily), 0, 2);
        gear.SetColumnSpan(gearStatus, 2);
        scaleActions.Add(() => gearStatus.Size = new Size(Px(170), Px(22)));
        grid.Controls.Add(gear, 0, 2);

        // Manual control pad (bottom centre)
        var pad = new TableLayoutPanel { AutoSize = true, ColumnCount = 3, RowCount = 3, Anchor = AnchorStyles.None };
        void PadButton(string text, int c, int r, EventHandler onClick)
        {
            var b = StyledButton(text, onClick);
            b.Dock = DockStyle.Fill;
            pad.Controls.Add(b, c, r);
        }
        PadButton("Nose up", 1, 0, (_, _) => Nudge(0, 2));
        PadButton("Bank L", 0, 1, (_, _) => Nudge(-5, 0));
        PadButton("Level", 1, 1, (_, _) => Level());
        PadButton("Bank R", 2, 1, (_, _) => Nudge(5, 0));
        PadButton("Nose dn", 1, 2, (_, _) => Nudge(0, -2));
        grid.Controls.Add(pad, 1, 2);

        // Help / reset (bottom right)
        var help = new TableLayoutPanel { AutoSize = true, ColumnCount = 1, Anchor = AnchorStyles.None };
        foreach (var b in new[] { StyledButton("Help", (_, _) => ShowHelp()), StyledButton("Reset flight", (_, _) => ResetFlight()) })
        {
            b.Dock = DockStyle.Fill;
            help.Controls.Add(b);
        }
        grid.Controls.Add(help, 2, 2);
    }

    static readonly string[] ReadoutNames =
    {
        "Latitude", "Longitude", "Altitude", "Vertical speed", "Airspeed", "Heading",
        "Turn rate", "Roll", "Pitch", "Angle of attack", "G-load", "Engine N1",
    };

    void BuildReadoutPanel(Panel card)
    {
        var table = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

        foreach (var name in ReadoutNames)
        {
            table.RowStyles.Add(new RowStyle(SizeType.Percent, 66f / ReadoutNames.Length));
            var label = Scaled(new FitLabel { Text = name, Dock = DockStyle.Fill, ForeColor = Theme.Muted, Margin = Padding.Empty }, 9.5f);
            var value = Scaled(new FitLabel { Dock = DockStyle.Fill, AlignRight = true, Margin = Padding.Empty }, 11f, FontStyle.Bold, Theme.MonoFontFamily);
            readouts[name] = value;
            table.Controls.Add(label);
            table.Controls.Add(value);
        }

        annunciator.Define("STALL", true);
        annunciator.Define("OVERSPEED", true);
        annunciator.Define("PULL UP", true);
        annunciator.Define("BANK ANGLE", false);
        annunciator.Define("GEAR", false);
        annunciator.Define("LOW FUEL", false);
        table.RowStyles.Add(new RowStyle(SizeType.Percent, 34));
        annunciator.Dock = DockStyle.Fill;
        scaleActions.Add(() => annunciator.Margin = new Padding(0, Px(10), 0, 0));
        table.Controls.Add(annunciator);
        table.SetColumnSpan(annunciator, 2);
        card.Controls.Add(table);
    }

    void BuildEnginePanel(Panel card)
    {
        var table = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1 };
        throttleLever.ValueChanged += (_, _) => model.State.Throttle = throttleLever.Value / 100f;
        foreach (var gauge in new[] { throttleLever, fuelGauge, tempGauge })
        {
            table.RowStyles.Add(new RowStyle(SizeType.Percent, 33.33f));
            gauge.Dock = DockStyle.Fill;
            scaleActions.Add(() => gauge.Margin = new Padding(0, 0, 0, Px(8)));
            table.Controls.Add(gauge);
        }
        card.Controls.Add(table);
    }

    // ===================================================================
    // Modes, serial connection
    // ===================================================================

    void SetMode(SourceMode m)
    {
        mode = m;
        heldKeys.Clear();
        simRadio.Checked = m == SourceMode.Simulator;
        serialRadio.Checked = m == SourceMode.Serial;
        manualRadio.Checked = m == SourceMode.Manual;
        UpdateStatus();
    }

    void RefreshPorts()
    {
        var selected = portBox.SelectedItem as string;
        var ports = SerialPort.GetPortNames().Distinct().OrderBy(p => p.Length).ThenBy(p => p).ToArray();
        portBox.Items.Clear();
        portBox.Items.AddRange(ports);
        if (selected != null && ports.Contains(selected)) portBox.SelectedItem = selected;
        else if (ports.Length > 0) portBox.SelectedIndex = 0;
        UpdateStatus();
    }

    void ToggleConnection()
    {
        if (link != null) { Disconnect(); return; }

        if (portBox.SelectedItem is not string portName)
        {
            MessageBox.Show(this, "No serial port selected. Plug in the device and press Refresh.",
                            "Plane GUI", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var newLink = new SerialLink(portName, (int)(baudBox.SelectedItem ?? 9600));
        try
        {
            newLink.Open();
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or ArgumentException or InvalidOperationException)
        {
            newLink.Dispose();
            MessageBox.Show(this, $"Could not open {portName}:\n{ex.Message}\n\nIs another program (e.g. the Arduino serial monitor) using it?",
                            "Plane GUI", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        link = newLink;
        logBox.Clear();
        connectButton.Text = "Disconnect";
        portBox.Enabled = baudBox.Enabled = false;
        SetMode(SourceMode.Serial);
    }

    void Disconnect()
    {
        link?.Dispose();
        link = null;
        connectButton.Text = "Connect";
        portBox.Enabled = baudBox.Enabled = true;
        UpdateStatus();
    }

    void UpdateStatus()
    {
        string text;
        Color color = Theme.Muted;
        if (link != null)
        {
            var since = link.SinceLastData;
            if (since is null) text = $"Connected to {link.PortName} @ {link.BaudRate}. Waiting for data…";
            else if (since.Value.TotalSeconds > 2) { text = $"{link.PortName}: no data for {since.Value.TotalSeconds:0} s"; color = Theme.Bad; }
            else { text = $"{link.PortName} @ {link.BaudRate}: receiving data"; color = Theme.Good; }
            if (mode != SourceMode.Serial) text += " (select Serial port to fly from it)";
        }
        else if (mode == SourceMode.Serial)
        {
            text = portBox.Items.Count == 0 ? "No serial ports found." : "Not connected. Press Connect.";
        }
        else
        {
            text = portBox.Items.Count == 0 ? "No serial ports found." : $"{portBox.Items.Count} port(s) available.";
        }
        if (statusLabel.Text != text) statusLabel.Text = text;
        statusLabel.ForeColor = color;
    }

    // ===================================================================
    // Manual controls, gear, help
    // ===================================================================

    void Nudge(float roll, float pitch)
    {
        if (mode != SourceMode.Manual) SetMode(SourceMode.Manual);
        var s = model.State;
        s.Roll = Math.Clamp(s.Roll + roll, -80f, 80f);
        s.Pitch = Math.Clamp(s.Pitch + pitch, -40f, 40f);
    }

    void Level()
    {
        model.State.Roll = 0;
        model.State.Pitch = 0;
    }

    void ResetFlight()
    {
        model.Reset();
        altitudeChart.Clear();
        gearTransitUntil = 0;
        prevAltitude = prevHeading = float.NaN;
        verticalSpeed = turnRate = slip = 0;
    }

    void SetGear(bool down)
    {
        if (model.State.GearDown == down) return;
        model.State.GearDown = down;
        gearTransitUntil = clock.Elapsed.TotalSeconds + 4; // gear takes a few seconds to travel
    }

    void UpdateGearStatus(FlightState s, double now)
    {
        if (gearDownRadio.Checked != s.GearDown) // keep radios in sync with serial / reset
        {
            gearDownRadio.Checked = s.GearDown;
            gearUpRadio.Checked = !s.GearDown;
        }
        string text;
        Color color;
        if (now < gearTransitUntil) { text = "IN TRANSIT"; color = Theme.Accent; }
        else if (s.GearDown && s.Airspeed > FlightModel.GearMaxSpeed) { text = "DOWN – OVERSPEED"; color = Theme.Bad; }
        else if (s.GearDown) { text = "DOWN ●●●"; color = Theme.Good; }
        else { text = "UP"; color = Theme.Muted; }
        if (gearStatus.Text != text) gearStatus.Text = text;
        gearStatus.ForeColor = color;
    }

    void ShowHelp() => MessageBox.Show(this,
        "DATA SOURCE\n" +
        "  Simulator – flies gentle manoeuvres, no hardware needed.\n" +
        "  Serial port – pick the port and baud rate, then Connect.\n" +
        "  Manual – use the buttons or the arrow keys.\n\n" +
        "THROTTLE (any mode)\n" +
        "  Drag the throttle bar, or hold Page Up / Page Down (or + / −).\n" +
        "  Climbing bleeds airspeed, diving adds it, gear down adds drag.\n\n" +
        "KEYS (Manual mode)\n" +
        "  ← / →  bank left / right\n" +
        "  ↑ / ↓  nose down / nose up (like a joystick)\n" +
        "  G  landing gear up / down\n" +
        "  F11  full screen,  Esc  leave full screen\n\n" +
        "SERIAL FORMAT\n" +
        "  One or more Key=Value pairs per line, e.g.\n" +
        "  Roll=12.5,Pitch=-3,Heading=270,Alt=1500,Speed=250\n" +
        "  Old GyroX= / GyroY= lines also work.",
        "Plane GUI – Help", MessageBoxButtons.OK, MessageBoxIcon.Information);

    static readonly Keys[] FlightKeys = { Keys.Left, Keys.Right, Keys.Up, Keys.Down };
    static readonly Keys[] ThrottleKeys = { Keys.PageUp, Keys.PageDown, Keys.Add, Keys.Subtract, Keys.Oemplus, Keys.OemMinus };

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.F11 || (keyData == Keys.Escape && FormBorderStyle == FormBorderStyle.None))
        {
            ToggleFullScreen();
            return true;
        }
        if (keyData == Keys.G && ActiveControl is not TextBoxBase)
        {
            SetGear(!model.State.GearDown);
            return true;
        }
        if (ThrottleKeys.Contains(keyData) || (mode == SourceMode.Manual && FlightKeys.Contains(keyData)))
        {
            heldKeys.Add(keyData);
            return true;
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        heldKeys.Remove(e.KeyCode);
        base.OnKeyUp(e);
    }

    // F11: borderless full screen
    FormWindowState windowedState;
    void ToggleFullScreen()
    {
        if (FormBorderStyle == FormBorderStyle.None)
        {
            FormBorderStyle = FormBorderStyle.Sizable;
            WindowState = windowedState;
        }
        else
        {
            windowedState = WindowState;
            WindowState = FormWindowState.Normal; // required for the border change to cover the taskbar
            FormBorderStyle = FormBorderStyle.None;
            WindowState = FormWindowState.Maximized;
        }
    }

    // ===================================================================
    // Frame loop
    // ===================================================================

    void OnFrame()
    {
        double now = clock.Elapsed.TotalSeconds;
        float dt = (float)Math.Min(0.1, now - lastFrame);
        lastFrame = now;
        if (dt <= 0) return;
        var s = model.State;
        bool haveHeading = false, haveAltitude = false, haveAirspeed = false;

        // Throttle works in every mode.
        const float throttleRate = 0.4f; // full travel in 2.5 s
        if (heldKeys.Contains(Keys.PageUp) || heldKeys.Contains(Keys.Add) || heldKeys.Contains(Keys.Oemplus)) s.Throttle += throttleRate * dt;
        if (heldKeys.Contains(Keys.PageDown) || heldKeys.Contains(Keys.Subtract) || heldKeys.Contains(Keys.OemMinus)) s.Throttle -= throttleRate * dt;
        s.Throttle = Math.Clamp(s.Throttle, 0f, 1f);

        switch (mode)
        {
            case SourceMode.Simulator:
                simulator.Step(s, dt);
                break;

            case SourceMode.Manual:
                const float rollRate = 30f, pitchRate = 10f; // deg/s while a key is held
                if (heldKeys.Contains(Keys.Left)) s.Roll -= rollRate * dt;
                if (heldKeys.Contains(Keys.Right)) s.Roll += rollRate * dt;
                if (heldKeys.Contains(Keys.Up)) s.Pitch -= pitchRate * dt;   // push = nose down
                if (heldKeys.Contains(Keys.Down)) s.Pitch += pitchRate * dt; // pull = nose up
                s.Roll = Math.Clamp(s.Roll, -80f, 80f);
                s.Pitch = Math.Clamp(s.Pitch, -40f, 40f);
                break;

            case SourceMode.Serial when link != null:
                // GyroX/GyroY are the keys the original sketch sent.
                if (link.TryGet(out double v, "Roll", "GyroX", "AngleX")) s.Roll = (float)(invertRoll.Checked ? -v : v);
                if (link.TryGet(out v, "Pitch", "GyroY", "AngleY")) s.Pitch = (float)(invertPitch.Checked ? -v : v);
                if (link.TryGet(out v, "Heading", "Yaw", "Hdg")) { s.Heading = FlightModel.Wrap360((float)v); haveHeading = true; }
                if (link.TryGet(out v, "Altitude", "Alt")) { s.Altitude = (float)v; haveAltitude = true; }
                if (link.TryGet(out v, "Airspeed", "Speed", "IAS")) { s.Airspeed = (float)v; haveAirspeed = true; }
                if (link.TryGet(out v, "Throttle")) s.Throttle = Math.Clamp((float)v / 100f, 0f, 1f);
                if (link.TryGet(out v, "Fuel")) s.Fuel = (float)v;
                if (link.TryGet(out v, "EngineTemp", "EGT", "Temp")) s.EngineTemp = (float)v;
                if (link.TryGet(out v, "Gear") && (v != 0) != s.GearDown) SetGear(v != 0);
                AppendLog(link.DrainLines());
                break;
        }

        model.Step(dt, integrateHeading: !haveHeading, integrateAltitude: !haveAltitude, integrateAirspeed: !haveAirspeed);

        if (mode == SourceMode.Serial && link != null)
        {
            if (link.TryGet(out double lat, "Lat", "Latitude")) s.Latitude = lat;
            if (link.TryGet(out double lon, "Lon", "Lng", "Longitude")) s.Longitude = lon;
        }
        s.AngleOfAttack = mode == SourceMode.Serial && link != null && link.TryGet(out double aoa, "AoA", "AngleOfAttack")
            ? (float)aoa
            : FlightModel.EstimateAngleOfAttack(s);

        UpdateDerivedRates(s, dt);

        if (now - lastChartSample >= 0.25)
        {
            lastChartSample = now;
            altitudeChart.Add(s.Altitude);
            UpdateStatus();
        }

        airspeedGauge.Airspeed = s.Airspeed;
        attitude.Roll = s.Roll;
        attitude.Pitch = s.Pitch;
        altimeter.Altitude = s.Altitude;
        turnCoordinator.TurnRate = turnRate;
        turnCoordinator.Slip = slip;
        heading.Heading = s.Heading;
        vsi.VerticalSpeed = verticalSpeed;
        throttleLever.Value = s.Throttle * 100f;
        fuelGauge.Value = s.Fuel;
        tempGauge.Value = s.EngineTemp;
        UpdateGearStatus(s, now);
        UpdateAnnunciator(s);
        UpdateReadouts(s);
    }

    /// <summary>Vertical speed, turn rate and slip ball, derived from whatever source is flying.</summary>
    void UpdateDerivedRates(FlightState s, float dt)
    {
        float smoothing = Math.Min(1f, dt * 3f);
        if (!float.IsNaN(prevAltitude))
            verticalSpeed += ((s.Altitude - prevAltitude) / dt * 60f - verticalSpeed) * smoothing;
        prevAltitude = s.Altitude;

        if (!float.IsNaN(prevHeading))
        {
            float delta = ((s.Heading - prevHeading) % 360f + 540f) % 360f - 180f; // shortest way round
            turnRate += (delta / dt - turnRate) * smoothing;
        }
        prevHeading = s.Heading;

        // Ball swings opposite to roll rate (adverse yaw) and settles when the turn is steady.
        float rollRate = (s.Roll - prevRoll) / dt;
        prevRoll = s.Roll;
        slip += (Math.Clamp(-rollRate / 40f, -1f, 1f) - slip) * Math.Min(1f, dt * 2.5f);
    }

    void UpdateAnnunciator(FlightState s)
    {
        annunciator.SetActive("STALL", s.Airspeed < FlightModel.StallSpeed || s.AngleOfAttack > 15f);
        annunciator.SetActive("OVERSPEED", s.Airspeed >= FlightModel.NeverExceed);
        annunciator.SetActive("PULL UP", s.Altitude < 1000f && verticalSpeed < -1500f);
        annunciator.SetActive("BANK ANGLE", Math.Abs(s.Roll) > 45f);
        annunciator.SetActive("GEAR", (s.GearDown && s.Airspeed > FlightModel.GearMaxSpeed) ||
                                      (!s.GearDown && s.Altitude < 1000f && verticalSpeed < 0));
        annunciator.SetActive("LOW FUEL", s.Fuel <= 20f);
    }

    void UpdateReadouts(FlightState s)
    {
        void Set(string key, string text)
        {
            var label = readouts[key];
            if (label.Text != text) label.Text = text;
        }
        Set("Latitude", $"{Math.Abs(s.Latitude):0.00000}° {(s.Latitude >= 0 ? 'N' : 'S')}");
        Set("Longitude", $"{Math.Abs(s.Longitude):0.00000}° {(s.Longitude >= 0 ? 'E' : 'W')}");
        Set("Altitude", $"{s.Altitude:#,0} ft");
        Set("Vertical speed", $"{verticalSpeed:+#,0;-#,0;0} ft/min");
        Set("Airspeed", $"{s.Airspeed:0} kt");
        Set("Heading", $"{Math.Round(s.Heading) % 360:000}°");
        Set("Turn rate", $"{turnRate:+0.0;-0.0;0.0} °/s");
        Set("Roll", s.Roll >= 0.05f ? $"R {s.Roll:0.0}°" : s.Roll <= -0.05f ? $"L {-s.Roll:0.0}°" : "0.0°");
        Set("Pitch", $"{s.Pitch:+0.0;-0.0;0.0}°");
        Set("Angle of attack", $"{s.AngleOfAttack:0.0}°");
        Set("G-load", $"{FlightModel.LoadFactor(s):0.00} g");
        Set("Engine N1", $"{s.N1 * 100f:0} %");
    }

    void AppendLog(List<string> lines)
    {
        if (lines.Count == 0) return;
        logBox.AppendText(string.Join(Environment.NewLine, lines) + Environment.NewLine);
        if (logBox.TextLength > 20000)
        {
            logBox.Text = logBox.Text[^10000..];
            logBox.SelectionStart = logBox.TextLength;
            logBox.ScrollToCaret();
        }
    }
}
