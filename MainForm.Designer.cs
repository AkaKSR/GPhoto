using System.Drawing;
using System.Windows.Forms;

namespace GPhoto
{
    partial class MainForm
    {
        private TableLayoutPanel tableLayoutPanelMain;
        private DataGridView dataGridViewFiles;
        private TableLayoutPanel rightPanel;
        private PictureBox pictureBoxPreview;
        private RichTextBox richTextDescription;
        private FlowLayoutPanel flowButtonsLeft;
        private FlowLayoutPanel flowButtonsRight;
        private Button btnAdd;
        private Button btnDelete;
        private Button btnInjectPayload;
        private Button btnExtractPayload;
        private Button btnBatch;
        private ContextMenuStrip contextMenuBatch;
        private ToolStripMenuItem menuBatchInject;
        private ToolStripMenuItem menuBatchExtract;
        private Button btnLoad;
        private Button btnSave;
        private Button btnUpload;
        private Button btnSettings;
        private Button btnExit;

        private void InitializeComponent()
        {
            this.tableLayoutPanelMain = new TableLayoutPanel();
            this.dataGridViewFiles = new DataGridView();
            this.rightPanel = new TableLayoutPanel();
            this.pictureBoxPreview = new PictureBox();
            this.richTextDescription = new RichTextBox();
            this.flowButtonsLeft = new FlowLayoutPanel();
            this.flowButtonsRight = new FlowLayoutPanel();
            this.btnAdd = new Button();
            this.btnDelete = new Button();
            this.btnInjectPayload = new Button();
            this.btnExtractPayload = new Button();
            this.btnBatch = new Button();
            this.contextMenuBatch = new ContextMenuStrip();
            this.menuBatchInject = new ToolStripMenuItem();
            this.menuBatchExtract = new ToolStripMenuItem();
            this.btnLoad = new Button();
            this.btnSave = new Button();
            this.btnUpload = new Button();
            this.btnExit = new Button();

            // Main Form
            this.SuspendLayout();
            this.Text = "GPhoto";
            this.ClientSize = new Size(1800, 800);
            // Start the form centered on the user's screen
            this.StartPosition = FormStartPosition.CenterScreen;

            // tableLayoutPanelMain
            this.tableLayoutPanelMain.ColumnCount = 2;
            this.tableLayoutPanelMain.RowCount = 2;
            this.tableLayoutPanelMain.Dock = DockStyle.Fill;
            this.tableLayoutPanelMain.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 70F));
            this.tableLayoutPanelMain.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30F));
            // Top row fills available space; bottom row is a fixed height for buttons
            this.tableLayoutPanelMain.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            this.tableLayoutPanelMain.RowStyles.Add(new RowStyle(SizeType.Absolute, 70F));

            // dataGridViewFiles
            this.dataGridViewFiles.Dock = DockStyle.Fill;
            this.dataGridViewFiles.AllowUserToAddRows = false;
            this.dataGridViewFiles.AllowUserToDeleteRows = false;
            this.dataGridViewFiles.RowHeadersVisible = false;
            this.dataGridViewFiles.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            this.dataGridViewFiles.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            // Allow users to resize column widths but prevent resizing of row heights
            this.dataGridViewFiles.AllowUserToResizeColumns = true;
            this.dataGridViewFiles.AllowUserToResizeRows = false;
            // Prevent header height/row header width resizing (keeps heights fixed)
            this.dataGridViewFiles.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            this.dataGridViewFiles.RowHeadersWidthSizeMode = DataGridViewRowHeadersWidthSizeMode.DisableResizing;

            // Add basic columns similar to the design
            // Selection checkbox column (left-most)
            this.dataGridViewFiles.Columns.Add(new DataGridViewCheckBoxColumn() { HeaderText = "", Name = "colSelect", FillWeight = 3, Width = 30, Resizable = DataGridViewTriState.False });
            this.dataGridViewFiles.Columns.Add(new DataGridViewTextBoxColumn() { HeaderText = "No.", Name = "colNo", FillWeight = 3, Resizable = DataGridViewTriState.True });
            this.dataGridViewFiles.Columns.Add(new DataGridViewTextBoxColumn() { HeaderText = "이미지 파일명", Name = "colFileName", FillWeight = 23, Resizable = DataGridViewTriState.True });
            // (file-select button column removed; per-cell embedded button is used instead)
            // Use checkbox columns for payload/upload flags so they display as checkboxes
            this.dataGridViewFiles.Columns.Add(new DataGridViewCheckBoxColumn() { HeaderText = "페이로드 유무", Name = "colPayload", FillWeight = 12, MinimumWidth = 160, Resizable = DataGridViewTriState.True });
            this.dataGridViewFiles.Columns.Add(new DataGridViewTextBoxColumn() { HeaderText = "페이로드 파일명", Name = "colPayloadName", FillWeight = 24, Resizable = DataGridViewTriState.True });
            this.dataGridViewFiles.Columns.Add(new DataGridViewTextBoxColumn() { HeaderText = "생성된 페이로드 파일명", Name = "colGeneratedPayloadName", FillWeight = 24, Resizable = DataGridViewTriState.True });
            this.dataGridViewFiles.Columns.Add(new DataGridViewCheckBoxColumn() { HeaderText = "업로드 유무", Name = "colUploaded", FillWeight = 12, MinimumWidth = 160, Resizable = DataGridViewTriState.True });
            this.dataGridViewFiles.Columns.Add(new DataGridViewTextBoxColumn() { HeaderText = "설명", Name = "colDescription", FillWeight = 12, Resizable = DataGridViewTriState.True, Visible = false });

            // rightPanel
            this.rightPanel.ColumnCount = 1;
            this.rightPanel.RowCount = 2;
            this.rightPanel.Dock = DockStyle.Fill;
            this.rightPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 55F));
            this.rightPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 45F));

            // pictureBoxPreview
            this.pictureBoxPreview.Dock = DockStyle.Fill;
            this.pictureBoxPreview.BorderStyle = BorderStyle.FixedSingle;
            this.pictureBoxPreview.SizeMode = PictureBoxSizeMode.Zoom;
            // Set initial default image from embedded resources
            try
            {
                var def = Properties.Resources.Default;
                if (def != null)
                {
                    this.pictureBoxPreview.Image = new Bitmap(def);
                    def.Dispose();
                }
            }
            catch { }

            // richTextDescription
            this.richTextDescription.Dock = DockStyle.Fill;
            this.richTextDescription.ReadOnly = false;

            // flowButtonsLeft
            this.flowButtonsLeft.FlowDirection = FlowDirection.LeftToRight;
            this.flowButtonsLeft.Dock = DockStyle.Fill;
            this.flowButtonsLeft.Padding = new Padding(10);
            this.flowButtonsLeft.WrapContents = false;
            this.flowButtonsLeft.AutoScroll = true;

            // flowButtonsRight
            this.flowButtonsRight.FlowDirection = FlowDirection.RightToLeft;
            this.flowButtonsRight.Dock = DockStyle.Fill;
            this.flowButtonsRight.Padding = new Padding(10);
            this.flowButtonsRight.WrapContents = false;

            // Buttons
            this.btnLoad.Text = "불러오기";
            this.btnLoad.Size = new Size(100, 40);
            this.btnSave.Text = "저장";
            this.btnSave.Size = new Size(100, 40);
            // Batch button (dropdown)
            this.btnBatch.Text = "배치 ▾";
            this.btnBatch.Size = new Size(100, 40);
            this.btnUpload.Text = "업로드";
            this.btnUpload.Size = new Size(100, 40);
            // this.btnUpload.Visible = false;
            this.btnSettings = new Button();
            this.btnSettings.Text = "설정";
            this.btnSettings.Size = new Size(100, 40);
            this.btnExit.Text = "종료";
            this.btnExit.Size = new Size(100, 40);
            this.btnExit.Anchor = AnchorStyles.Right;
            this.btnExit.Click += (s, e) => this.Close();

            // Assemble left-bottom and right-bottom button panels inside a container
            var bottomPanel = new TableLayoutPanel();
            bottomPanel.ColumnCount = 2;
            bottomPanel.RowCount = 1;
            bottomPanel.Dock = DockStyle.Fill;
            bottomPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 70F));
            bottomPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30F));

            // Add/Delete buttons to the left of the Load button
            this.btnAdd.Text = "추가";
            this.btnAdd.Size = new Size(100, 40);
            this.btnDelete.Text = "삭제";
            this.btnDelete.Size = new Size(100, 40);
            this.btnInjectPayload.Text = "페이로드 주입";
            this.btnInjectPayload.Size = new Size(120, 40);
            this.btnExtractPayload.Text = "페이로드 추출";
            this.btnExtractPayload.Size = new Size(120, 40);

            this.flowButtonsLeft.Controls.Add(this.btnAdd);
            this.flowButtonsLeft.Controls.Add(this.btnDelete);
            this.flowButtonsLeft.Controls.Add(this.btnInjectPayload);
            this.flowButtonsLeft.Controls.Add(this.btnExtractPayload);
            this.flowButtonsLeft.Controls.Add(this.btnLoad);
            this.flowButtonsLeft.Controls.Add(this.btnSave);
            // add batch button to the right of Save
            this.flowButtonsLeft.Controls.Add(this.btnBatch);
            this.flowButtonsLeft.Controls.Add(this.btnUpload);
            // Settings button should appear to the right of Upload
            this.flowButtonsLeft.Controls.Add(this.btnSettings);

            this.flowButtonsRight.Controls.Add(this.btnExit);

            // Configure batch context menu
            this.menuBatchInject.Text = "주입";
            this.menuBatchExtract.Text = "추출";
            this.contextMenuBatch.Items.AddRange(new ToolStripItem[] { this.menuBatchInject, this.menuBatchExtract });

            bottomPanel.Controls.Add(this.flowButtonsLeft, 0, 0);
            bottomPanel.Controls.Add(this.flowButtonsRight, 1, 0);

            // Add controls to rightPanel
            this.rightPanel.Controls.Add(this.pictureBoxPreview, 0, 0);
            this.rightPanel.Controls.Add(this.richTextDescription, 0, 1);

            // Add main controls to tableLayoutPanelMain
            this.tableLayoutPanelMain.Controls.Add(this.dataGridViewFiles, 0, 0);
            this.tableLayoutPanelMain.SetRowSpan(this.dataGridViewFiles, 1);
            this.tableLayoutPanelMain.Controls.Add(this.rightPanel, 1, 0);
            this.tableLayoutPanelMain.Controls.Add(bottomPanel, 0, 1);
            // Make bottom panel span both columns so it lines up under both
            this.tableLayoutPanelMain.SetColumnSpan(bottomPanel, 2);

            // Add the main table to the form
            this.Controls.Add(this.tableLayoutPanelMain);

            this.ResumeLayout(false);
        }
    }
}
