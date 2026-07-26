using PKHeX.Core;

namespace OrganizerMod;

public sealed class OrganizerWindow : Form
{
    private readonly ISaveFileProvider saveFileProvider;
    private readonly Label saveInformation;

    public OrganizerWindow(ISaveFileProvider saveFileProvider)
    {
        this.saveFileProvider = saveFileProvider
            ?? throw new ArgumentNullException(nameof(saveFileProvider));

        Text = "Organizer Mod";
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(440, 190);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;

        var heading = new Label
        {
            AutoSize = true,
            Font = new Font(Font, FontStyle.Bold),
            Location = new Point(20, 20),
            Text = "Organizer Mod loaded successfully.",
        };

        saveInformation = new Label
        {
            AutoSize = false,
            Location = new Point(20, 55),
            Size = new Size(400, 85),
        };

        var closeButton = new Button
        {
            DialogResult = DialogResult.Cancel,
            Location = new Point(330, 145),
            Size = new Size(90, 30),
            Text = "Close",
        };

        Controls.Add(heading);
        Controls.Add(saveInformation);
        Controls.Add(closeButton);
        CancelButton = closeButton;

        RefreshSaveInfo();
    }

    public void RefreshSaveInfo()
    {
        try
        {
            var save = saveFileProvider.SAV;
            saveInformation.Text =
                $"Loaded save: {save.GetType().Name}{Environment.NewLine}" +
                $"Game: {save.Version}  |  Generation: {save.Generation}{Environment.NewLine}" +
                $"Trainer: {save.OT}  |  Boxes: {save.BoxCount}{Environment.NewLine}" +
                "No save data is modified by this window.";
        }
        catch (Exception ex)
        {
            saveInformation.Text =
                $"Save information is currently unavailable ({ex.GetType().Name}).{Environment.NewLine}" +
                "No save data is modified by this window.";
        }
    }
}
