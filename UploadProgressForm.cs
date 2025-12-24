using System;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

namespace GPhoto
{
    public class UploadProgressForm : Form
    {
        private ProgressBar progressBar = null!;
        private ListBox lstLog = null!;
        private Button btnCancel = null!;
        private Label lblCurrent = null!;
        private CancellationTokenSource cts = null!;

        public UploadProgressForm(CancellationTokenSource cts)
        {
            this.cts = cts ?? throw new ArgumentNullException(nameof(cts));
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.Text = "업로드 진행";
            this.ClientSize = new Size(600, 400);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            lblCurrent = new Label() { Dock = DockStyle.Top, Height = 24, Text = "준비 중...", TextAlign = ContentAlignment.MiddleLeft };
            progressBar = new ProgressBar() { Dock = DockStyle.Top, Height = 20, Minimum = 0, Maximum = 100, Value = 0 };
            lstLog = new ListBox() { Dock = DockStyle.Fill }; 
            btnCancel = new Button() { Text = "취소", Width = 100, Height = 36, Anchor = AnchorStyles.Right | AnchorStyles.Bottom };
            btnCancel.Click += (s, e) => { btnCancel.Enabled = false; try { cts.Cancel(); AppendLine("취소 요청됨..."); } catch { } };

            var bottomPanel = new Panel() { Dock = DockStyle.Bottom, Height = 56 };
            btnCancel.Location = new Point(bottomPanel.ClientSize.Width - btnCancel.Width - 12, 10);
            btnCancel.Anchor = AnchorStyles.Right | AnchorStyles.Bottom;
            bottomPanel.Controls.Add(btnCancel);

            this.Controls.Add(lstLog);
            this.Controls.Add(progressBar);
            this.Controls.Add(lblCurrent);
            this.Controls.Add(bottomPanel);
        }

        public void AppendLine(string text)
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action(() => AppendLine(text)));
                return;
            }
            lstLog.Items.Add(text);
            lstLog.TopIndex = Math.Max(0, lstLog.Items.Count - 1);
        }

        public void SetProgress(int percent)
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action(() => SetProgress(percent)));
                return;
            }
            progressBar.Value = Math.Max(progressBar.Minimum, Math.Min(progressBar.Maximum, percent));
        }

        public void SetCurrentFile(string text)
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action(() => SetCurrentFile(text)));
                return;
            }
            lblCurrent.Text = text;
        }
    }
}
