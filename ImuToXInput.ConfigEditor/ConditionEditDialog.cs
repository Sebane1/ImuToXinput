using ImuToXInput.Config;

namespace ImuToXInput.ConfigEditor;

public sealed class ConditionEditDialog : Form
{
    private readonly ComboBox _cmbType;
    private readonly Panel _panelFields;
    private readonly ComboBox _cmbTracker;
    private readonly ComboBox _cmbTrackerA;
    private readonly ComboBox _cmbTrackerB;
    private readonly ComboBox _cmbComponent;
    private readonly ComboBox _cmbSource;
    private readonly ComboBox _cmbOp;
    private readonly NumericUpDown _numValue;

    public MappingCondition? Result { get; private set; }

    public ConditionEditDialog(MappingCondition? existing)
    {
        Text = "Edit condition";
        Size = new Size(460, 380);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MinimizeBox = false;
        MaximizeBox = false;

        var y = 10;
        var lblType = new Label { Text = "Condition type:", Location = new Point(12, y), AutoSize = true };
        _cmbType = new ComboBox
        {
            Location = new Point(12, y + 20),
            Width = 320,
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        _cmbType.Items.AddRange(ConfigEditorConstants.ConditionTypes);
        _cmbType.SelectedIndexChanged += (_, _) => RefreshConditionFields();

        y += 55;
        var lblFields = new Label { Text = "Parameters:", Location = new Point(12, y), AutoSize = true };
        _panelFields = new Panel { Location = new Point(12, y + 20), Size = new Size(420, 200), BorderStyle = BorderStyle.FixedSingle };

        _cmbTracker = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 160 };
        _cmbTrackerA = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 160 };
        _cmbTrackerB = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 160 };
        _cmbComponent = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 80 };
        _cmbSource = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 160 };
        _cmbOp = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 220 };
        _cmbOp.Items.AddRange(ConfigEditorConstants.Operators);
        _numValue = new NumericUpDown { Width = 80, DecimalPlaces = 2, Minimum = -1000, Maximum = 1000 };

        _cmbTracker.Items.AddRange(ConfigEditorConstants.TrackerIds);
        _cmbTrackerA.Items.AddRange(ConfigEditorConstants.TrackerIds);
        _cmbTrackerB.Items.AddRange(ConfigEditorConstants.TrackerIds);
        _cmbComponent.Items.AddRange(ConfigEditorConstants.EulerComponents);
        _cmbSource.Items.AddRange(ConfigEditorConstants.PositionSources);

        var btnOk = new Button { Text = "OK", DialogResult = DialogResult.OK, Location = new Point(260, 290), Size = new Size(80, 28) };
        var btnCancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Location = new Point(348, 290), Size = new Size(80, 28) };
        AcceptButton = btnOk;
        CancelButton = btnCancel;

        Controls.Add(lblType);
        Controls.Add(_cmbType);
        Controls.Add(lblFields);
        Controls.Add(_panelFields);
        Controls.Add(btnOk);
        Controls.Add(btnCancel);

        if (existing != null)
            LoadCondition(existing);
        else
            _cmbType.SelectedIndex = 0;

        RefreshConditionFields();
    }

    private void LoadCondition(MappingCondition c)
    {
        if (c is EulerThresholdCondition et)
        {
            _cmbType.SelectedItem = "euler_threshold";
            _cmbTracker.SelectedItem = et.Tracker;
            _cmbComponent.SelectedItem = et.Component;
            _cmbOp.SelectedItem = et.Op;
            _numValue.Value = (decimal)et.Value;
        }
        else if (c is EulerDiffCondition ed)
        {
            _cmbType.SelectedItem = "euler_diff";
            _cmbTrackerA.SelectedItem = ed.TrackerA;
            _cmbTrackerB.SelectedItem = ed.TrackerB;
            _cmbComponent.SelectedItem = ed.Component;
            _cmbOp.SelectedItem = ed.Op;
            _numValue.Value = (decimal)ed.Value;
        }
        else if (c is EulerSumCondition es)
        {
            _cmbType.SelectedItem = "euler_sum";
            _cmbTrackerA.SelectedItem = es.TrackerA;
            _cmbTrackerB.SelectedItem = es.TrackerB;
            _cmbComponent.SelectedItem = es.Component;
            _cmbOp.SelectedItem = es.Op;
            _numValue.Value = (decimal)es.Value;
        }
        else if (c is PositionThresholdCondition pt)
        {
            _cmbType.SelectedItem = "position_threshold";
            _cmbTracker.SelectedItem = pt.Tracker;
            _cmbSource.SelectedItem = pt.Source;
            _cmbOp.SelectedItem = pt.Op;
            _numValue.Value = (decimal)pt.Value;
        }
    }

    private const int LabelLeft = 5;
    private const int ControlLeft = 145;
    private const int RowHeight = 30;

    private void RefreshConditionFields()
    {
        _panelFields.Controls.Clear();
        var type = _cmbType.SelectedItem?.ToString() ?? "euler_threshold";
        var row = 6;

        if (type == "euler_threshold")
        {
            _panelFields.Controls.Add(new Label { Text = "Tracker:", Location = new Point(LabelLeft, row) });
            _cmbTracker.Location = new Point(ControlLeft, row); _panelFields.Controls.Add(_cmbTracker);
            row += RowHeight;
            _panelFields.Controls.Add(new Label { Text = "Component (X/Y/Z):", Location = new Point(LabelLeft, row) });
            _cmbComponent.Location = new Point(ControlLeft, row); _panelFields.Controls.Add(_cmbComponent);
            row += RowHeight;
            _panelFields.Controls.Add(new Label { Text = "Operator:", Location = new Point(LabelLeft, row) });
            _cmbOp.Location = new Point(ControlLeft, row); _panelFields.Controls.Add(_cmbOp);
            row += RowHeight;
            _panelFields.Controls.Add(new Label { Text = "Value:", Location = new Point(LabelLeft, row) });
            _numValue.Location = new Point(ControlLeft, row); _panelFields.Controls.Add(_numValue);
        }
        else if (type == "euler_diff" || type == "euler_sum")
        {
            _panelFields.Controls.Add(new Label { Text = "Tracker A:", Location = new Point(LabelLeft, row) });
            _cmbTrackerA.Location = new Point(ControlLeft, row); _panelFields.Controls.Add(_cmbTrackerA);
            row += RowHeight;
            _panelFields.Controls.Add(new Label { Text = "Tracker B:", Location = new Point(LabelLeft, row) });
            _cmbTrackerB.Location = new Point(ControlLeft, row); _panelFields.Controls.Add(_cmbTrackerB);
            row += RowHeight;
            _panelFields.Controls.Add(new Label { Text = "Component:", Location = new Point(LabelLeft, row) });
            _cmbComponent.Location = new Point(ControlLeft, row); _panelFields.Controls.Add(_cmbComponent);
            row += RowHeight;
            _panelFields.Controls.Add(new Label { Text = "Operator:", Location = new Point(LabelLeft, row) });
            _cmbOp.Location = new Point(ControlLeft, row); _panelFields.Controls.Add(_cmbOp);
            row += RowHeight;
            _panelFields.Controls.Add(new Label { Text = "Value:", Location = new Point(LabelLeft, row) });
            _numValue.Location = new Point(ControlLeft, row); _panelFields.Controls.Add(_numValue);
        }
        else
        {
            _panelFields.Controls.Add(new Label { Text = "Tracker:", Location = new Point(LabelLeft, row) });
            _cmbTracker.Location = new Point(ControlLeft, row); _panelFields.Controls.Add(_cmbTracker);
            row += RowHeight;
            _panelFields.Controls.Add(new Label { Text = "Source:", Location = new Point(LabelLeft, row) });
            _cmbSource.Location = new Point(ControlLeft, row); _panelFields.Controls.Add(_cmbSource);
            row += RowHeight;
            _panelFields.Controls.Add(new Label { Text = "Operator:", Location = new Point(LabelLeft, row) });
            _cmbOp.Location = new Point(ControlLeft, row); _panelFields.Controls.Add(_cmbOp);
            row += RowHeight;
            _panelFields.Controls.Add(new Label { Text = "Value:", Location = new Point(LabelLeft, row) });
            _numValue.Location = new Point(ControlLeft, row); _panelFields.Controls.Add(_numValue);
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (DialogResult == DialogResult.OK)
            Result = BuildCondition();
        base.OnFormClosing(e);
    }

    private MappingCondition? BuildCondition()
    {
        var type = _cmbType.SelectedItem?.ToString() ?? "";
        var op = _cmbOp.SelectedItem?.ToString() ?? "greater_than";
        var value = (float)_numValue.Value;

        return type switch
        {
            "euler_threshold" => new EulerThresholdCondition
            {
                Type = type,
                Tracker = _cmbTracker.SelectedItem?.ToString() ?? "",
                Component = _cmbComponent.SelectedItem?.ToString() ?? "X",
                Op = op,
                Value = value
            },
            "euler_diff" => new EulerDiffCondition
            {
                Type = type,
                TrackerA = _cmbTrackerA.SelectedItem?.ToString() ?? "",
                TrackerB = _cmbTrackerB.SelectedItem?.ToString() ?? "",
                Component = _cmbComponent.SelectedItem?.ToString() ?? "X",
                Op = op,
                Value = value
            },
            "euler_sum" => new EulerSumCondition
            {
                Type = type,
                TrackerA = _cmbTrackerA.SelectedItem?.ToString() ?? "",
                TrackerB = _cmbTrackerB.SelectedItem?.ToString() ?? "",
                Component = _cmbComponent.SelectedItem?.ToString() ?? "X",
                Op = op,
                Value = value
            },
            "position_threshold" => new PositionThresholdCondition
            {
                Type = type,
                Tracker = _cmbTracker.SelectedItem?.ToString() ?? "",
                Source = _cmbSource.SelectedItem?.ToString() ?? "FloorRelY",
                Op = op,
                Value = value
            },
            _ => null
        };
    }
}
