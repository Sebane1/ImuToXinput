namespace ImuToXInput.ConfigEditor;

partial class MainForm
{
    private System.ComponentModel.IContainer components = null;

    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null))
            components.Dispose();
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        lblFolder = new Label();
        listConfigs = new ListBox();
        btnBrowse = new Button();
        btnNew = new Button();
        btnEdit = new Button();
        btnDelete = new Button();
        btnOpenFolder = new Button();
        SuspendLayout();
        //
        // lblFolder
        //
        lblFolder.AutoSize = true;
        lblFolder.Location = new Point(12, 9);
        lblFolder.Name = "lblFolder";
        lblFolder.Size = new Size(120, 15);
        lblFolder.TabIndex = 0;
        lblFolder.Text = "Config folder: (not set)";
        //
        // listConfigs
        //
        listConfigs.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        listConfigs.FormattingEnabled = true;
        listConfigs.ItemHeight = 15;
        listConfigs.Location = new Point(12, 30);
        listConfigs.Name = "listConfigs";
        listConfigs.Size = new Size(360, 259);
        listConfigs.TabIndex = 1;
        listConfigs.DoubleClick += listConfigs_DoubleClick;
        //
        // btnBrowse
        //
        btnBrowse.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        btnBrowse.Location = new Point(378, 27);
        btnBrowse.Name = "btnBrowse";
        btnBrowse.Size = new Size(94, 25);
        btnBrowse.TabIndex = 2;
        btnBrowse.Text = "Browse...";
        btnBrowse.UseVisualStyleBackColor = true;
        btnBrowse.Click += btnBrowse_Click;
        //
        // btnNew
        //
        btnNew.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
        btnNew.Location = new Point(378, 230);
        btnNew.Name = "btnNew";
        btnNew.Size = new Size(94, 28);
        btnNew.TabIndex = 3;
        btnNew.Text = "New";
        btnNew.UseVisualStyleBackColor = true;
        btnNew.Click += btnNew_Click;
        //
        // btnEdit
        //
        btnEdit.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
        btnEdit.Location = new Point(378, 264);
        btnEdit.Name = "btnEdit";
        btnEdit.Size = new Size(94, 28);
        btnEdit.TabIndex = 4;
        btnEdit.Text = "Edit";
        btnEdit.UseVisualStyleBackColor = true;
        btnEdit.Click += btnEdit_Click;
        //
        // btnDelete
        //
        btnDelete.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
        btnDelete.Location = new Point(378, 298);
        btnDelete.Name = "btnDelete";
        btnDelete.Size = new Size(94, 28);
        btnDelete.TabIndex = 5;
        btnDelete.Text = "Delete";
        btnDelete.UseVisualStyleBackColor = true;
        btnDelete.Click += btnDelete_Click;
        //
        // btnOpenFolder
        //
        btnOpenFolder.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        btnOpenFolder.Location = new Point(378, 58);
        btnOpenFolder.Name = "btnOpenFolder";
        btnOpenFolder.Size = new Size(94, 25);
        btnOpenFolder.TabIndex = 6;
        btnOpenFolder.Text = "Open folder";
        btnOpenFolder.UseVisualStyleBackColor = true;
        btnOpenFolder.Click += btnOpenFolder_Click;
        //
        // MainForm
        //
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(484, 341);
        Controls.Add(btnOpenFolder);
        Controls.Add(btnDelete);
        Controls.Add(btnEdit);
        Controls.Add(btnNew);
        Controls.Add(btnBrowse);
        Controls.Add(listConfigs);
        Controls.Add(lblFolder);
        MinimumSize = new Size(400, 320);
        Name = "MainForm";
        StartPosition = FormStartPosition.CenterScreen;
        Text = "ImuToXInput Config Editor";
        ResumeLayout(false);
        PerformLayout();
    }

    private Label lblFolder;
    private ListBox listConfigs;
    private Button btnBrowse;
    private Button btnNew;
    private Button btnEdit;
    private Button btnDelete;
    private Button btnOpenFolder;
}
