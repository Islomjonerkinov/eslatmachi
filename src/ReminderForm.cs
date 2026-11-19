using System;
using System.Drawing;
using System.Media;
using System.Windows.Forms;

namespace Eslatmachi
{
    public class ReminderForm : Form
    {
        public bool SnoozeRequested { get; private set; }

        public ReminderForm(string title, string notes, DateTime reminderTime)
        {
            Text = "Eslatma — " + title;
            Name = "ReminderDialog";
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(540, 390);
            BackColor = Color.White;
            Font = new Font("Segoe UI", 10F);
            TopMost = true;

            Panel header = new Panel();
            header.Dock = DockStyle.Top;
            header.Height = 92;
            header.BackColor = Color.FromArgb(15, 118, 110);
            Controls.Add(header);

            PictureBox iconBox = new PictureBox();
            iconBox.Image = SystemIcons.Information.ToBitmap();
            iconBox.SizeMode = PictureBoxSizeMode.StretchImage;
            iconBox.SetBounds(24, 22, 46, 46);
            header.Controls.Add(iconBox);

            Label headerLabel = new Label();
            headerLabel.Text = "Eslatma vaqti keldi";
            headerLabel.ForeColor = Color.White;
            headerLabel.Font = new Font("Segoe UI Semibold", 20F, FontStyle.Bold);
            headerLabel.AutoSize = true;
            headerLabel.Location = new Point(84, 28);
            header.Controls.Add(headerLabel);

            Label titleLabel = new Label();
            titleLabel.Name = "ReminderTitle";
            titleLabel.Text = title;
            titleLabel.Font = new Font("Segoe UI Semibold", 17F, FontStyle.Bold);
            titleLabel.ForeColor = Color.FromArgb(24, 38, 55);
            titleLabel.SetBounds(26, 112, 488, 65);
            titleLabel.AutoEllipsis = true;
            Controls.Add(titleLabel);

            Label timeLabel = new Label();
            timeLabel.Text = reminderTime.ToString("dd.MM.yyyy  HH:mm");
            timeLabel.ForeColor = Color.FromArgb(15, 118, 110);
            timeLabel.Font = new Font("Segoe UI Semibold", 11F, FontStyle.Bold);
            timeLabel.AutoSize = true;
            timeLabel.Location = new Point(28, 180);
            Controls.Add(timeLabel);

            Label notesLabel = new Label();
            notesLabel.Text = string.IsNullOrWhiteSpace(notes) ? "Qo‘shimcha izoh yo‘q." : notes;
            notesLabel.ForeColor = Color.FromArgb(91, 104, 119);
            notesLabel.SetBounds(28, 214, 484, 65);
            notesLabel.AutoEllipsis = true;
            Controls.Add(notesLabel);

            Button snoozeButton = MakeButton("10 daqiqa kechiktirish", new Rectangle(26, 303, 205, 48), Color.FromArgb(232, 241, 240), Color.FromArgb(15, 95, 90));
            snoozeButton.Name = "SnoozeButton";
            snoozeButton.Click += delegate
            {
                SnoozeRequested = true;
                DialogResult = DialogResult.OK;
                Close();
            };
            Controls.Add(snoozeButton);

            Button doneButton = MakeButton("Bajarildi", new Rectangle(244, 303, 130, 48), Color.FromArgb(15, 118, 110), Color.White);
            doneButton.Name = "DoneButton";
            doneButton.DialogResult = DialogResult.OK;
            Controls.Add(doneButton);

            Button closeButton = MakeButton("Yopish", new Rectangle(386, 303, 128, 48), Color.FromArgb(238, 241, 244), Color.FromArgb(70, 82, 95));
            closeButton.Name = "CloseReminderButton";
            closeButton.DialogResult = DialogResult.Cancel;
            Controls.Add(closeButton);

            AcceptButton = doneButton;
            CancelButton = closeButton;
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            SystemSounds.Asterisk.Play();
            Activate();
        }

        private static Button MakeButton(string text, Rectangle bounds, Color background, Color foreground)
        {
            Button button = new Button();
            button.Text = text;
            button.SetBounds(bounds.X, bounds.Y, bounds.Width, bounds.Height);
            button.BackColor = background;
            button.ForeColor = foreground;
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.Cursor = Cursors.Hand;
            return button;
        }
    }
}
