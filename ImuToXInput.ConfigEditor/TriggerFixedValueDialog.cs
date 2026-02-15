namespace ImuToXInput.ConfigEditor;

public class TriggerFixedValueDialog : Form
{
    private readonly NumericUpDown _numValue;

    public byte Value => (byte)_numValue.Value;

    public TriggerFixedValueDialog(byte initial = 128)
    {
        Text = "Fixed trigger value";
        Size = new Size(260, 120);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        MaximizeBox = false;

        var lbl = new Label { Text = "Value (0–255):", Location = new Point(12, 14), AutoSize = true };
        _numValue = new NumericUpDown
        {
            Location = new Point(12, 32),
            Width = 80,
            Minimum = 0,
            Maximum = 255,
            Value = initial,
            DecimalPlaces = 0
        };
        var btnOk = new Button { Text = "OK", Location = new Point(12, 62), Width = 75, DialogResult = DialogResult.OK };
        var btnCancel = new Button { Text = "Cancel", Location = new Point(93, 62), Width = 75, DialogResult = DialogResult.Cancel };
        AcceptButton = btnOk;
        CancelButton = btnCancel;

        Controls.Add(lbl);
        Controls.Add(_numValue);
        Controls.Add(btnOk);
        Controls.Add(btnCancel);
    }
}
