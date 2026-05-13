using System.Windows.Forms;

var title = args.Length > 0 ? string.Join(" ", args) : "Program Hider Smoke Window";

ApplicationConfiguration.Initialize();
Application.Run(new SmokeForm(title));

internal sealed class SmokeForm : Form
{
    public SmokeForm(string title)
    {
        Text = title;
        StartPosition = FormStartPosition.CenterScreen;
        Width = 720;
        Height = 220;
        Shown += OnShown;

        var label = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI", 14F, FontStyle.Regular, GraphicsUnit.Point),
            Text = "Program Hider smoke target window"
        };

        Controls.Add(label);
    }

    private void OnShown(object? sender, EventArgs eventArgs)
    {
        BeginInvoke(
            () =>
            {
                TopMost = true;
                Activate();
                BringToFront();
                TopMost = false;
            });
    }
}
