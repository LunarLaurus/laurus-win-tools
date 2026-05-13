using System.Windows.Forms;

namespace ProgramHider;

// Simple credential prompt used for restore authorization.
internal sealed class PinPromptForm : Form
{
    private readonly TextBox _pinTextBox = new()
    {
        UseSystemPasswordChar = true,
        Width = 220
    };

    public PinPromptForm(string actionDescription)
    {
        Text = "Program Hider Unlock";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterScreen;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(360, 150);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            ColumnCount = 1,
            RowCount = 4
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        root.Controls.Add(
            new Label
            {
                Text = $"Enter the restore PIN/password to {actionDescription}.",
                AutoSize = true
            },
            0,
            0);

        root.Controls.Add(
            new Label
            {
                Text = "PIN/password",
                AutoSize = true,
                Margin = new Padding(0, 12, 0, 4)
            },
            0,
            1);

        root.Controls.Add(_pinTextBox, 0, 2);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true
        };

        var okButton = new Button
        {
            Text = "Unlock",
            AutoSize = true,
            DialogResult = DialogResult.None
        };
        okButton.Click += (_, _) => Submit();

        var cancelButton = new Button
        {
            Text = "&Cancel",
            AutoSize = true,
            DialogResult = DialogResult.Cancel
        };

        buttons.Controls.Add(okButton);
        buttons.Controls.Add(cancelButton);
        root.Controls.Add(buttons, 0, 3);

        Controls.Add(root);
        AcceptButton = okButton;
        CancelButton = cancelButton;
    }

    public string? EnteredSecret { get; private set; }

    private void Submit()
    {
        if (string.IsNullOrWhiteSpace(_pinTextBox.Text))
        {
            MessageBox.Show(
                "Enter the restore PIN/password.",
                "Program Hider",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        EnteredSecret = _pinTextBox.Text;
        DialogResult = DialogResult.OK;
        Close();
    }
}
