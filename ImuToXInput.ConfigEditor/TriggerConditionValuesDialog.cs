namespace ImuToXInput.ConfigEditor;

public sealed class TriggerConditionValuesDialog : Form
{
    private readonly NumericUpDown _numWhenTrue;
    private readonly NumericUpDown _numWhenFalse;

    public byte ValueWhenTrue => (byte)_numWhenTrue.Value;
    public byte ValueWhenFalse => (byte)_numWhenFalse.Value;

    public TriggerConditionValuesDialog(byte valueWhenTrue = 255, byte valueWhenFalse = 0)
    {
        Text = "Trigger output values";
        Size = new Size(280, 160);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        MaximizeBox = false;

        var lblTrue = new Label { Text = "When condition true (0–255):", Location = new Point(12, 14), AutoSize = true };
        _numWhenTrue = new NumericUpDown
        {
            Location = new Point(12, 32),
            Width = 80,
            Minimum = 0,
            Maximum = 255,
            Value = valueWhenTrue,
            DecimalPlaces = 0
        };
        var lblFalse = new Label { Text = "When condition false (0–255):", Location = new Point(12, 58), AutoSize = true };
        _numWhenFalse = new NumericUpDown
        {
            Location = new Point(12, 76),
            Width = 80,
            Minimum = 0,
            Maximum = 255,
            Value = valueWhenFalse,
            DecimalPlaces = 0
        };
        var btnOk = new Button { Text = "OK", Location = new Point(12, 108), Width = 75, DialogResult = DialogResult.OK };
        var btnCancel = new Button { Text = "Cancel", Location = new Point(93, 108), Width = 75, DialogResult = DialogResult.Cancel };
        AcceptButton = btnOk;
        CancelButton = btnCancel;

        Controls.Add(lblTrue);
        Controls.Add(_numWhenTrue);
        Controls.Add(lblFalse);
        Controls.Add(_numWhenFalse);
        Controls.Add(btnOk);
        Controls.Add(btnCancel);
    }
}
