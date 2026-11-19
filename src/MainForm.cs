using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Microsoft.Win32;

namespace Eslatmachi
{
    public class MainForm : Form
    {
        private readonly ReminderStore store;
        private List<Reminder> reminders;
        private string editingId;
        private bool allowExit;
        private bool trayMessageShown;
        private bool updatingStartup;

        private Label editorHeading;
        private TextBox titleBox;
        private DateTimePicker datePicker;
        private DateTimePicker timePicker;
        private ComboBox repeatBox;
        private TextBox notesBox;
        private Button addButton;
        private Button cancelEditButton;
        private CheckBox startupCheck;
        private DataGridView grid;
        private Label emptyLabel;
        private Label countLabel;
        private Label nextLabel;
        private Button editButton;
        private Button completeButton;
        private Button deleteButton;
        private NotifyIcon trayIcon;
        private System.Windows.Forms.Timer reminderTimer;

        public MainForm()
        {
            Name = "MainForm";
            Text = "Eslatmachi";
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(1100, 720);
            MinimumSize = new Size(980, 650);
            BackColor = Color.FromArgb(244, 247, 250);
            Font = new Font("Segoe UI", 10F);
            AutoScaleMode = AutoScaleMode.Dpi;
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);

            store = new ReminderStore();
            reminders = new List<Reminder>();
            BuildInterface();
            BuildTray();

            Load += MainForm_Load;
            FormClosing += MainForm_FormClosing;
            FormClosed += MainForm_FormClosed;
        }

        private void BuildInterface()
        {
            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.Padding = new Padding(24);
            root.ColumnCount = 2;
            root.RowCount = 2;
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 350F));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 84F));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            Controls.Add(root);

            Panel header = new Panel();
            header.Dock = DockStyle.Fill;
            header.Margin = new Padding(0, 0, 0, 14);
            root.Controls.Add(header, 0, 0);
            root.SetColumnSpan(header, 2);

            PictureBox logo = new PictureBox();
            logo.Image = Icon.ToBitmap();
            logo.SizeMode = PictureBoxSizeMode.StretchImage;
            logo.SetBounds(0, 4, 52, 52);
            header.Controls.Add(logo);

            Label appTitle = new Label();
            appTitle.Text = "Eslatmachi";
            appTitle.Font = new Font("Segoe UI Semibold", 24F, FontStyle.Bold);
            appTitle.ForeColor = Color.FromArgb(24, 38, 55);
            appTitle.AutoSize = true;
            appTitle.Location = new Point(64, 0);
            header.Controls.Add(appTitle);

            Label subtitle = new Label();
            subtitle.Text = "Ishlaringiz o‘z vaqtida esingizda bo‘lsin";
            subtitle.ForeColor = Color.FromArgb(105, 118, 132);
            subtitle.AutoSize = true;
            subtitle.Location = new Point(68, 43);
            header.Controls.Add(subtitle);

            Label dateLabel = new Label();
            dateLabel.Dock = DockStyle.Right;
            dateLabel.Width = 270;
            dateLabel.Text = DateTime.Now.ToString("dd.MM.yyyy");
            dateLabel.TextAlign = ContentAlignment.MiddleRight;
            dateLabel.ForeColor = Color.FromArgb(105, 118, 132);
            dateLabel.Font = new Font("Segoe UI Semibold", 11F, FontStyle.Bold);
            header.Controls.Add(dateLabel);

            Panel editorCard = MakeCard();
            editorCard.Dock = DockStyle.Fill;
            editorCard.Margin = new Padding(0, 0, 16, 0);
            editorCard.AutoScroll = true;
            root.Controls.Add(editorCard, 0, 1);
            BuildEditor(editorCard);

            Panel listCard = MakeCard();
            listCard.Dock = DockStyle.Fill;
            listCard.Margin = new Padding(0);
            root.Controls.Add(listCard, 1, 1);
            BuildList(listCard);
        }

        private void BuildEditor(Panel editorCard)
        {
            editorHeading = new Label();
            editorHeading.Text = "Yangi eslatma";
            editorHeading.Font = new Font("Segoe UI Semibold", 15F, FontStyle.Bold);
            editorHeading.ForeColor = Color.FromArgb(24, 38, 55);
            editorHeading.AutoSize = true;
            editorHeading.Location = new Point(20, 20);
            editorCard.Controls.Add(editorHeading);

            editorCard.Controls.Add(MakeFieldLabel("Ish nomi", 20, 62));
            titleBox = MakeInput("TitleInput", new Rectangle(20, 86, 290, 32));
            titleBox.MaxLength = 200;
            editorCard.Controls.Add(titleBox);

            editorCard.Controls.Add(MakeFieldLabel("Sana", 20, 130));
            datePicker = new DateTimePicker();
            datePicker.Name = "DateInput";
            datePicker.Format = DateTimePickerFormat.Short;
            datePicker.SetBounds(20, 154, 164, 32);
            datePicker.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            editorCard.Controls.Add(datePicker);

            editorCard.Controls.Add(MakeFieldLabel("Vaqt", 196, 130));
            timePicker = new DateTimePicker();
            timePicker.Name = "TimeInput";
            timePicker.Format = DateTimePickerFormat.Custom;
            timePicker.CustomFormat = "HH:mm";
            timePicker.ShowUpDown = true;
            timePicker.SetBounds(196, 154, 114, 32);
            timePicker.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            editorCard.Controls.Add(timePicker);

            editorCard.Controls.Add(MakeFieldLabel("Takrorlash", 20, 198));
            repeatBox = new ComboBox();
            repeatBox.Name = "RepeatInput";
            repeatBox.DropDownStyle = ComboBoxStyle.DropDownList;
            repeatBox.Items.AddRange(new object[] { "Bir marta", "Har kuni", "Har hafta" });
            repeatBox.SelectedIndex = 0;
            repeatBox.SetBounds(20, 222, 290, 32);
            repeatBox.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            editorCard.Controls.Add(repeatBox);

            editorCard.Controls.Add(MakeFieldLabel("Izoh (ixtiyoriy)", 20, 268));
            notesBox = MakeInput("NotesInput", new Rectangle(20, 292, 290, 66));
            notesBox.Multiline = true;
            notesBox.MaxLength = 500;
            notesBox.ScrollBars = ScrollBars.Vertical;
            notesBox.AcceptsReturn = true;
            editorCard.Controls.Add(notesBox);

            addButton = MakeButton("Eslatma qo‘shish", Color.FromArgb(15, 118, 110), Color.White);
            addButton.Name = "AddReminderButton";
            addButton.Font = new Font("Segoe UI Semibold", 11F, FontStyle.Bold);
            addButton.SetBounds(20, 380, 290, 44);
            addButton.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            addButton.Click += AddOrUpdateReminder;
            editorCard.Controls.Add(addButton);

            cancelEditButton = MakeButton("Bekor qilish", Color.FromArgb(238, 241, 244), Color.FromArgb(70, 82, 95));
            cancelEditButton.Name = "CancelEditButton";
            cancelEditButton.SetBounds(20, 434, 290, 34);
            cancelEditButton.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            cancelEditButton.Visible = false;
            cancelEditButton.Click += delegate { ResetEditor(); };
            editorCard.Controls.Add(cancelEditButton);

            startupCheck = new CheckBox();
            startupCheck.Name = "StartupCheck";
            startupCheck.Text = "Windows yoqilganda avtomatik ochilsin";
            startupCheck.AutoSize = false;
            startupCheck.SetBounds(20, 484, 300, 26);
            startupCheck.ForeColor = Color.FromArgb(82, 96, 109);
            startupCheck.CheckedChanged += StartupCheck_CheckedChanged;
            editorCard.Controls.Add(startupCheck);

            Label hint = new Label();
            hint.Text = "Ilovani yopishingiz mumkin: u xabarnomalar panelida ishlab turadi.";
            hint.ForeColor = Color.FromArgb(122, 133, 145);
            hint.Font = new Font("Segoe UI", 9F);
            hint.SetBounds(20, 520, 292, 42);
            editorCard.Controls.Add(hint);
        }

        private void BuildList(Panel listCard)
        {
            TableLayoutPanel layout = new TableLayoutPanel();
            layout.Dock = DockStyle.Fill;
            layout.Padding = new Padding(20, 18, 20, 14);
            layout.ColumnCount = 1;
            layout.RowCount = 3;
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 70F));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 54F));
            listCard.Controls.Add(layout);

            Panel listHeader = new Panel();
            listHeader.Dock = DockStyle.Fill;
            layout.Controls.Add(listHeader, 0, 0);

            Label heading = new Label();
            heading.Text = "Eslatmalar";
            heading.Font = new Font("Segoe UI Semibold", 15F, FontStyle.Bold);
            heading.ForeColor = Color.FromArgb(24, 38, 55);
            heading.AutoSize = true;
            heading.Location = new Point(0, 0);
            listHeader.Controls.Add(heading);

            countLabel = new Label();
            countLabel.Text = "0 ta faol eslatma";
            countLabel.ForeColor = Color.FromArgb(105, 118, 132);
            countLabel.AutoSize = true;
            countLabel.Location = new Point(2, 35);
            listHeader.Controls.Add(countLabel);

            nextLabel = new Label();
            nextLabel.Dock = DockStyle.Right;
            nextLabel.Width = 270;
            nextLabel.TextAlign = ContentAlignment.MiddleRight;
            nextLabel.ForeColor = Color.FromArgb(15, 118, 110);
            nextLabel.Font = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold);
            listHeader.Controls.Add(nextLabel);

            grid = new DataGridView();
            grid.Name = "ReminderGrid";
            grid.Dock = DockStyle.Fill;
            grid.BackgroundColor = Color.White;
            grid.BorderStyle = BorderStyle.None;
            grid.AllowUserToAddRows = false;
            grid.AllowUserToDeleteRows = false;
            grid.AllowUserToResizeRows = false;
            grid.ReadOnly = true;
            grid.MultiSelect = false;
            grid.RowHeadersVisible = false;
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grid.AutoGenerateColumns = false;
            grid.EnableHeadersVisualStyles = false;
            grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(242, 246, 247);
            grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(82, 96, 109);
            grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold);
            grid.ColumnHeadersDefaultCellStyle.Padding = new Padding(8, 0, 8, 0);
            grid.ColumnHeadersHeight = 42;
            grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            grid.RowTemplate.Height = 48;
            grid.DefaultCellStyle.BackColor = Color.White;
            grid.DefaultCellStyle.ForeColor = Color.FromArgb(43, 56, 69);
            grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(218, 239, 235);
            grid.DefaultCellStyle.SelectionForeColor = Color.FromArgb(16, 73, 69);
            grid.DefaultCellStyle.Padding = new Padding(8, 0, 8, 0);
            grid.DefaultCellStyle.Font = new Font("Segoe UI", 10F);
            grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(250, 252, 252);
            grid.GridColor = Color.FromArgb(235, 240, 242);
            grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            grid.ClipboardCopyMode = DataGridViewClipboardCopyMode.Disable;
            layout.Controls.Add(grid, 0, 1);

            DataGridViewTextBoxColumn titleColumn = MakeColumn("TitleColumn", "Ish", DataGridViewAutoSizeColumnMode.Fill, 150);
            titleColumn.FillWeight = 100;
            grid.Columns.Add(titleColumn);
            grid.Columns.Add(MakeColumn("TimeColumn", "Sana va vaqt", DataGridViewAutoSizeColumnMode.None, 160));
            grid.Columns.Add(MakeColumn("RepeatColumn", "Takror", DataGridViewAutoSizeColumnMode.None, 90));
            grid.Columns.Add(MakeColumn("StatusColumn", "Holat", DataGridViewAutoSizeColumnMode.None, 112));

            emptyLabel = new Label();
            emptyLabel.Name = "EmptyState";
            emptyLabel.Dock = DockStyle.Fill;
            emptyLabel.TextAlign = ContentAlignment.MiddleCenter;
            emptyLabel.BackColor = Color.White;
            emptyLabel.ForeColor = Color.FromArgb(122, 133, 145);
            emptyLabel.Font = new Font("Segoe UI", 11F);
            emptyLabel.Text = "Hozircha eslatma yo‘q.\r\n\r\nBirinchi eslatmangizni qo‘shing.";
            layout.Controls.Add(emptyLabel, 0, 1);

            FlowLayoutPanel actions = new FlowLayoutPanel();
            actions.Dock = DockStyle.Fill;
            actions.WrapContents = false;
            actions.AutoScroll = true;
            actions.Padding = new Padding(0, 10, 0, 0);
            layout.Controls.Add(actions, 0, 2);

            editButton = MakeActionButton("Tahrirlash", "EditButton", new Size(102, 34), Color.FromArgb(232, 241, 240), Color.FromArgb(15, 95, 90));
            editButton.Click += delegate { EditSelectedReminder(); };
            actions.Controls.Add(editButton);

            completeButton = MakeActionButton("Bajarildi", "CompleteButton", new Size(105, 34), Color.FromArgb(15, 118, 110), Color.White);
            completeButton.Click += CompleteSelectedReminder;
            actions.Controls.Add(completeButton);

            deleteButton = MakeActionButton("O‘chirish", "DeleteButton", new Size(100, 34), Color.FromArgb(253, 235, 235), Color.FromArgb(177, 55, 55));
            deleteButton.Click += DeleteSelectedReminder;
            actions.Controls.Add(deleteButton);

            grid.SelectionChanged += delegate { UpdateActionButtons(); };
            grid.CellDoubleClick += delegate(object sender, DataGridViewCellEventArgs e)
            {
                if (e.RowIndex >= 0)
                    EditSelectedReminder();
            };
            grid.KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Delete)
                    DeleteSelectedReminder(sender, EventArgs.Empty);
            };
        }

        private void BuildTray()
        {
            ContextMenuStrip menu = new ContextMenuStrip();
            ToolStripMenuItem openItem = new ToolStripMenuItem("Eslatmachi’ni ochish");
            openItem.Font = new Font(openItem.Font, FontStyle.Bold);
            openItem.Click += delegate { OpenFromTray(); };
            menu.Items.Add(openItem);
            ToolStripMenuItem exitItem = new ToolStripMenuItem("Chiqish");
            exitItem.Click += delegate { ExitApplication(); };
            menu.Items.Add(exitItem);

            trayIcon = new NotifyIcon();
            trayIcon.Text = "Eslatmachi — ishlab turibdi";
            trayIcon.Icon = (Icon)Icon.Clone();
            trayIcon.ContextMenuStrip = menu;
            trayIcon.Visible = true;
            trayIcon.DoubleClick += delegate { OpenFromTray(); };
            trayIcon.BalloonTipClicked += delegate { OpenFromTray(); };

            reminderTimer = new System.Windows.Forms.Timer();
            reminderTimer.Interval = 1000;
            reminderTimer.Tick += CheckDueReminders;
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            try
            {
                reminders = store.Load();
            }
            catch (Exception ex)
            {
                reminders = new List<Reminder>();
                MessageBox.Show(
                    "Eslatmalar faylini o‘qib bo‘lmadi. Fayl o‘zgartirilmadi.\n\n" + ex.Message,
                    "Ma’lumotlarni o‘qishda xatolik",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }

            try
            {
                updatingStartup = true;
                startupCheck.Checked = IsStartupEnabled();
            }
            catch
            {
                startupCheck.Checked = false;
            }
            finally
            {
                updatingStartup = false;
            }

            ResetEditor();
            RefreshGrid();
            reminderTimer.Start();
            BeginInvoke(new MethodInvoker(delegate { CheckDueReminders(null, EventArgs.Empty); }));
        }

        private void AddOrUpdateReminder(object sender, EventArgs e)
        {
            string title = titleBox.Text.Trim();
            if (title.Length == 0)
            {
                MessageBox.Show(this, "Ish nomini kiriting.", "Eslatma", MessageBoxButtons.OK, MessageBoxIcon.Information);
                titleBox.Focus();
                return;
            }

            DateTime selectedTime = timePicker.Value;
            DateTime remindAt = new DateTime(
                datePicker.Value.Year,
                datePicker.Value.Month,
                datePicker.Value.Day,
                selectedTime.Hour,
                selectedTime.Minute,
                0);
            if (remindAt <= DateTime.Now)
            {
                MessageBox.Show(this, "Eslatma vaqti hozirgi vaqtdan keyin bo‘lishi kerak.", "Vaqt xato", MessageBoxButtons.OK, MessageBoxIcon.Information);
                datePicker.Focus();
                return;
            }

            Reminder reminder;
            if (editingId == null)
            {
                reminder = new Reminder();
                reminders.Add(reminder);
            }
            else
            {
                reminder = reminders.Find(delegate(Reminder item) { return item.Id == editingId; });
                if (reminder == null)
                {
                    ResetEditor();
                    return;
                }
            }

            reminder.Title = title;
            reminder.Notes = notesBox.Text.Trim();
            reminder.RemindAt = remindAt;
            reminder.Repeat = (RepeatKind)repeatBox.SelectedIndex;
            reminder.SnoozeUntil = null;
            reminder.Completed = false;
            reminder.Enabled = true;

            if (!SaveChanges())
                return;

            string savedId = reminder.Id;
            ResetEditor();
            RefreshGrid();
            SelectReminder(savedId);
            titleBox.Focus();
        }

        private void EditSelectedReminder()
        {
            Reminder reminder = SelectedReminder();
            if (reminder == null)
                return;

            editingId = reminder.Id;
            editorHeading.Text = "Eslatmani tahrirlash";
            addButton.Text = "O‘zgarishlarni saqlash";
            cancelEditButton.Visible = true;
            titleBox.Text = reminder.Title;
            notesBox.Text = reminder.Notes;
            datePicker.Value = reminder.RemindAt.Date;
            timePicker.Value = reminder.RemindAt;
            repeatBox.SelectedIndex = (int)reminder.Repeat;
            titleBox.Focus();
            titleBox.SelectAll();
        }

        private void ResetEditor()
        {
            editingId = null;
            DateTime initial = DateTime.Now.AddHours(1);
            editorHeading.Text = "Yangi eslatma";
            addButton.Text = "Eslatma qo‘shish";
            cancelEditButton.Visible = false;
            titleBox.Clear();
            notesBox.Clear();
            datePicker.Value = initial.Date;
            timePicker.Value = initial;
            repeatBox.SelectedIndex = 0;
        }

        private void DeleteSelectedReminder(object sender, EventArgs e)
        {
            Reminder reminder = SelectedReminder();
            if (reminder == null)
                return;

            DialogResult answer = MessageBox.Show(
                this,
                "“" + reminder.Title + "” eslatmasi o‘chirilsinmi?",
                "Eslatmani o‘chirish",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);
            if (answer != DialogResult.Yes)
                return;

            reminders.Remove(reminder);
            if (editingId == reminder.Id)
                ResetEditor();
            if (SaveChanges())
                RefreshGrid();
            else
                reminders.Add(reminder);
        }

        private void CompleteSelectedReminder(object sender, EventArgs e)
        {
            Reminder reminder = SelectedReminder();
            if (reminder == null || !reminder.IsActive)
                return;

            reminder.MarkCompleted(DateTime.Now);
            SaveChanges();
            RefreshGrid();
        }

        private void RefreshGrid()
        {
            string selectedId = null;
            Reminder selected = SelectedReminder();
            if (selected != null)
                selectedId = selected.Id;

            grid.Rows.Clear();
            List<Reminder> sorted = new List<Reminder>(reminders);
            sorted.Sort(CompareReminders);
            int rowToSelect = -1;
            int activeCount = 0;
            Reminder next = null;

            foreach (Reminder reminder in sorted)
            {
                int index = grid.Rows.Add(
                    reminder.Title,
                    FormatReminderTime(reminder),
                    reminder.RepeatText,
                    reminder.StatusText);
                DataGridViewRow row = grid.Rows[index];
                row.Tag = reminder.Id;
                row.Cells[0].ToolTipText = string.IsNullOrWhiteSpace(reminder.Notes) ? reminder.Title : reminder.Notes;
                if (reminder.Completed || !reminder.Enabled)
                    row.Cells[3].Style.ForeColor = Color.FromArgb(122, 133, 145);
                else
                    row.Cells[3].Style.ForeColor = Color.FromArgb(15, 118, 110);
                if (reminder.Id == selectedId)
                    rowToSelect = index;
            }

            foreach (Reminder reminder in reminders)
            {
                if (!reminder.IsActive)
                    continue;
                activeCount++;
                if (next == null || reminder.EffectiveTime < next.EffectiveTime)
                    next = reminder;
            }

            countLabel.Text = activeCount + " ta faol eslatma";
            nextLabel.Text = next == null ? "Keyingi eslatma yo‘q" : "Keyingi: " + next.EffectiveTime.ToString("dd.MM.yyyy  HH:mm");
            emptyLabel.Visible = reminders.Count == 0;
            if (emptyLabel.Visible)
                emptyLabel.BringToFront();

            grid.ClearSelection();
            if (rowToSelect >= 0)
            {
                grid.Rows[rowToSelect].Selected = true;
                grid.CurrentCell = grid.Rows[rowToSelect].Cells[0];
            }
            UpdateActionButtons();
        }

        private void SelectReminder(string id)
        {
            foreach (DataGridViewRow row in grid.Rows)
            {
                if ((string)row.Tag == id)
                {
                    row.Selected = true;
                    grid.CurrentCell = row.Cells[0];
                    break;
                }
            }
        }

        private Reminder SelectedReminder()
        {
            if (grid.SelectedRows.Count == 0)
                return null;
            string id = grid.SelectedRows[0].Tag as string;
            return reminders.Find(delegate(Reminder item) { return item.Id == id; });
        }

        private void UpdateActionButtons()
        {
            Reminder selected = SelectedReminder();
            editButton.Enabled = selected != null;
            deleteButton.Enabled = selected != null;
            completeButton.Enabled = selected != null && selected.IsActive;
        }

        private void CheckDueReminders(object sender, EventArgs e)
        {
            if (reminderTimer == null)
                return;
            reminderTimer.Stop();
            try
            {
                for (int index = 0; index < reminders.Count; index++)
                {
                    Reminder reminder = reminders[index];
                    DateTime now = DateTime.Now;
                    if (!reminder.IsActive || reminder.EffectiveTime > now)
                        continue;

                    bool wasSnoozed = reminder.SnoozeUntil.HasValue;
                    DateTime reminderTime = reminder.EffectiveTime;
                    reminder.PrepareForAlert(now, wasSnoozed);
                    SaveChanges();

                    string balloonText = string.IsNullOrWhiteSpace(reminder.Notes)
                        ? reminderTime.ToString("dd.MM.yyyy  HH:mm")
                        : reminder.Notes;
                    if (balloonText.Length > 180)
                        balloonText = balloonText.Substring(0, 177) + "...";
                    trayIcon.ShowBalloonTip(10000, "Eslatma: " + reminder.Title, balloonText, ToolTipIcon.Info);

                    bool snoozeRequested;
                    using (ReminderForm dialog = new ReminderForm(reminder.Title, reminder.Notes, reminderTime))
                    {
                        dialog.ShowDialog();
                        snoozeRequested = dialog.SnoozeRequested;
                    }

                    if (snoozeRequested)
                    {
                        reminder.Snooze(DateTime.Now.AddMinutes(10));
                        SaveChanges();
                    }
                }
            }
            finally
            {
                RefreshGrid();
                reminderTimer.Start();
            }
        }

        private bool SaveChanges()
        {
            try
            {
                store.Save(reminders);
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    this,
                    "Eslatmalarni saqlab bo‘lmadi:\n" + ex.Message,
                    "Saqlashda xatolik",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return false;
            }
        }

        private bool IsStartupEnabled()
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Run", false))
            {
                string value = key == null ? null : key.GetValue("Eslatmachi") as string;
                return string.Equals(value, "\"" + Application.ExecutablePath + "\"", StringComparison.OrdinalIgnoreCase);
            }
        }

        private void StartupCheck_CheckedChanged(object sender, EventArgs e)
        {
            if (updatingStartup)
                return;
            try
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Run"))
                {
                    if (startupCheck.Checked)
                        key.SetValue("Eslatmachi", "\"" + Application.ExecutablePath + "\"");
                    else
                        key.DeleteValue("Eslatmachi", false);
                }
            }
            catch (Exception ex)
            {
                updatingStartup = true;
                startupCheck.Checked = !startupCheck.Checked;
                updatingStartup = false;
                MessageBox.Show(this, "Avtomatik ishga tushirish sozlanmadi:\n" + ex.Message, "Sozlama", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (!allowExit && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                Hide();
                if (!trayMessageShown)
                {
                    trayIcon.ShowBalloonTip(5000, "Eslatmachi ishlab turibdi", "Ochish uchun xabarnomalar panelidagi ikonkani ikki marta bosing. Butunlay chiqish uchun ikonkaga o‘ng tugma bosing.", ToolTipIcon.Info);
                    trayMessageShown = true;
                }
            }
        }

        private void MainForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            if (reminderTimer != null)
            {
                reminderTimer.Stop();
                reminderTimer.Dispose();
            }
            if (trayIcon != null)
            {
                trayIcon.Visible = false;
                trayIcon.Icon.Dispose();
                trayIcon.Dispose();
            }
        }

        private void OpenFromTray()
        {
            Show();
            WindowState = FormWindowState.Normal;
            Activate();
        }

        private void ExitApplication()
        {
            allowExit = true;
            reminderTimer.Stop();
            trayIcon.Visible = false;
            Application.Exit();
        }

        private static int CompareReminders(Reminder left, Reminder right)
        {
            if (left.IsActive != right.IsActive)
                return left.IsActive ? -1 : 1;
            return left.EffectiveTime.CompareTo(right.EffectiveTime);
        }

        private static string FormatReminderTime(Reminder reminder)
        {
            if (!reminder.IsActive)
                return reminder.RemindAt.ToString("dd.MM.yyyy  HH:mm");
            DateTime time = reminder.EffectiveTime;
            if (time.Date == DateTime.Today)
                return "Bugun  " + time.ToString("HH:mm");
            if (time.Date == DateTime.Today.AddDays(1))
                return "Ertaga  " + time.ToString("HH:mm");
            return time.ToString("dd.MM.yyyy  HH:mm");
        }

        private static Panel MakeCard()
        {
            Panel panel = new Panel();
            panel.BackColor = Color.White;
            panel.Paint += delegate(object sender, PaintEventArgs e)
            {
                Control control = (Control)sender;
                using (Pen pen = new Pen(Color.FromArgb(229, 235, 238)))
                    e.Graphics.DrawRectangle(pen, 0, 0, control.Width - 1, control.Height - 1);
            };
            return panel;
        }

        private static Label MakeFieldLabel(string text, int x, int y)
        {
            Label label = new Label();
            label.Text = text;
            label.ForeColor = Color.FromArgb(82, 96, 109);
            label.Font = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold);
            label.AutoSize = true;
            label.Location = new Point(x, y);
            return label;
        }

        private static TextBox MakeInput(string name, Rectangle bounds)
        {
            TextBox box = new TextBox();
            box.Name = name;
            box.BorderStyle = BorderStyle.FixedSingle;
            box.BackColor = Color.FromArgb(251, 252, 253);
            box.ForeColor = Color.FromArgb(24, 38, 55);
            box.SetBounds(bounds.X, bounds.Y, bounds.Width, bounds.Height);
            box.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            return box;
        }

        private static Button MakeButton(string text, Color background, Color foreground)
        {
            Button button = new Button();
            button.Text = text;
            button.BackColor = background;
            button.ForeColor = foreground;
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.Cursor = Cursors.Hand;
            return button;
        }

        private static Button MakeActionButton(string text, string name, Size size, Color background, Color foreground)
        {
            Button button = MakeButton(text, background, foreground);
            button.Name = name;
            button.Size = size;
            button.Margin = new Padding(0, 0, 8, 0);
            return button;
        }

        private static DataGridViewTextBoxColumn MakeColumn(string name, string header, DataGridViewAutoSizeColumnMode mode, int width)
        {
            DataGridViewTextBoxColumn column = new DataGridViewTextBoxColumn();
            column.Name = name;
            column.HeaderText = header;
            column.AutoSizeMode = mode;
            column.Width = width;
            column.MinimumWidth = 70;
            column.SortMode = DataGridViewColumnSortMode.NotSortable;
            return column;
        }
    }
}
