using ImuToXInput.Core;

namespace ImuToXInput.ConfigEditor;

public class TriggerAxisDialog : Form
{
    private readonly ComboBox _cmbTracker;
    private readonly ComboBox _cmbSource;
    private readonly NumericUpDown _numScale;
    private readonly CheckBox _chkInvert;

    public string Tracker => _cmbTracker.SelectedItem?.ToString() ?? "";
    public string Source => _cmbSource.SelectedItem?.ToString() ?? "";
    public float AxisScale => (float)_numScale.Value;
    public bool Invert => _chkInvert.Checked;

    public TriggerAxisDialog(string? initialTracker = null, string? initialSource = null, float initialScale = 1f, bool initialInvert = false)
    {
        Text = "Trigger from axis";
        Size = new Size(280, 200);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        MaximizeBox = false;

        var lblT = new Label { Text = "Tracker:", Location = new Point(12, 12), AutoSize = true };
        _cmbTracker = new ComboBox { Location = new Point(12, 30), Width = 200, DropDownStyle = ComboBoxStyle.DropDownList };
        _cmbTracker.Items.AddRange(ConfigEditorConstants.TrackerIds);
        SelectItem(_cmbTracker, initialTracker);

        var lblS = new Label { Text = "Source:", Location = new Point(12, 58), AutoSize = true };
        _cmbSource = new ComboBox { Location = new Point(12, 76), Width = 200, DropDownStyle = ComboBoxStyle.DropDownList };
        _cmbSource.Items.AddRange(ConfigEditorConstants.AxisSources);
        SelectItem(_cmbSource, initialSource);

        var lblScale = new Label { Text = "Scale:", Location = new Point(12, 100), AutoSize = true };
        _numScale = new NumericUpDown { Location = new Point(12, 118), Width = 80, Minimum = 0.01m, Maximum = 10m, DecimalPlaces = 2, Increment = 0.1m, Value = (decimal)Math.Clamp(initialScale, 0.01f, 10f) };
        _chkInvert = new CheckBox { Text = "Invert", Location = new Point(110, 118), AutoSize = true, Checked = initialInvert };

        var btnOk = new Button { Text = "OK", Location = new Point(12, 152), Width = 75, DialogResult = DialogResult.OK };
        var btnCancel = new Button { Text = "Cancel", Location = new Point(93, 152), Width = 75, DialogResult = DialogResult.Cancel };
        AcceptButton = btnOk;
        CancelButton = btnCancel;

        Controls.Add(lblT);
        Controls.Add(_cmbTracker);
        Controls.Add(lblS);
        Controls.Add(_cmbSource);
        Controls.Add(lblScale);
        Controls.Add(_numScale);
        Controls.Add(_chkInvert);
        Controls.Add(btnOk);
        Controls.Add(btnCancel);
    }

    private static void SelectItem(ComboBox cmb, string? value)
    {
        if (string.IsNullOrEmpty(value)) { if (cmb.Items.Count > 0) cmb.SelectedIndex = 0; return; }
        for (int i = 0; i < cmb.Items.Count; i++)
        {
            if (string.Equals(cmb.Items[i]?.ToString(), value, StringComparison.OrdinalIgnoreCase))
            {
                cmb.SelectedIndex = i;
                return;
            }
        }
        if (cmb.Items.Count > 0) cmb.SelectedIndex = 0;
    }
}
