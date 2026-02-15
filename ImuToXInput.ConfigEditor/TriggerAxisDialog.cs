namespace ImuToXInput.ConfigEditor;

public class TriggerAxisDialog : Form
{
    private readonly ComboBox _cmbTracker;
    private readonly ComboBox _cmbSource;

    public string Tracker => _cmbTracker.SelectedItem?.ToString() ?? "";
    public string Source => _cmbSource.SelectedItem?.ToString() ?? "";

    public TriggerAxisDialog()
    {
        Text = "Trigger from axis";
        Size = new Size(280, 150);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        MaximizeBox = false;

        var lblT = new Label { Text = "Tracker:", Location = new Point(12, 12), AutoSize = true };
        _cmbTracker = new ComboBox { Location = new Point(12, 30), Width = 200, DropDownStyle = ComboBoxStyle.DropDownList };
        _cmbTracker.Items.AddRange(ConfigEditorConstants.TrackerIds);
        if (_cmbTracker.Items.Count > 0) _cmbTracker.SelectedIndex = 0;

        var lblS = new Label { Text = "Source:", Location = new Point(12, 58), AutoSize = true };
        _cmbSource = new ComboBox { Location = new Point(12, 76), Width = 200, DropDownStyle = ComboBoxStyle.DropDownList };
        _cmbSource.Items.AddRange(ConfigEditorConstants.AxisSources);
        if (_cmbSource.Items.Count > 0) _cmbSource.SelectedIndex = 0;

        var btnOk = new Button { Text = "OK", Location = new Point(12, 108), Width = 75, DialogResult = DialogResult.OK };
        var btnCancel = new Button { Text = "Cancel", Location = new Point(93, 108), Width = 75, DialogResult = DialogResult.Cancel };
        AcceptButton = btnOk;
        CancelButton = btnCancel;

        Controls.Add(lblT);
        Controls.Add(_cmbTracker);
        Controls.Add(lblS);
        Controls.Add(_cmbSource);
        Controls.Add(btnOk);
        Controls.Add(btnCancel);
    }
}
