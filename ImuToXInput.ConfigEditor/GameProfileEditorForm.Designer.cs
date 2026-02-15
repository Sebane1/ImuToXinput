namespace ImuToXInput.ConfigEditor;

partial class GameProfileEditorForm
{
    private System.ComponentModel.IContainer components = null;
    private Label lblName;
    private TextBox txtName;
    private Label lblFileName;
    private TextBox txtFileName;
    private Label lblProcessNames;
    private TextBox txtProcessName;
    private ListBox listProcessNames;
    private Button btnAddProcessName;
    private Button btnRemoveProcessName;
    private Label lblFloorTrackers;
    private ComboBox cmbFloorTracker;
    private ListBox listFloorTrackers;
    private Button btnAddFloorTracker;
    private Button btnRemoveFloorTracker;
    private TabControl tabControl;
    private TabPage tabAxis;
    private TabPage tabButtons;
    private TabPage tabTriggers;
    private DataGridView dgvAxis;
    private Button btnAddAxis;
    private Button btnRemoveAxis;
    private ListBox listButtons;
    private ComboBox cmbButton;
    private Button btnAddButton;
    private Button btnEditButton;
    private Button btnRemoveButton;
    private ListBox listTriggers;
    private ComboBox cmbTrigger;
    private Button btnAddTrigger;
    private Button btnAddFixedTrigger;
    private Button btnAddAxisTrigger;
    private Button btnEditTrigger;
    private Button btnRemoveTrigger;
    private Button btnSave;
    private Button btnCancel;
    private TabPage tabScript;
    private RichTextBox txtScript;
    private Button btnRefreshFromForm;
    private Button btnApplyScript;

    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null))
            components.Dispose();
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        ClientSize = new Size(584, 751);
        MinimumSize = new Size(500, 700);

        lblName = new Label { Text = "Profile name:", Location = new Point(12, 12), AutoSize = true };
        txtName = new TextBox { Location = new Point(12, 30), Width = 280 };
        lblFileName = new Label { Text = "File name (e.g. mygame.json):", Location = new Point(12, 55), AutoSize = true, Visible = false };
        txtFileName = new TextBox { Location = new Point(12, 73), Width = 280, Visible = false };

        lblProcessNames = new Label { Text = "Process names (game .exe without extension):", Location = new Point(12, 100), AutoSize = true };
        txtProcessName = new TextBox { Location = new Point(12, 118), Width = 180 };
        btnAddProcessName = new Button { Text = "Add", Location = new Point(198, 116), Width = 60 };
        btnAddProcessName.Click += btnAddProcessName_Click;
        listProcessNames = new ListBox { Location = new Point(12, 144), Width = 246, Height = 70 };
        btnRemoveProcessName = new Button { Text = "Remove", Location = new Point(264, 144), Width = 70 };
        btnRemoveProcessName.Click += btnRemoveProcessName_Click;

        lblFloorTrackers = new Label { Text = "Trackers (for floor-relative position):", Location = new Point(12, 220), AutoSize = true };
        cmbFloorTracker = new ComboBox { Location = new Point(12, 238), Width = 180, DropDownStyle = ComboBoxStyle.DropDownList };
        cmbFloorTracker.Items.AddRange(ConfigEditorConstants.FloorTrackerIds);
        if (cmbFloorTracker.Items.Count > 0) cmbFloorTracker.SelectedIndex = 0;
        btnAddFloorTracker = new Button { Text = "Add", Location = new Point(198, 236), Width = 60 };
        btnAddFloorTracker.Click += btnAddFloorTracker_Click;
        listFloorTrackers = new ListBox { Location = new Point(12, 264), Width = 246, Height = 50 };
        btnRemoveFloorTracker = new Button { Text = "Remove", Location = new Point(264, 264), Width = 70 };
        btnRemoveFloorTracker.Click += btnRemoveFloorTracker_Click;

        tabControl = new TabControl { Location = new Point(12, 320), Size = new Size(560, 380), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };

        tabAxis = new TabPage("Axis mappings");
        dgvAxis = new DataGridView
        {
            Location = new Point(6, 6),
            Size = new Size(520, 180),
            AllowUserToAddRows = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
        };
        var colTracker = new DataGridViewComboBoxColumn { Name = "Tracker", HeaderText = "Tracker", Width = 120 };
        colTracker.Items.AddRange(ConfigEditorConstants.TrackerIds);
        dgvAxis.Columns.Add(colTracker);
        var colSource = new DataGridViewComboBoxColumn { Name = "Source", HeaderText = "Source", Width = 100 };
        colSource.Items.AddRange(ConfigEditorConstants.AxisSources);
        dgvAxis.Columns.Add(colSource);
        dgvAxis.Columns.Add(new DataGridViewTextBoxColumn { Name = "Scale", HeaderText = "Scale", Width = 60 });
        dgvAxis.Columns.Add(new DataGridViewCheckBoxColumn { Name = "Invert", HeaderText = "Invert", Width = 50 });
        var colAxis = new DataGridViewComboBoxColumn { Name = "Axis", HeaderText = "Axis", Width = 100 };
        colAxis.Items.AddRange(ConfigEditorConstants.AxisOutputs);
        dgvAxis.Columns.Add(colAxis);
        btnAddAxis = new Button { Text = "Add row", Location = new Point(6, 192), Width = 80 };
        btnAddAxis.Click += btnAddAxis_Click;
        btnRemoveAxis = new Button { Text = "Remove row", Location = new Point(92, 192), Width = 80 };
        btnRemoveAxis.Click += btnRemoveAxis_Click;
        tabAxis.Controls.Add(dgvAxis);
        tabAxis.Controls.Add(btnAddAxis);
        tabAxis.Controls.Add(btnRemoveAxis);

        tabButtons = new TabPage("Button mappings");
        listButtons = new ListBox { Location = new Point(6, 6), Size = new Size(400, 180) };
        cmbButton = new ComboBox { Location = new Point(6, 192), Width = 120, DropDownStyle = ComboBoxStyle.DropDownList };
        cmbButton.Items.AddRange(ConfigEditorConstants.Buttons);
        if (cmbButton.Items.Count > 0) cmbButton.SelectedIndex = 0;
        btnAddButton = new Button { Text = "Add", Location = new Point(132, 190), Width = 60 };
        btnAddButton.Click += btnAddButton_Click;
        btnEditButton = new Button { Text = "Edit", Location = new Point(198, 190), Width = 60 };
        btnEditButton.Click += btnEditButton_Click;
        btnRemoveButton = new Button { Text = "Remove", Location = new Point(264, 190), Width = 60 };
        btnRemoveButton.Click += btnRemoveButton_Click;
        tabButtons.Controls.Add(listButtons);
        tabButtons.Controls.Add(cmbButton);
        tabButtons.Controls.Add(btnAddButton);
        tabButtons.Controls.Add(btnEditButton);
        tabButtons.Controls.Add(btnRemoveButton);

        tabTriggers = new TabPage("Trigger mappings");
        listTriggers = new ListBox { Location = new Point(6, 6), Size = new Size(400, 180) };
        cmbTrigger = new ComboBox { Location = new Point(6, 192), Width = 120, DropDownStyle = ComboBoxStyle.DropDownList };
        cmbTrigger.Items.AddRange(ConfigEditorConstants.Triggers);
        if (cmbTrigger.Items.Count > 0) cmbTrigger.SelectedIndex = 0;
        btnAddTrigger = new Button { Text = "Add (cond)", Location = new Point(132, 190), Width = 70 };
        btnAddTrigger.Click += btnAddTrigger_Click;
        btnAddFixedTrigger = new Button { Text = "Fixed", Location = new Point(208, 190), Width = 50 };
        btnAddFixedTrigger.Click += btnAddFixedTrigger_Click;
        btnAddAxisTrigger = new Button { Text = "Axis", Location = new Point(264, 190), Width = 50 };
        btnAddAxisTrigger.Click += btnAddAxisTrigger_Click;
        btnEditTrigger = new Button { Text = "Edit", Location = new Point(320, 190), Width = 50 };
        btnEditTrigger.Click += btnEditTrigger_Click;
        btnRemoveTrigger = new Button { Text = "Remove", Location = new Point(376, 190), Width = 60 };
        btnRemoveTrigger.Click += btnRemoveTrigger_Click;
        tabTriggers.Controls.Add(listTriggers);
        tabTriggers.Controls.Add(cmbTrigger);
        tabTriggers.Controls.Add(btnAddTrigger);
        tabTriggers.Controls.Add(btnAddFixedTrigger);
        tabTriggers.Controls.Add(btnAddAxisTrigger);
        tabTriggers.Controls.Add(btnEditTrigger);
        tabTriggers.Controls.Add(btnRemoveTrigger);

        tabScript = new TabPage("Script");
        var scriptPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(6) };
        var scriptButtonPanel = new Panel { Height = 36, Dock = DockStyle.Bottom };
        btnRefreshFromForm = new Button { Text = "Refresh from form", Location = new Point(6, 6), Width = 120 };
        btnRefreshFromForm.Click += btnRefreshFromForm_Click;
        btnApplyScript = new Button { Text = "Apply script", Location = new Point(132, 6), Width = 100 };
        btnApplyScript.Click += btnApplyScript_Click;
        scriptButtonPanel.Controls.Add(btnRefreshFromForm);
        scriptButtonPanel.Controls.Add(btnApplyScript);
        txtScript = new RichTextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ScrollBars = RichTextBoxScrollBars.Both,
            WordWrap = false,
            Font = new Font("Consolas", 9F),
            BorderStyle = BorderStyle.FixedSingle
        };
        txtScript.TextChanged += txtScript_TextChanged;
        scriptPanel.Controls.Add(scriptButtonPanel);
        scriptPanel.Controls.Add(txtScript);
        tabScript.Controls.Add(scriptPanel);

        tabControl.TabPages.Add(tabAxis);
        tabControl.TabPages.Add(tabButtons);
        tabControl.TabPages.Add(tabTriggers);
        tabControl.TabPages.Add(tabScript);

        btnSave = new Button { Text = "Save", Location = new Point(392, 708), Width = 80, DialogResult = DialogResult.OK, Anchor = AnchorStyles.Bottom | AnchorStyles.Right };
        btnSave.Click += btnSave_Click;
        btnCancel = new Button { Text = "Cancel", Location = new Point(478, 708), Width = 80, DialogResult = DialogResult.Cancel, Anchor = AnchorStyles.Bottom | AnchorStyles.Right };
        CancelButton = btnCancel;

        Controls.Add(lblName);
        Controls.Add(txtName);
        Controls.Add(lblFileName);
        Controls.Add(txtFileName);
        Controls.Add(lblProcessNames);
        Controls.Add(txtProcessName);
        Controls.Add(btnAddProcessName);
        Controls.Add(listProcessNames);
        Controls.Add(btnRemoveProcessName);
        Controls.Add(lblFloorTrackers);
        Controls.Add(cmbFloorTracker);
        Controls.Add(btnAddFloorTracker);
        Controls.Add(listFloorTrackers);
        Controls.Add(btnRemoveFloorTracker);
        Controls.Add(tabControl);
        Controls.Add(btnSave);
        Controls.Add(btnCancel);

        StartPosition = FormStartPosition.CenterParent;
        Text = "Edit game config";
        FormBorderStyle = FormBorderStyle.Sizable;
    }
}
