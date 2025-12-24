using System;
using System.IO;
using System.Linq;
using System.Drawing;
using System.Windows.Forms;
using System.IO.Compression;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Security.Cryptography;
using System.Threading;
using System.Xml.Linq;


namespace GPhoto
{
    public partial class MainForm : Form
    {
        // Path of the last loaded XML file. If null, Save will create a new file via SaveFileDialog.
        private string? currentXmlPath;
        // Index of currently selected row for description binding (-1 if none)
        private int currentSelectedRowIndex = -1;
        // Suppress recursive updates between grid and richText
        private bool suppressDescriptionSync = false;
        // Header checkbox control for select-all functionality
        private CheckBox? headerCheckBox = null;
        // Guard to prevent recursive updates between header checkbox and row checkboxes
        private bool headerCheckBoxUpdating = false;
        public MainForm()
        {
            InitializeComponent();
            // Hook up button handlers
            this.btnLoad!.Click += BtnLoad_Click;
            this.btnSave!.Click += BtnSave_Click;
            // Batch dropdown/menu handlers
            this.btnBatch!.Click += (s, e) => {
                try { this.contextMenuBatch!.Show(this.btnBatch, new System.Drawing.Point(0, this.btnBatch.Height)); } catch { }
            };
            this.menuBatchInject!.Click += (s, e) => BatchInject_Click(s, e);
            this.menuBatchExtract!.Click += (s, e) => BatchExtract_Click(s, e);
            this.btnSettings!.Click += BtnSettings_Click;
            this.btnAdd!.Click += BtnAdd_Click;
            this.btnDelete!.Click += BtnDelete_Click;
            // Payload inject button
            this.btnInjectPayload!.Click += BtnInjectPayload_Click;
            // Payload extract button
            this.btnExtractPayload!.Click += BtnExtractPayload_Click;

            // Upload button
            this.btnUpload!.Click += BtnUpload_Click;

            // DataGridView <-> RichTextBox two-way binding for Description
            this.dataGridViewFiles!.SelectionChanged += DataGridViewFiles_SelectionChanged;
            this.dataGridViewFiles.CellValueChanged += DataGridViewFiles_CellValueChanged;
            this.dataGridViewFiles.CurrentCellDirtyStateChanged += DataGridViewFiles_CurrentCellDirtyStateChanged;
            this.dataGridViewFiles.CellValidating += DataGridViewFiles_CellValidating;
            this.dataGridViewFiles.CellEndEdit += DataGridViewFiles_CellEndEdit;
            this.dataGridViewFiles.CellPainting += DataGridViewFiles_CellPainting;
            this.dataGridViewFiles.CellMouseClick += DataGridViewFiles_CellMouseClick;
            this.richTextDescription!.TextChanged += RichTextDescription_TextChanged;
            // Setup header checkbox for select-all
            SetupHeaderCheckBox();
        }

        private void SetupHeaderCheckBox()
        {
            try
            {
                if (!this.dataGridViewFiles.Columns.Contains("colSelect")) return;
                // create checkbox and add to DataGridView's controls so it scrolls with the grid
                this.headerCheckBox = new CheckBox();
                // Use explicit sizing (bigger to avoid clipping on high-DPI), and keep no extra margin
                this.headerCheckBox.AutoSize = false;
                this.headerCheckBox.Size = new Size(18, 18);
                this.headerCheckBox.Margin = new Padding(0);
                this.headerCheckBox.Padding = new Padding(0);
                this.headerCheckBox.BackColor = Color.Transparent;
                this.headerCheckBox.FlatStyle = FlatStyle.System;
                this.headerCheckBox.CheckedChanged += HeaderCheckBox_CheckedChanged;
                this.headerCheckBox.Visible = true;
                this.dataGridViewFiles.Controls.Add(this.headerCheckBox);
                PositionHeaderCheckBox();

                // reposition when columns move/scroll/resize
                this.dataGridViewFiles.ColumnWidthChanged += (s, e) => PositionHeaderCheckBox();
                this.dataGridViewFiles.Scroll += (s, e) => PositionHeaderCheckBox();
                this.dataGridViewFiles.Resize += (s, e) => PositionHeaderCheckBox();
                this.dataGridViewFiles.ColumnDisplayIndexChanged += (s, e) => PositionHeaderCheckBox();
            }
            catch { }
        }

        private void PositionHeaderCheckBox()
        {
            if (this.headerCheckBox == null) return;
            try
            {
                var col = this.dataGridViewFiles.Columns["colSelect"];
                if (col == null) return;
                var rect = this.dataGridViewFiles.GetCellDisplayRectangle(col.Index, -1, true);
                // Add small padding so the checkbox doesn't overlap header borders (prevents clipping)
                int padX = 1;
                int padY = 2;
                int x = rect.Left + Math.Max(0, (rect.Width - this.headerCheckBox.Width) / 2) + padX;
                int y = rect.Top + Math.Max(0, (rect.Height - this.headerCheckBox.Height) / 2) + padY;
                // Ensure checkbox stays within the visible client rectangle
                var client = this.dataGridViewFiles.ClientRectangle;
                x = Math.Min(x, client.Right - this.headerCheckBox.Width - 1);
                y = Math.Min(y, client.Bottom - this.headerCheckBox.Height - 1);
                if (x < 0) x = rect.Left;
                if (y < 0) y = rect.Top;
                this.headerCheckBox.Location = new Point(x, y);
                this.headerCheckBox.BringToFront();
            }
            catch { }
        }

        private void HeaderCheckBox_CheckedChanged(object? sender, EventArgs e)
        {
            if (this.headerCheckBoxUpdating) return;
            if (this.headerCheckBox == null) return;
            try
            {
                this.headerCheckBoxUpdating = true;
                bool check = this.headerCheckBox.Checked;
                int selIndex = -1;
                if (this.dataGridViewFiles.Columns.Contains("colSelect")) { var c = this.dataGridViewFiles.Columns["colSelect"]; if (c != null) selIndex = c.Index; }
                if (selIndex < 0) return;
                // If a checkbox cell is currently being edited, end edit so programmatic update takes effect
                try { this.dataGridViewFiles.EndEdit(); } catch { }

                foreach (DataGridViewRow row in this.dataGridViewFiles.Rows)
                {
                    if (row.IsNewRow) continue;
                    row.Cells[selIndex].Value = check;
                }

                // Commit and refresh so the edited/selected cell UI updates immediately
                try { this.dataGridViewFiles.CommitEdit(DataGridViewDataErrorContexts.Commit); } catch { }
                try { this.dataGridViewFiles.Refresh(); } catch { }
            }
            finally { this.headerCheckBoxUpdating = false; }
        }

        private void BtnSettings_Click(object? sender, EventArgs e)
        {
            try
            {
                using var dlg = new UploadSettingsForm();
                dlg.ShowDialog(this);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"설정 창을 여는 중 오류가 발생했습니다: {ex.Message}", "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private UploadSettings? ReadUploadSettings()
        {
            try
            {
                var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                var encPath = Path.Combine(baseDir, "config.dat");
                if (!File.Exists(encPath)) return null;
                var protectedBytes = File.ReadAllBytes(encPath);
                var entropy = Encoding.UTF8.GetBytes("GPhoto.Config.Entropy.v1");
                var plainBytes = ProtectedData.Unprotect(protectedBytes, entropy, DataProtectionScope.CurrentUser);
                var json = Encoding.UTF8.GetString(plainBytes);
                var settings = JsonSerializer.Deserialize<UploadSettings>(json);
                return settings;
            }
            catch { return null; }
        }

        private async void BtnUpload_Click(object? sender, EventArgs e)
        {
            try
            {
                this.btnUpload.Enabled = false;

                var settings = ReadUploadSettings();
                if (settings == null || string.IsNullOrWhiteSpace(settings.Host))
                {
                    MessageBox.Show(this, "업로드 설정이 없습니다. '설정'에서 FTP 정보를 입력해 주세요.", "설정 필요", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                int selIndex = -1;
                int genIndex = -1;
                if (this.dataGridViewFiles.Columns.Contains("colSelect")) { var c = this.dataGridViewFiles.Columns["colSelect"]; if (c != null) selIndex = c.Index; }
                if (this.dataGridViewFiles.Columns.Contains("colGeneratedPayloadName")) { var c2 = this.dataGridViewFiles.Columns["colGeneratedPayloadName"]; if (c2 != null) genIndex = c2.Index; }
                int uploadedIndex = -1;
                if (this.dataGridViewFiles.Columns.Contains("colUploaded")) { var c3 = this.dataGridViewFiles.Columns["colUploaded"]; if (c3 != null) uploadedIndex = c3.Index; }

                if (selIndex < 0 || genIndex < 0)
                {
                    MessageBox.Show(this, "필요한 컬럼을 찾을 수 없습니다.", "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                var succeeded = new System.Collections.Generic.List<string>();
                var failed = new System.Collections.Generic.List<string>();

                var rows = this.dataGridViewFiles.Rows.Cast<DataGridViewRow>().Where(r => !r.IsNewRow).ToList();
                int total = rows.Count(r => {
                    try { var v = r.Cells[selIndex].Value; if (v is bool b) return b; if (v != null) { if (bool.TryParse(v.ToString(), out var bb)) return bb; } } catch { }
                    return false;
                });

                if (total == 0)
                {
                    MessageBox.Show(this, "업로드할 항목을 선택하세요. 왼쪽의 체크박스를 사용해 선택할 수 있습니다.", "안내", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                using var cts = new CancellationTokenSource();
                using var progressForm = new UploadProgressForm(cts);
                progressForm.Show(this);
                int done = 0;

                foreach (DataGridViewRow row in rows)
                {
                    if (cts.Token.IsCancellationRequested) { progressForm.AppendLine("업로드 취소됨"); break; }

                    bool isSelected = false;
                    try
                    {
                        var v = row.Cells[selIndex].Value;
                        if (v is bool bb) isSelected = bb;
                        else if (v != null) bool.TryParse(v.ToString(), out isSelected);
                    }
                    catch { }

                    if (!isSelected) continue;

                    var localPath = Convert.ToString(row.Cells[genIndex].Value) ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(localPath)) { failed.Add($"행 {row.Index + 1}: 업로드할 파일 경로가 비어있음"); continue; }
                    if (!File.Exists(localPath)) { failed.Add($"행 {row.Index + 1}: 파일을 찾을 수 없음: {localPath}"); continue; }

                    progressForm.SetCurrentFile($"업로드: {localPath}");
                    progressForm.AppendLine($"업로드 시작: {localPath}");

                    try
                    {
                        var fileName = Path.GetFileName(localPath);
                        var uriString = $"ftp://{settings.Host}:{settings.Port}/{Uri.EscapeDataString(fileName)}";

                        // Check if file exists on server
                        bool existsOnServer = false;
                        try
                        {
#pragma warning disable SYSLIB0014
                            var reqCheck = (FtpWebRequest)WebRequest.Create(uriString);
#pragma warning restore SYSLIB0014
                            reqCheck.Method = WebRequestMethods.Ftp.GetFileSize;
                            reqCheck.Credentials = new NetworkCredential(settings.ID, settings.Password);
                            reqCheck.EnableSsl = false;
                            using var respCheck = (FtpWebResponse)await reqCheck.GetResponseAsync();
                            // If we get a response, file exists
                            existsOnServer = true;
                        }
                        catch (WebException)
                        {
                            // If server indicates file not found, treat as not existing.
                            // Other errors will be treated as not existing (we'll try upload and report failure if needed).
                            existsOnServer = false;
                        }

                        if (existsOnServer)
                        {
                            // Ask user whether to overwrite this file
                            var answer = MessageBox.Show(this, $"원격에 '{fileName}' 파일이 이미 존재합니다. 덮어쓰시겠습니까?\n(예: 덮어쓰기, 아니오: 건너뜀, 취소: 전체 중단)", "파일 존재", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                            if (answer == DialogResult.Cancel)
                            {
                                // Cancel entire upload
                                try { cts.Cancel(); progressForm.AppendLine("업로드 취소됨"); } catch { }
                                break;
                            }
                            if (answer == DialogResult.No)
                            {
                                progressForm.AppendLine($"스킵됨(원격에 존재): {localPath}");
                                failed.Add($"행 {row.Index + 1}: 업로드 건너뜀(원격에 존재): {localPath}");
                                continue; // skip this file
                            }
                            // else yes -> continue to overwrite
                        }

                        // Proceed with upload (will create or overwrite)
#pragma warning disable SYSLIB0014
                        var request = (FtpWebRequest)WebRequest.Create(uriString);
#pragma warning restore SYSLIB0014
                        request.Method = WebRequestMethods.Ftp.UploadFile;
                        request.Credentials = new NetworkCredential(settings.ID, settings.Password);
                        request.EnableSsl = false;

                        using (var fs = File.OpenRead(localPath))
                        using (var reqStream = await request.GetRequestStreamAsync())
                        {
                            // copy in chunks so we can observe cancellation and report progress
                            var buffer = new byte[81920];
                            long totalRead = 0;
                            int read;
                            while ((read = await fs.ReadAsync(buffer, 0, buffer.Length, cts.Token)) > 0)
                            {
                                await reqStream.WriteAsync(buffer, 0, read, cts.Token);
                                totalRead += read;
                                if (fs.Length > 0)
                                {
                                    int percentFile = (int)(totalRead * 100L / fs.Length);
                                    progressForm.SetProgress(percentFile);
                                }
                                if (cts.Token.IsCancellationRequested) break;
                            }
                        }

                        using var resp = (FtpWebResponse)await request.GetResponseAsync();
                        succeeded.Add(localPath);
                        progressForm.AppendLine($"업로드 완료: {localPath}");
                        // Mark uploaded checkbox for this row
                        try
                        {
                            if (uploadedIndex >= 0)
                            {
                                row.Cells[uploadedIndex].Value = true;
                            }
                        }
                        catch { }
                    }
                    catch (OperationCanceledException)
                    {
                        failed.Add($"행 {row.Index + 1}: 업로드 취소");
                        progressForm.AppendLine($"업로드 취소: {localPath}");
                        break;
                    }
                    catch (Exception ex)
                    {
                        failed.Add($"행 {row.Index + 1}: 업로드 실패 - {ex.Message}");
                        progressForm.AppendLine($"업로드 실패: {localPath} - {ex.Message}");
                    }

                    done++;
                    if (total > 0)
                    {
                        int overall = (int)(done * 100L / total);
                        progressForm.SetProgress(overall);
                    }
                }

                progressForm.AppendLine("업로드 작업 완료");
                progressForm.SetCurrentFile("완료");
                // allow user to see final entries
                try { await System.Threading.Tasks.Task.Delay(400, cts.Token); } catch { }
                try { progressForm.Close(); } catch { }

                var sb = new StringBuilder();
                sb.AppendLine("업로드 작업 완료");
                if (succeeded.Count > 0)
                {
                    sb.AppendLine("성공:");
                    foreach (var s in succeeded) sb.AppendLine(s);
                }
                if (failed.Count > 0)
                {
                    sb.AppendLine();
                    sb.AppendLine("실패:");
                    foreach (var f in failed) sb.AppendLine(f);
                }

                MessageBox.Show(this, sb.ToString(), "업로드 결과", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            finally
            {
                try { this.btnUpload.Enabled = true; } catch { }
            }
        }

        private void BtnLoad_Click(object? sender, EventArgs e)
        {
            // Prompt for XML file; default to project/sample file
            using var dlg = new OpenFileDialog();
            dlg.Filter = "XML files (*.xml)|*.xml|All files (*.*)|*.*";
            dlg.Title = "Load file list (XML)";
            var defaultPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "sample_file_list.xml");
            if (File.Exists(defaultPath)) dlg.InitialDirectory = Path.GetDirectoryName(defaultPath);
            dlg.FileName = Path.GetFileName(defaultPath);

            if (dlg.ShowDialog(this) != DialogResult.OK) return;

            try
            {
                var doc = XDocument.Load(dlg.FileName);
                PopulateGridFromXml(doc);
                // Remember the loaded path so Save can overwrite it
                this.currentXmlPath = dlg.FileName;
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"파일을 읽는 중 오류가 발생했습니다:\n{ex.Message}", "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void PopulateGridFromXml(XDocument doc)
        {
            var rows = doc.Root?.Elements("File");
            if (rows == null) return;

            this.dataGridViewFiles.Rows.Clear();

            int rowNumber = 1;
            foreach (var el in rows)
            {
                // Ignore <No> from XML; generate sequential numbers starting at 1
                string fileName = (string?)el.Element("FileName") ?? string.Empty;
                string hasPayload = (string?)el.Element("HasPayload") ?? "false";
                string payloadFileName = (string?)el.Element("PayloadFileName") ?? string.Empty;
                string uploaded = (string?)el.Element("Uploaded") ?? "false";
                string description = (string?)el.Element("Description") ?? string.Empty;

                // Normalize boolean-like values
                bool hasPayloadBool = bool.TryParse(hasPayload, out var hp) && hp;
                bool uploadedBool = bool.TryParse(uploaded, out var up) && up;

                // Add row values; checkbox columns accept boolean values
                // Note: Designer now includes `colGeneratedPayloadName` between payload filename and uploaded flag.
                string generatedPayload = (string?)el.Element("GeneratedPayloadFileName") ?? string.Empty;
                this.dataGridViewFiles.Rows.Add(
                    false,
                    rowNumber.ToString(),
                    fileName,
                    hasPayloadBool,
                    payloadFileName,
                    generatedPayload,
                    uploadedBool,
                    description
                );

                rowNumber++;
            }
        }

        private void BtnSave_Click(object? sender, EventArgs e)
        {
            // If we have a currently loaded XML path, overwrite it. Otherwise prompt for a new path.
            string? targetPath = this.currentXmlPath;
            if (string.IsNullOrEmpty(targetPath))
            {
                using var dlg = new SaveFileDialog();
                dlg.Filter = "XML files (*.xml)|*.xml|All files (*.*)|*.*";
                dlg.Title = "Save file list (XML)";
                dlg.FileName = "file_list.xml";
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                targetPath = dlg.FileName;
            }

            try
            {
                SaveGridToXml(targetPath);
                // Remember the saved file as the current path
                this.currentXmlPath = targetPath;
                MessageBox.Show(this, "저장 완료.", "정보", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"파일을 저장하는 중 오류가 발생했습니다:\n{ex.Message}", "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnAdd_Click(object? sender, EventArgs e)
        {
            // Create a new row with sensible defaults.
            int nextNo = this.dataGridViewFiles.Rows.Count + 1;
            // Capture the index of the newly added row so we can set the generated payload filename if possible
            int newRowIndex = this.dataGridViewFiles.Rows.Add(
                false,             // Select checkbox (colSelect)
                nextNo.ToString(), // No.
                string.Empty,      // FileName
                false,             // HasPayload (checkbox)
                string.Empty,      // PayloadFileName
                string.Empty,      // GeneratedPayloadFileName
                false,             // Uploaded (checkbox)
                string.Empty       // Description
            );

            // Attempt to populate the '생성된 페이로드 파일명' cell based on the image filename if present
            try
            {
                int idxFileName = -1;
                int idxGenerated = -1;
                if (this.dataGridViewFiles.Columns.Contains("colFileName")) { var c = this.dataGridViewFiles.Columns["colFileName"]; if (c != null) idxFileName = c.Index; }
                if (this.dataGridViewFiles.Columns.Contains("colGeneratedPayloadName")) { var c2 = this.dataGridViewFiles.Columns["colGeneratedPayloadName"]; if (c2 != null) idxGenerated = c2.Index; }

                if (idxFileName >= 0 && idxGenerated >= 0)
                {
                    var val = Convert.ToString(this.dataGridViewFiles.Rows[newRowIndex].Cells[idxFileName].Value) ?? string.Empty;
                    // Prefer payload filename when available for generated payload name; otherwise fall back to main file name
                    int idxPayloadName = -1;
                    if (this.dataGridViewFiles.Columns.Contains("colPayloadName")) { var cp = this.dataGridViewFiles.Columns["colPayloadName"]; if (cp != null) idxPayloadName = cp.Index; }
                    string generated = string.Empty;
                    if (idxPayloadName >= 0)
                    {
                        var payloadVal = Convert.ToString(this.dataGridViewFiles.Rows[newRowIndex].Cells[idxPayloadName].Value) ?? string.Empty;
                        if (!string.IsNullOrWhiteSpace(payloadVal))
                        {
                            var payloadBase = Path.GetFileNameWithoutExtension(payloadVal);
                            // use payload base + _payload and assume same extension as main file if available
                            var ext = Path.GetExtension(val);
                            generated = payloadBase + "_payload" + ext;
                        }
                    }
                    if (string.IsNullOrWhiteSpace(generated) && !string.IsNullOrWhiteSpace(val))
                    {
                        var nameNoExt = Path.GetFileNameWithoutExtension(val);
                        var ext = Path.GetExtension(val);
                        generated = nameNoExt + "_payload" + ext;
                    }
                    if (!string.IsNullOrWhiteSpace(generated)) this.dataGridViewFiles.Rows[newRowIndex].Cells[idxGenerated].Value = generated;
                }
            }
            catch { }
        }

        private void BtnDelete_Click(object? sender, EventArgs e)
        {
            // Remove selected rows (if any). If none selected, show info.
            var selected = this.dataGridViewFiles.SelectedRows.Cast<DataGridViewRow>().Where(r => !r.IsNewRow).ToList();
            if (selected.Count == 0)
            {
                MessageBox.Show(this, "삭제할 항목을 선택하세요.", "안내", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            foreach (var row in selected)
            {
                this.dataGridViewFiles.Rows.Remove(row);
            }
            // After deletion, renumber the No. column
            RenumberRows();
        }

        private void BtnInjectPayload_Click(object? sender, EventArgs e)
        {
            // Process selected rows: for each, compress payload file to a zip (in-memory),
            // read main file bytes, append zip bytes, and save result with `_payload` inserted before extension.
            var selectedRows = this.dataGridViewFiles.SelectedRows.Cast<DataGridViewRow>().Where(r => !r.IsNewRow).ToList();
            if (selectedRows.Count == 0)
            {
                MessageBox.Show(this, "페이로드를 주입할 항목을 선택하세요.", "안내", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // Determine column indices
            int idxFileName = -1;
            int idxPayloadName = -1;
            int idxGeneratedPayloadName = -1;
            int idxPayload = -1;
            if (this.dataGridViewFiles.Columns.Contains("colFileName")) { var c = this.dataGridViewFiles.Columns["colFileName"]; if (c != null) idxFileName = c.Index; }
            if (this.dataGridViewFiles.Columns.Contains("colPayloadName")) { var c2 = this.dataGridViewFiles.Columns["colPayloadName"]; if (c2 != null) idxPayloadName = c2.Index; }
            if (this.dataGridViewFiles.Columns.Contains("colGeneratedPayloadName")) { var c3 = this.dataGridViewFiles.Columns["colGeneratedPayloadName"]; if (c3 != null) idxGeneratedPayloadName = c3.Index; }
            if (this.dataGridViewFiles.Columns.Contains("colPayload")) { var c4 = this.dataGridViewFiles.Columns["colPayload"]; if (c4 != null) idxPayload = c4.Index; }

            if (idxFileName < 0)
            {
                MessageBox.Show(this, "메인 파일명 컬럼을 찾을 수 없습니다.", "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            if (idxPayloadName < 0)
            {
                MessageBox.Show(this, "페이로드 파일명 컬럼을 찾을 수 없습니다.", "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            var succeeded = new System.Collections.Generic.List<string>();
            var failed = new System.Collections.Generic.List<string>();

            foreach (var row in selectedRows)
            {
                try
                {
                    var mainVal = Convert.ToString(row.Cells[idxFileName].Value) ?? string.Empty;
                    var payloadVal = Convert.ToString(row.Cells[idxPayloadName].Value) ?? string.Empty;

                    if (string.IsNullOrWhiteSpace(payloadVal))
                    {
                        failed.Add($"행 {row.Index + 1}: 페이로드 파일명이 비어있음");
                        continue;
                    }

                    byte[] mainBytes;
                    try
                    {
                        if (!string.IsNullOrWhiteSpace(mainVal) && File.Exists(mainVal))
                        {
                            mainBytes = File.ReadAllBytes(mainVal);
                        }
                        else
                        {
                            // Use embedded default image (PNG) when main file is missing or path empty
                            var defBmp = GPhoto.Properties.Resources.Default;
                            if (defBmp == null)
                            {
                                failed.Add($"행 {row.Index + 1}: 기본 이미지 리소스를 찾을 수 없습니다.");
                                continue;
                            }
                            using var bmpCopy = new Bitmap(defBmp);
                            using var msMain = new MemoryStream();
                            bmpCopy.Save(msMain, System.Drawing.Imaging.ImageFormat.Png);
                            mainBytes = msMain.ToArray();
                        }
                    }
                    catch (Exception ex)
                    {
                        failed.Add($"행 {row.Index + 1}: 원본 파일을 읽는 중 오류 발생 - {ex.Message}");
                        continue;
                    }
                    if (!File.Exists(payloadVal))
                    {
                        failed.Add($"행 {row.Index + 1}: 페이로드 파일을 찾을 수 없음: {payloadVal}");
                        continue;
                    }

                    // Create zip containing the payload file (in memory)
                    byte[] zipBytes;
                    using (var ms = new MemoryStream())
                    {
                        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, true))
                        {
                            var entryName = Path.GetFileName(payloadVal);
                            var entry = zip.CreateEntry(entryName, CompressionLevel.Optimal);
                            using var entryStream = entry.Open();
                            using var fsPayload = File.OpenRead(payloadVal);
                            fsPayload.CopyTo(entryStream);
                        }
                        zipBytes = ms.ToArray();
                    }

                    // Combine main bytes + zip bytes
                    var outBytes = new byte[mainBytes.Length + zipBytes.Length];
                    Buffer.BlockCopy(mainBytes, 0, outBytes, 0, mainBytes.Length);
                    Buffer.BlockCopy(zipBytes, 0, outBytes, mainBytes.Length, zipBytes.Length);

                    // Construct output filename: use payload file base name + _payload, keep original main file extension
                    // Determine output directory and extension. If main path missing, fall back to payload dir or app base dir.
                    var dir = Path.GetDirectoryName(mainVal);
                    if (string.IsNullOrWhiteSpace(dir)) dir = Path.GetDirectoryName(payloadVal);
                    if (string.IsNullOrWhiteSpace(dir)) dir = AppDomain.CurrentDomain.BaseDirectory;
                    var payloadBase = Path.GetFileNameWithoutExtension(payloadVal);
                    var ext = Path.GetExtension(mainVal);
                    if (string.IsNullOrWhiteSpace(ext)) ext = ".png"; // default to PNG when main file absent
                    var outName = payloadBase + "_payload" + ext;
                    var outPath = Path.Combine(dir, outName);

                    File.WriteAllBytes(outPath, outBytes);
                    succeeded.Add(outPath);

                    // Mark the payload checkbox as true for this row
                    try
                    {
                        if (idxPayload >= 0)
                        {
                            row.Cells[idxPayload].Value = true;
                        }
                    }
                    catch { }

                    // If the grid has a 'GeneratedPayloadFileName' column, write the generated path there for this row
                    try
                    {
                        if (idxGeneratedPayloadName >= 0)
                        {
                            row.Cells[idxGeneratedPayloadName].Value = outPath;
                        }
                    }
                    catch { }
                }
                catch (Exception ex)
                {
                    failed.Add($"행 {row.Index + 1}: 예외 발생 - {ex.Message}");
                }
            }

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("페이로드 주입 작업 완료");
            if (succeeded.Count > 0)
            {
                sb.AppendLine("저장된 파일:");
                foreach (var s in succeeded) sb.AppendLine(s);
            }
            if (failed.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("실패 항목:");
                foreach (var f in failed) sb.AppendLine(f);
            }

            MessageBox.Show(this, sb.ToString(), "결과", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void BtnExtractPayload_Click(object? sender, EventArgs e)
        {
            var selectedRows = this.dataGridViewFiles.SelectedRows.Cast<DataGridViewRow>().Where(r => !r.IsNewRow).ToList();
            if (selectedRows.Count == 0)
            {
                MessageBox.Show(this, "추출할 항목을 선택하세요.", "안내", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            int idxFileName = -1;
            int idxGeneratedPayloadName = -1;
            if (this.dataGridViewFiles.Columns.Contains("colFileName")) { var c = this.dataGridViewFiles.Columns["colFileName"]; if (c != null) idxFileName = c.Index; }
            if (this.dataGridViewFiles.Columns.Contains("colGeneratedPayloadName")) { var c2 = this.dataGridViewFiles.Columns["colGeneratedPayloadName"]; if (c2 != null) idxGeneratedPayloadName = c2.Index; }

            var succeeded = new System.Collections.Generic.List<string>();
            var failed = new System.Collections.Generic.List<string>();

            foreach (var row in selectedRows)
            {
                try
                {
                    // Prefer generated payload file if present, otherwise fall back to file name
                    string src = string.Empty;
                    if (idxGeneratedPayloadName >= 0) src = Convert.ToString(row.Cells[idxGeneratedPayloadName].Value) ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(src) && idxFileName >= 0) src = Convert.ToString(row.Cells[idxFileName].Value) ?? string.Empty;

                    if (string.IsNullOrWhiteSpace(src) || !File.Exists(src))
                    {
                        failed.Add($"행 {row.Index + 1}: 추출 대상 파일을 찾을 수 없음: {src}");
                        continue;
                    }

                    byte[] data = File.ReadAllBytes(src);

                    // Search for last occurrence of ZIP local file header signature PK\x03\x04
                    byte[] sig = new byte[] { 0x50, 0x4B, 0x03, 0x04 };
                    int pos = -1;
                    for (int i = data.Length - sig.Length; i >= 0; i--)
                    {
                        bool match = true;
                        for (int j = 0; j < sig.Length; j++)
                        {
                            if (data[i + j] != sig[j]) { match = false; break; }
                        }
                        if (match) { pos = i; break; }
                    }

                    if (pos < 0)
                    {
                        failed.Add($"행 {row.Index + 1}: 파일에 포함된 ZIP 시그니처를 찾을 수 없습니다: {src}");
                        continue;
                    }

                    // Extract bytes from pos to end into a zip file
                    var dir = Path.GetDirectoryName(src) ?? string.Empty;
                    var baseName = Path.GetFileNameWithoutExtension(src);
                    var outName = baseName + "_extract.zip";
                    var outPath = Path.Combine(dir, outName);

                    int len = data.Length - pos;
                    byte[] zipBytes = new byte[len];
                    Buffer.BlockCopy(data, pos, zipBytes, 0, len);
                    File.WriteAllBytes(outPath, zipBytes);
                    succeeded.Add(outPath);
                }
                catch (Exception ex)
                {
                    failed.Add($"행 {row.Index + 1}: 예외 발생 - {ex.Message}");
                }
            }

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("페이로드 추출 작업 완료");
            if (succeeded.Count > 0)
            {
                sb.AppendLine("생성된 파일:");
                foreach (var s in succeeded) sb.AppendLine(s);
            }
            if (failed.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("실패 항목:");
                foreach (var f in failed) sb.AppendLine(f);
            }

            MessageBox.Show(this, sb.ToString(), "결과", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void BatchInject_Click(object? sender, EventArgs e)
        {
            // Find rows where payload checkbox is false/unchecked AND payload filename cell has a non-empty value
            int idxFileName = -1;
            int idxPayloadName = -1;
            int idxGeneratedPayloadName = -1;
            int idxPayload = -1;
            if (this.dataGridViewFiles.Columns.Contains("colFileName")) { var c = this.dataGridViewFiles.Columns["colFileName"]; if (c != null) idxFileName = c.Index; }
            if (this.dataGridViewFiles.Columns.Contains("colPayloadName")) { var c2 = this.dataGridViewFiles.Columns["colPayloadName"]; if (c2 != null) idxPayloadName = c2.Index; }
            if (this.dataGridViewFiles.Columns.Contains("colGeneratedPayloadName")) { var c3 = this.dataGridViewFiles.Columns["colGeneratedPayloadName"]; if (c3 != null) idxGeneratedPayloadName = c3.Index; }
            if (this.dataGridViewFiles.Columns.Contains("colPayload")) { var c4 = this.dataGridViewFiles.Columns["colPayload"]; if (c4 != null) idxPayload = c4.Index; }

            if (idxFileName < 0 || idxPayloadName < 0)
            {
                MessageBox.Show(this, "필요한 컬럼을 찾을 수 없습니다.", "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            var succeeded = new System.Collections.Generic.List<string>();
            var failed = new System.Collections.Generic.List<string>();

            foreach (DataGridViewRow row in this.dataGridViewFiles.Rows)
            {
                if (row.IsNewRow) continue;

                bool hasPayloadBool = false;
                try
                {
                    var val = row.Cells[idxPayload].Value;
                    if (val is bool b) hasPayloadBool = b;
                    else if (val != null) bool.TryParse(val.ToString(), out hasPayloadBool);
                }
                catch { }

                var payloadVal = Convert.ToString(row.Cells[idxPayloadName].Value) ?? string.Empty;
                if (hasPayloadBool) continue; // skip rows already marked as having payload
                if (string.IsNullOrWhiteSpace(payloadVal)) continue; // skip rows without payload filename

                try
                {
                    var mainVal = Convert.ToString(row.Cells[idxFileName].Value) ?? string.Empty;
                    // Allow missing main file; fallback to embedded default image when absent
                    byte[] mainBytes;
                    try
                    {
                        if (!string.IsNullOrWhiteSpace(mainVal) && File.Exists(mainVal))
                        {
                            mainBytes = File.ReadAllBytes(mainVal);
                        }
                        else
                        {
                            var defBmp = GPhoto.Properties.Resources.Default;
                            if (defBmp == null)
                            {
                                failed.Add($"행 {row.Index + 1}: 기본 이미지 리소스를 찾을 수 없습니다.");
                                continue;
                            }
                            using var bmpCopy = new Bitmap(defBmp);
                            using var msMain = new MemoryStream();
                            bmpCopy.Save(msMain, System.Drawing.Imaging.ImageFormat.Png);
                            mainBytes = msMain.ToArray();
                        }
                    }
                    catch (Exception ex)
                    {
                        failed.Add($"행 {row.Index + 1}: 원본 파일을 읽는 중 오류 발생 - {ex.Message}");
                        continue;
                    }
                    if (!File.Exists(payloadVal))
                    {
                        failed.Add($"행 {row.Index + 1}: 페이로드 파일을 찾을 수 없음: {payloadVal}");
                        continue;
                    }

                    // create zip containing payload (in memory)
                    byte[] zipBytes;
                    using (var ms = new MemoryStream())
                    {
                        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, true))
                        {
                            var entryName = Path.GetFileName(payloadVal);
                            var entry = zip.CreateEntry(entryName, CompressionLevel.Optimal);
                            using var entryStream = entry.Open();
                            using var fsPayload = File.OpenRead(payloadVal);
                            fsPayload.CopyTo(entryStream);
                        }
                        zipBytes = ms.ToArray();
                    }

                    var outBytes = new byte[mainBytes.Length + zipBytes.Length];
                    Buffer.BlockCopy(mainBytes, 0, outBytes, 0, mainBytes.Length);
                    Buffer.BlockCopy(zipBytes, 0, outBytes, mainBytes.Length, zipBytes.Length);

                    // Determine output directory and extension. If main path missing, fall back to payload dir or app base dir.
                    var dir = Path.GetDirectoryName(mainVal);
                    if (string.IsNullOrWhiteSpace(dir)) dir = Path.GetDirectoryName(payloadVal);
                    if (string.IsNullOrWhiteSpace(dir)) dir = AppDomain.CurrentDomain.BaseDirectory;
                    var payloadBase = Path.GetFileNameWithoutExtension(payloadVal);
                    var ext = Path.GetExtension(mainVal);
                    if (string.IsNullOrWhiteSpace(ext)) ext = ".png"; // default to PNG when main file absent
                    var outName = payloadBase + "_payload" + ext;
                    var outPath = Path.Combine(dir, outName);

                    File.WriteAllBytes(outPath, outBytes);
                    succeeded.Add(outPath);

                    try
                    {
                        if (idxGeneratedPayloadName >= 0)
                        {
                            row.Cells[idxGeneratedPayloadName].Value = outPath;
                        }
                    }
                    catch { }

                    // Mark the payload checkbox as true for this row so the UI reflects the injection
                    try
                    {
                        if (idxPayload >= 0)
                        {
                            row.Cells[idxPayload].Value = true;
                        }
                    }
                    catch { }
                }
                catch (Exception ex)
                {
                    failed.Add($"행 {row.Index + 1}: 예외 발생 - {ex.Message}");
                }
            }

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("배치 주입 작업 완료");
            if (succeeded.Count > 0)
            {
                sb.AppendLine("저장된 파일:");
                foreach (var s in succeeded) sb.AppendLine(s);
            }
            if (failed.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("실패 항목:");
                foreach (var f in failed) sb.AppendLine(f);
            }

            MessageBox.Show(this, sb.ToString(), "결과", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void BatchExtract_Click(object? sender, EventArgs e)
        {
            int idxFileName = -1;
            int idxGeneratedPayloadName = -1;
            int idxPayloadName = -1;
            int idxPayload = -1;
            if (this.dataGridViewFiles.Columns.Contains("colFileName")) { var c = this.dataGridViewFiles.Columns["colFileName"]; if (c != null) idxFileName = c.Index; }
            if (this.dataGridViewFiles.Columns.Contains("colGeneratedPayloadName")) { var c2 = this.dataGridViewFiles.Columns["colGeneratedPayloadName"]; if (c2 != null) idxGeneratedPayloadName = c2.Index; }
            if (this.dataGridViewFiles.Columns.Contains("colPayloadName")) { var c3 = this.dataGridViewFiles.Columns["colPayloadName"]; if (c3 != null) idxPayloadName = c3.Index; }
            if (this.dataGridViewFiles.Columns.Contains("colPayload")) { var c4 = this.dataGridViewFiles.Columns["colPayload"]; if (c4 != null) idxPayload = c4.Index; }

            if (idxFileName < 0)
            {
                MessageBox.Show(this, "필요한 컬럼을 찾을 수 없습니다.", "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            var succeeded = new System.Collections.Generic.List<string>();
            var failed = new System.Collections.Generic.List<string>();

            foreach (DataGridViewRow row in this.dataGridViewFiles.Rows)
            {
                if (row.IsNewRow) continue;

                bool hasPayloadBool = false;
                try
                {
                    var val = row.Cells[idxPayload].Value;
                    if (val is bool b) hasPayloadBool = b;
                    else if (val != null) bool.TryParse(val.ToString(), out hasPayloadBool);
                }
                catch { }

                var payloadVal = Convert.ToString(row.Cells[idxPayloadName].Value) ?? string.Empty;
                if (hasPayloadBool) continue;
                if (string.IsNullOrWhiteSpace(payloadVal)) continue;

                try
                {
                    string src = string.Empty;
                    if (idxGeneratedPayloadName >= 0) src = Convert.ToString(row.Cells[idxGeneratedPayloadName].Value) ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(src) && idxFileName >= 0) src = Convert.ToString(row.Cells[idxFileName].Value) ?? string.Empty;

                    if (string.IsNullOrWhiteSpace(src) || !File.Exists(src))
                    {
                        failed.Add($"행 {row.Index + 1}: 추출 대상 파일을 찾을 수 없음: {src}");
                        continue;
                    }

                    byte[] data = File.ReadAllBytes(src);
                    byte[] sig = new byte[] { 0x50, 0x4B, 0x03, 0x04 };
                    int pos = -1;
                    for (int i = data.Length - sig.Length; i >= 0; i--)
                    {
                        bool match = true;
                        for (int j = 0; j < sig.Length; j++)
                        {
                            if (data[i + j] != sig[j]) { match = false; break; }
                        }
                        if (match) { pos = i; break; }
                    }

                    if (pos < 0)
                    {
                        failed.Add($"행 {row.Index + 1}: 파일에 포함된 ZIP 시그니처를 찾을 수 없습니다: {src}");
                        continue;
                    }

                    var dir = Path.GetDirectoryName(src) ?? string.Empty;
                    var baseName = Path.GetFileNameWithoutExtension(src);
                    var outName = baseName + "_extract.zip";
                    var outPath = Path.Combine(dir, outName);

                    int len = data.Length - pos;
                    byte[] zipBytes = new byte[len];
                    Buffer.BlockCopy(data, pos, zipBytes, 0, len);
                    File.WriteAllBytes(outPath, zipBytes);
                    succeeded.Add(outPath);
                }
                catch (Exception ex)
                {
                    failed.Add($"행 {row.Index + 1}: 예외 발생 - {ex.Message}");
                }
            }

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("배치 추출 작업 완료");
            if (succeeded.Count > 0)
            {
                sb.AppendLine("생성된 파일:");
                foreach (var s in succeeded) sb.AppendLine(s);
            }
            if (failed.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("실패 항목:");
                foreach (var f in failed) sb.AppendLine(f);
            }

            MessageBox.Show(this, sb.ToString(), "결과", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void SaveGridToXml(string path)
        {
            var doc = new XDocument(new XElement("Files"));

            // Determine column indices for expected columns (use names used in designer)
            int idxNo = -1;
            if (this.dataGridViewFiles.Columns.Contains("colNo")) { var c = this.dataGridViewFiles.Columns["colNo"]; if (c != null) idxNo = c.Index; }
            int idxFileName = -1;
            if (this.dataGridViewFiles.Columns.Contains("colFileName")) { var c = this.dataGridViewFiles.Columns["colFileName"]; if (c != null) idxFileName = c.Index; }
            int idxPayload = -1;
            if (this.dataGridViewFiles.Columns.Contains("colPayload")) { var c = this.dataGridViewFiles.Columns["colPayload"]; if (c != null) idxPayload = c.Index; }
            int idxPayloadName = -1;
            if (this.dataGridViewFiles.Columns.Contains("colPayloadName")) { var c = this.dataGridViewFiles.Columns["colPayloadName"]; if (c != null) idxPayloadName = c.Index; }
            int idxGeneratedPayloadName = -1;
            if (this.dataGridViewFiles.Columns.Contains("colGeneratedPayloadName")) { var cgen = this.dataGridViewFiles.Columns["colGeneratedPayloadName"]; if (cgen != null) idxGeneratedPayloadName = cgen.Index; }
            int idxUploaded = -1;
            if (this.dataGridViewFiles.Columns.Contains("colUploaded")) { var c = this.dataGridViewFiles.Columns["colUploaded"]; if (c != null) idxUploaded = c.Index; }
            int idxDescription = -1;
            if (this.dataGridViewFiles.Columns.Contains("colDescription")) { var c = this.dataGridViewFiles.Columns["colDescription"]; if (c != null) idxDescription = c.Index; }

            int exportNo = 1;
            foreach (DataGridViewRow row in this.dataGridViewFiles.Rows)
            {
                if (row.IsNewRow) continue;

                string noStr = idxNo >= 0 ? Convert.ToString(row.Cells[idxNo].Value) ?? exportNo.ToString() : exportNo.ToString();
                string fileName = idxFileName >= 0 ? Convert.ToString(row.Cells[idxFileName].Value) ?? string.Empty : string.Empty;

                bool hasPayloadBool = false;
                if (idxPayload >= 0)
                {
                    var val = row.Cells[idxPayload].Value;
                    if (val is bool b) hasPayloadBool = b;
                    else if (val != null) bool.TryParse(val.ToString(), out hasPayloadBool);
                }

                string payloadName = idxPayloadName >= 0 ? Convert.ToString(row.Cells[idxPayloadName].Value) ?? string.Empty : string.Empty;
                string generatedPayloadName = idxGeneratedPayloadName >= 0 ? Convert.ToString(row.Cells[idxGeneratedPayloadName].Value) ?? string.Empty : string.Empty;

                bool uploadedBool = false;
                if (idxUploaded >= 0)
                {
                    var val = row.Cells[idxUploaded].Value;
                    if (val is bool b2) uploadedBool = b2;
                    else if (val != null) bool.TryParse(val.ToString(), out uploadedBool);
                }

                string description = idxDescription >= 0 ? Convert.ToString(row.Cells[idxDescription].Value) ?? string.Empty : string.Empty;

                var fileElem = new XElement("File",
                    new XElement("No", noStr),
                    new XElement("FileName", fileName),
                    new XElement("HasPayload", hasPayloadBool.ToString().ToLowerInvariant()),
                    new XElement("PayloadFileName", payloadName),
                    new XElement("GeneratedPayloadFileName", generatedPayloadName),
                    new XElement("Uploaded", uploadedBool.ToString().ToLowerInvariant()),
                    new XElement("Description", description)
                );

                var root = doc.Root;
                if (root == null)
                {
                    root = new XElement("Files");
                    doc.Add(root);
                }
                root.Add(fileElem);
                exportNo++;
            }

            // Ensure directory exists
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);

            doc.Save(path);
        }

        private void DataGridViewFiles_CurrentCellDirtyStateChanged(object? sender, EventArgs e)
        {
            // Commit edits so CellValueChanged fires immediately (helps when user edits cells)
            if (this.dataGridViewFiles.IsCurrentCellDirty)
            {
                this.dataGridViewFiles.CommitEdit(DataGridViewDataErrorContexts.Commit);
            }
        }

        // Allowed file extensions for upload to Google Photos (images + common video formats).
        private static readonly System.Collections.Generic.HashSet<string> _allowedExtensions = new(System.StringComparer.OrdinalIgnoreCase)
        {
            // Images
            ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".tif", ".tiff", ".webp", ".heic", ".heif",
            // Videos
            ".mp4", ".mov", ".m4v", ".avi", ".wmv", ".mkv", ".webm", ".3gp", ".3g2", ".mpeg", ".mpg"
        };

        // Subset of allowed extensions that are images (will be shown in the picture preview)
        private static readonly System.Collections.Generic.HashSet<string> _imageExtensions = new(System.StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".tif", ".tiff", ".webp", ".heic", ".heif"
        };

        private static bool IsImageExtension(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return false;
            try
            {
                var ext = Path.GetExtension(path);
                return !string.IsNullOrEmpty(ext) && _imageExtensions.Contains(ext);
            }
            catch { return false; }
        }

        private void UpdatePreviewForPath(string? path)
        {
            // Dispose previous image if any
            try
            {
                if (this.pictureBoxPreview.Image != null)
                {
                    var old = this.pictureBoxPreview.Image;
                    this.pictureBoxPreview.Image = null;
                    old.Dispose();
                }
            }
            catch { }
            // Determine the path to load. If the provided path is invalid or missing,
            // fall back to the built-in default image at `assets/default.png` (relative
            // to the application base directory).
            string? loadPath = null;

            if (!string.IsNullOrWhiteSpace(path) && IsImageExtension(path) && File.Exists(path))
            {
                loadPath = path;
            }
            // If not a valid image path, fall back to embedded resource only (do not read external assets/default.png)

            if (string.IsNullOrWhiteSpace(loadPath))
            {
                // Use embedded resource-only default (via Properties.Resources)
                try
                {
                    var def = GPhoto.Properties.Resources.Default;
                    if (def != null)
                    {
                        // The resource property returns a fresh Bitmap; assign a copy so disposal remains local
                        this.pictureBoxPreview.Image = new Bitmap(def);
                        def.Dispose();
                    }
                    return;
                }
                catch
                {
                    return;
                }
            }

            try
            {
                using var fs = File.OpenRead(loadPath!);
                using var img = Image.FromStream(fs);
                // Make a copy so the file isn't locked by Image
                var bmp = new Bitmap(img);
                this.pictureBoxPreview.Image = bmp;
            }
            catch
            {
                // ignore preview errors
            }
        }

        private System.IO.Stream? GetEmbeddedDefaultImageStream()
        {
            // No longer used - retained for compatibility but returns null
            return null;
        }

        private static bool IsAllowedFileName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;
            try
            {
                var ext = Path.GetExtension(name);
                return !string.IsNullOrEmpty(ext) && _allowedExtensions.Contains(ext);
            }
            catch { return false; }
        }

        private void DataGridViewFiles_CellContentClick(object? sender, DataGridViewCellEventArgs e)
        {
            // (old handler removed) now use CellMouseClick on colFileName to detect embedded button clicks
        }

        private void DataGridViewFiles_CellValidating(object? sender, DataGridViewCellValidatingEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            if (!this.dataGridViewFiles.Columns.Contains("colFileName")) return;
            var col = this.dataGridViewFiles.Columns["colFileName"];
            if (col == null) return;

            if (e.ColumnIndex == col.Index)
            {
                var newVal = Convert.ToString(e.FormattedValue) ?? string.Empty;
                // Allow empty filename (no file selected) without showing an error
                if (string.IsNullOrWhiteSpace(newVal))
                {
                    this.dataGridViewFiles.Rows[e.RowIndex].Cells[e.ColumnIndex].ErrorText = string.Empty;
                    return;
                }

                if (!IsAllowedFileName(newVal))
                {
                    // Reject invalid non-empty value
                    this.dataGridViewFiles.Rows[e.RowIndex].Cells[e.ColumnIndex].ErrorText = "구글 포토에 업로드 가능한 파일만 입력할 수 있습니다.";
                    MessageBox.Show(this, "파일명은 구글 포토에 업로드 가능한 확장자만 입력할 수 있습니다. 예: .jpg, .png, .mp4", "입력 오류", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    e.Cancel = true;
                }
            }
        }

        private void DataGridViewFiles_CellEndEdit(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            // Clear any error text after editing
            if (e.RowIndex < this.dataGridViewFiles.Rows.Count)
            {
                var cell = this.dataGridViewFiles.Rows[e.RowIndex].Cells[e.ColumnIndex];
                if (cell != null) cell.ErrorText = string.Empty;
            }
        }

        private Rectangle GetFileButtonRectRelative(int cellWidth, int cellHeight)
        {
            int size = Math.Min(cellHeight - 6, 20);
            int x = cellWidth - size - 4;
            int y = (cellHeight - size) / 2;
            return new Rectangle(x, y, size, size);
        }

        private void DataGridViewFiles_CellPainting(object? sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;

            // We support embedded file-select buttons for both `colFileName` and `colPayloadName`.
            int fileColIndex = -1;
            int payloadColIndex = -1;
            if (this.dataGridViewFiles.Columns.Contains("colFileName")) { var c = this.dataGridViewFiles.Columns["colFileName"]; if (c != null) fileColIndex = c.Index; }
            if (this.dataGridViewFiles.Columns.Contains("colPayloadName")) { var c2 = this.dataGridViewFiles.Columns["colPayloadName"]; if (c2 != null) payloadColIndex = c2.Index; }

            if (e.ColumnIndex != fileColIndex && e.ColumnIndex != payloadColIndex) return;

            // Paint background and border
            e.Paint(e.CellBounds, DataGridViewPaintParts.Background | DataGridViewPaintParts.Border);

            // Compute button rectangle relative to cell
            var btnRel = GetFileButtonRectRelative(e.CellBounds.Width, e.CellBounds.Height);

            // Draw text clipped to left of button
            var textRect = new Rectangle(e.CellBounds.Left + 2, e.CellBounds.Top + 2, e.CellBounds.Width - btnRel.Width - 8, e.CellBounds.Height - 4);
            string text = Convert.ToString(e.FormattedValue) ?? string.Empty;
            var dc = e.Graphics;
            var font = e.CellStyle?.Font ?? this.Font;
            var foreColor = e.CellStyle?.ForeColor ?? SystemColors.ControlText;
            if (dc != null)
            {
                TextRenderer.DrawText(dc, text, font, textRect, foreColor, TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
            }

            // Draw small button (use ControlPaint)
            var btnRect = new Rectangle(e.CellBounds.Left + btnRel.Left, e.CellBounds.Top + btnRel.Top, btnRel.Width, btnRel.Height);
            if (dc != null)
            {
                ControlPaint.DrawButton(dc, btnRect, ButtonState.Normal);
                TextRenderer.DrawText(dc, "...", font, btnRect, foreColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }

            e.Handled = true;
        }

        private void DataGridViewFiles_CellMouseClick(object? sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            // Support clicking embedded button in either file-name or payload-name columns
            int fileColIndex = -1;
            int payloadColIndex = -1;
            if (this.dataGridViewFiles.Columns.Contains("colFileName")) { var c = this.dataGridViewFiles.Columns["colFileName"]; if (c != null) fileColIndex = c.Index; }
            if (this.dataGridViewFiles.Columns.Contains("colPayloadName")) { var c2 = this.dataGridViewFiles.Columns["colPayloadName"]; if (c2 != null) payloadColIndex = c2.Index; }

            if (e.ColumnIndex != fileColIndex && e.ColumnIndex != payloadColIndex) return;

            // Get cell display size
            var cellRect = this.dataGridViewFiles.GetCellDisplayRectangle(e.ColumnIndex, e.RowIndex, false);
            var btnRel = GetFileButtonRectRelative(cellRect.Width, cellRect.Height);

            var clickPoint = new Point(e.X, e.Y); // coordinates relative to cell
            if (!btnRel.Contains(clickPoint)) return;

            // If payload column clicked: allow any file type and put full path into payload cell
            if (e.ColumnIndex == payloadColIndex)
            {
                using var dlg = new OpenFileDialog();
                dlg.Filter = "All files (*.*)|*.*";
                dlg.Title = "Select payload file";
                dlg.Multiselect = false;
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                var selected = dlg.FileName;
                if (string.IsNullOrEmpty(selected)) return;

                // Put full path into the PayloadFileName cell of the same row
                this.dataGridViewFiles.Rows[e.RowIndex].Cells[payloadColIndex].Value = selected;

                // Select the row (no preview change required)
                try
                {
                    this.dataGridViewFiles.ClearSelection();
                    this.dataGridViewFiles.Rows[e.RowIndex].Selected = true;
                    this.dataGridViewFiles.CurrentCell = this.dataGridViewFiles.Rows[e.RowIndex].Cells[payloadColIndex];
                }
                catch { }

                return;
            }

            // Otherwise it's the main file-name column: existing behavior (filter by allowed extensions)
            if (e.ColumnIndex == fileColIndex)
            {
                using var dlg = new OpenFileDialog();
                var patterns = _allowedExtensions.Select(ext => "*" + ext).ToArray();
                var patternList = string.Join(";", patterns);
                dlg.Filter = $"Allowed files ({patternList})|{patternList}|All files (*.*)|*.*";
                dlg.Title = "Select file to assign to this row";
                dlg.Multiselect = false;

                if (dlg.ShowDialog(this) != DialogResult.OK) return;

                var selected = dlg.FileName;
                if (string.IsNullOrEmpty(selected)) return;

                if (!IsAllowedFileName(selected))
                {
                    MessageBox.Show(this, "선택한 파일은 구글 포토에 업로드 가능한 파일이 아닙니다.", "잘못된 파일", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                // Put full path into the FileName cell of the same row
                this.dataGridViewFiles.Rows[e.RowIndex].Cells[fileColIndex].Value = selected;

                // Select the row and update preview for the newly selected file
                try
                {
                    this.dataGridViewFiles.ClearSelection();
                    this.dataGridViewFiles.Rows[e.RowIndex].Selected = true;
                    this.dataGridViewFiles.CurrentCell = this.dataGridViewFiles.Rows[e.RowIndex].Cells[fileColIndex];
                }
                catch { }

                UpdatePreviewForPath(selected);
            }
        }

        private void DataGridViewFiles_CellValueChanged(object? sender, DataGridViewCellEventArgs e)
        {
            if (suppressDescriptionSync) return;
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            // If the selection checkbox column changed, update header checkbox state
            if (this.dataGridViewFiles.Columns.Contains("colSelect"))
            {
                var selCol = this.dataGridViewFiles.Columns["colSelect"];
                if (selCol != null && e.ColumnIndex == selCol.Index)
                {
                    if (this.headerCheckBox != null)
                    {
                        bool allChecked = true;
                        int total = 0;
                        foreach (DataGridViewRow r in this.dataGridViewFiles.Rows)
                        {
                            if (r.IsNewRow) continue;
                            total++;
                            var v = r.Cells[selCol.Index].Value;
                            bool b = false;
                            if (v is bool bb) b = bb;
                            else if (v != null) bool.TryParse(v.ToString(), out b);
                            if (!b) { allChecked = false; break; }
                        }
                        try { this.headerCheckBoxUpdating = true; this.headerCheckBox.Checked = (total > 0 && allChecked); } finally { this.headerCheckBoxUpdating = false; }
                    }
                }
            }
            // If the changed cell is the FileName for the currently selected row, update preview
            if (this.dataGridViewFiles.Columns.Contains("colFileName"))
            {
                var fileCol = this.dataGridViewFiles.Columns["colFileName"];
                if (fileCol != null && e.ColumnIndex == fileCol.Index)
                {
                    // Only update preview if that row is currently selected
                    if (this.dataGridViewFiles.SelectedRows.Cast<DataGridViewRow>().Any(r => r.Index == e.RowIndex))
                    {
                        var val = this.dataGridViewFiles.Rows[e.RowIndex].Cells[e.ColumnIndex].Value;
                        UpdatePreviewForPath(Convert.ToString(val));
                    }
                }
            }

            // If the changed cell is the description for the currently selected row, update the rich text box
            if (!this.dataGridViewFiles.Columns.Contains("colDescription")) return;
            var descCol = this.dataGridViewFiles.Columns["colDescription"];
            if (descCol == null) return;
            if (e.ColumnIndex != descCol.Index) return;

            // Only update if that row is currently selected
            if (this.dataGridViewFiles.SelectedRows.Cast<DataGridViewRow>().Any(r => r.Index == e.RowIndex))
            {
                try
                {
                    suppressDescriptionSync = true;
                    var val = this.dataGridViewFiles.Rows[e.RowIndex].Cells[e.ColumnIndex].Value;
                    this.richTextDescription.Text = Convert.ToString(val) ?? string.Empty;
                }
                finally
                {
                    suppressDescriptionSync = false;
                }
            }
        }

        private void DataGridViewFiles_SelectionChanged(object? sender, EventArgs e)
        {
            if (suppressDescriptionSync) return;

            // When selection changes, show the description of the first selected row
            if (this.dataGridViewFiles.SelectedRows.Count > 0)
            {
                var row = this.dataGridViewFiles.SelectedRows[0];
                if (row == null || row.IsNewRow)
                {
                    currentSelectedRowIndex = -1;
                    try { suppressDescriptionSync = true; this.richTextDescription.Text = string.Empty; } finally { suppressDescriptionSync = false; }
                    return;
                }

                int descIndex = -1;
                if (this.dataGridViewFiles.Columns.Contains("colDescription"))
                {
                    var c = this.dataGridViewFiles.Columns["colDescription"];
                    if (c != null) descIndex = c.Index;
                }

                currentSelectedRowIndex = row.Index;

                if (descIndex >= 0)
                {
                    try
                    {
                        suppressDescriptionSync = true;
                        var val = row.Cells[descIndex].Value;
                        this.richTextDescription.Text = Convert.ToString(val) ?? string.Empty;
                    }
                    finally { suppressDescriptionSync = false; }
                }
                // Update image preview from the filename cell of the selected row
                if (this.dataGridViewFiles.Columns.Contains("colFileName"))
                {
                    var fileCol = this.dataGridViewFiles.Columns["colFileName"];
                    if (fileCol != null)
                    {
                        var fileVal = Convert.ToString(row.Cells[fileCol.Index].Value);
                        UpdatePreviewForPath(fileVal);
                    }
                }
            }
            else
            {
                currentSelectedRowIndex = -1;
                try { suppressDescriptionSync = true; this.richTextDescription.Text = string.Empty; } finally { suppressDescriptionSync = false; }
            }
        }

        private void RichTextDescription_TextChanged(object? sender, EventArgs e)
        {
            if (suppressDescriptionSync) return;
            if (currentSelectedRowIndex < 0) return;
            if (currentSelectedRowIndex >= this.dataGridViewFiles.Rows.Count) return;

            var row = this.dataGridViewFiles.Rows[currentSelectedRowIndex];
            if (row.IsNewRow) return;

            int descIndex = -1;
            if (this.dataGridViewFiles.Columns.Contains("colDescription"))
            {
                var c = this.dataGridViewFiles.Columns["colDescription"];
                if (c != null) descIndex = c.Index;
            }

            if (descIndex < 0) return;

            try
            {
                suppressDescriptionSync = true;
                row.Cells[descIndex].Value = this.richTextDescription.Text;
            }
            finally { suppressDescriptionSync = false; }
        }

        private void RenumberRows()
        {
            // Find the 'No.' column index by name
            if (!this.dataGridViewFiles.Columns.Contains("colNo")) return;
            var col = this.dataGridViewFiles.Columns["colNo"];
            if (col == null) return;
            int noIndex = col.Index;

            int displayIndex = 1;
            for (int i = 0; i < this.dataGridViewFiles.Rows.Count; i++)
            {
                var row = this.dataGridViewFiles.Rows[i];
                if (row.IsNewRow) continue;
                row.Cells[noIndex].Value = displayIndex.ToString();
                displayIndex++;
            }
        }
    }
}
