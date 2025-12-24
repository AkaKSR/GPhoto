using System;
using System.IO;
using System.Drawing;
using System.Text;
using System.Text.Json;
using System.Windows.Forms;
using System.Security.Cryptography;

namespace GPhoto
{
    public class UploadSettings
    {
        public string Host { get; set; } = string.Empty;
        public int Port { get; set; } = 21;
        public string ID { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public class UploadSettingsForm : Form
    {
        private TextBox txtHost = null!;
        private TextBox txtPort = null!;
        private TextBox txtID = null!;
        private TextBox txtPassword = null!;
        private Button btnSave = null!;
        private Button btnClose = null!;

        private const string SettingsFile = "config.dat";
        private static readonly byte[] _entropy = Encoding.UTF8.GetBytes("GPhoto.Config.Entropy.v1");

        public UploadSettingsForm()
        {
            InitializeComponent();
            LoadSettings();
        }

        private void InitializeComponent()
        {
            this.Text = "설정";
            this.ClientSize = new Size(600, 320);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            var mainPanel = new TableLayoutPanel();
            mainPanel.Dock = DockStyle.Fill;
            mainPanel.Padding = new Padding(12);
            mainPanel.ColumnCount = 4;
            mainPanel.RowCount = 4;
            mainPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100F)); // label
            mainPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            mainPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100F)); // label
            mainPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            for (int i = 0; i < 4; i++) mainPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));

            // Labels and textboxes — create textboxes first so we can match label vertical alignment
            txtHost = new TextBox() { Anchor = AnchorStyles.Left | AnchorStyles.Right };
            var lblHost = new Label() { Text = "Host", AutoSize = false, TextAlign = ContentAlignment.MiddleLeft, Dock = DockStyle.Fill };
            mainPanel.Controls.Add(lblHost, 0, 0);
            mainPanel.Controls.Add(txtHost, 1, 0);

            txtPort = new TextBox() { Anchor = AnchorStyles.Left | AnchorStyles.Right };
            txtPort.Text = "21";
            var lblPort = new Label() { Text = "Port", AutoSize = false, TextAlign = ContentAlignment.MiddleLeft, Dock = DockStyle.Fill };
            mainPanel.Controls.Add(lblPort, 2, 0);
            mainPanel.Controls.Add(txtPort, 3, 0);

            txtID = new TextBox() { Anchor = AnchorStyles.Left | AnchorStyles.Right };
            var lblID = new Label() { Text = "ID", AutoSize = false, TextAlign = ContentAlignment.MiddleLeft, Dock = DockStyle.Fill };
            mainPanel.Controls.Add(lblID, 0, 1);
            mainPanel.Controls.Add(txtID, 1, 1);

            txtPassword = new TextBox() { Anchor = AnchorStyles.Left | AnchorStyles.Right, UseSystemPasswordChar = true };
            var lblPassword = new Label() { Text = "Password", AutoSize = false, TextAlign = ContentAlignment.MiddleLeft, Dock = DockStyle.Fill };
            mainPanel.Controls.Add(lblPassword, 2, 1);
            mainPanel.Controls.Add(txtPassword, 3, 1);

            // Bottom button panel
            var btnPanel = new FlowLayoutPanel();
            btnPanel.FlowDirection = FlowDirection.RightToLeft;
            btnPanel.Dock = DockStyle.Bottom;
            btnPanel.Padding = new Padding(10);
            btnPanel.Height = 64;

            btnSave = new Button() { Text = "저장", Size = new Size(100, 40) };
            btnClose = new Button() { Text = "닫기", Size = new Size(100, 40) };
            btnSave.Click += BtnSave_Click;
            btnClose.Click += (s, e) => this.Close();
            btnPanel.Controls.Add(btnClose);
            btnPanel.Controls.Add(btnSave);

            this.Controls.Add(mainPanel);
            this.Controls.Add(btnPanel);
        }

        private void BtnSave_Click(object? sender, EventArgs e)
        {
            if (!int.TryParse(txtPort.Text, out var port)) port = 21;
            var settings = new UploadSettings()
            {
                Host = txtHost.Text ?? string.Empty,
                Port = port,
                ID = txtID.Text ?? string.Empty,
                Password = txtPassword.Text ?? string.Empty
            };

            try
            {
                var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions() { WriteIndented = true });
                var plainBytes = Encoding.UTF8.GetBytes(json);
                var protectedBytes = ProtectedData.Protect(plainBytes, _entropy, DataProtectionScope.CurrentUser);
                File.WriteAllBytes(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, SettingsFile), protectedBytes);
                MessageBox.Show(this, "설정이 암호화되어 저장되었습니다.", "정보", MessageBoxButtons.OK, MessageBoxIcon.Information);
                this.DialogResult = DialogResult.OK;
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"설정 저장 중 오류가 발생했습니다: {ex.Message}", "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void LoadSettings()
        {
            try
            {
                var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                var encPath = Path.Combine(baseDir, SettingsFile);

                if (!File.Exists(encPath)) return;

                try
                {
                    var protectedBytes = File.ReadAllBytes(encPath);
                    var plainBytes = ProtectedData.Unprotect(protectedBytes, _entropy, DataProtectionScope.CurrentUser);
                    var json = Encoding.UTF8.GetString(plainBytes);
                    var settings = JsonSerializer.Deserialize<UploadSettings>(json);
                    if (settings == null) return;
                    txtHost.Text = settings.Host ?? string.Empty;
                    txtPort.Text = settings.Port.ToString();
                    txtID.Text = settings.ID ?? string.Empty;
                    txtPassword.Text = settings.Password ?? string.Empty;
                }
                catch (CryptographicException)
                {
                    // ignore decryption failures
                }
            }
            catch { }
        }
    }
}
