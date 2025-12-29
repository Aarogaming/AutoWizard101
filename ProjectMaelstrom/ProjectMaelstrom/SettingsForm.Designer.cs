namespace ProjectMaelstrom
{
    partial class SettingsForm
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            label1 = new Label();
            ocrSpaceApiKey = new TextBox();
            saveSettingsBtn = new Button();
            label2 = new Label();
            selectedGameResolution = new ComboBox();
            themeModeLabel = new Label();
            themeModeCombo = new ComboBox();
            captureToggle = new CheckBox();
            audioToggle = new CheckBox();
            tuningToggle = new CheckBox();
            devTelemetryToggle = new CheckBox();
            devUiSnapshotsToggle = new CheckBox();
            captureUiSnapshotButton = new Button();
            playerPreviewToggle = new CheckBox();
            autoPauseToggle = new CheckBox();
            audioDeltaLabel = new Label();
            audioDeltaNumeric = new NumericUpDown();
            updatesLabel = new Label();
            feedUrlText = new TextBox();
            autoCheckUpdatesToggle = new CheckBox();
            checkUpdatesButton = new Button();
            downloadUpdateButton = new Button();
            applyUpdateButton = new Button();
            updaterStatusLabel = new Label();
            launchManagerButton = new Button();
            runDiagnosticsButton = new Button();
            viewDevSuggestionsButton = new Button();
            goldMinLabel = new Label();
            goldMinNumeric = new NumericUpDown();
            goldCapLabel = new Label();
            goldCapNumeric = new NumericUpDown();
            ((System.ComponentModel.ISupportInitialize)audioDeltaNumeric).BeginInit();
            ((System.ComponentModel.ISupportInitialize)goldMinNumeric).BeginInit();
            ((System.ComponentModel.ISupportInitialize)goldCapNumeric).BeginInit();
            SuspendLayout();
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new Point(14, 11);
            label1.Name = "label1";
            label1.Size = new Size(193, 30);
            label1.TabIndex = 0;
            label1.Text = "OCR Space API Key";
            // 
            // ocrSpaceApiKey
            // 
            ocrSpaceApiKey.Location = new Point(14, 44);
            ocrSpaceApiKey.Name = "ocrSpaceApiKey";
            ocrSpaceApiKey.Size = new Size(359, 35);
            ocrSpaceApiKey.TabIndex = 1;
            // 
            // saveSettingsBtn
            // 
            saveSettingsBtn.Location = new Point(13, 544);
            saveSettingsBtn.Name = "saveSettingsBtn";
            saveSettingsBtn.Size = new Size(360, 41);
            saveSettingsBtn.TabIndex = 4;
            saveSettingsBtn.Text = "Save";
            saveSettingsBtn.UseVisualStyleBackColor = true;
            saveSettingsBtn.Click += saveSettingsBtn_Click;
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Location = new Point(12, 95);
            label2.Name = "label2";
            label2.Size = new Size(231, 30);
            label2.TabIndex = 5;
            label2.Text = "Select Game Resolution";
            // 
            // selectedGameResolution
            // 
            selectedGameResolution.FormattingEnabled = true;
            selectedGameResolution.Items.AddRange(new object[] { "1280x720" });
            selectedGameResolution.Location = new Point(14, 133);
            selectedGameResolution.Name = "selectedGameResolution";
            selectedGameResolution.Size = new Size(359, 38);
            selectedGameResolution.TabIndex = 6;
            selectedGameResolution.Text = "1280x720";
            // 
            // themeModeLabel
            // 
            themeModeLabel.AutoSize = true;
            themeModeLabel.Location = new Point(14, 183);
            themeModeLabel.Name = "themeModeLabel";
            themeModeLabel.Size = new Size(124, 30);
            themeModeLabel.TabIndex = 7;
            themeModeLabel.Text = "Theme Mode";
            // 
            // themeModeCombo
            // 
            themeModeCombo.DropDownStyle = ComboBoxStyle.DropDownList;
            themeModeCombo.FormattingEnabled = true;
            themeModeCombo.Items.AddRange(new object[] { "System", "Wizard101" });
            themeModeCombo.Location = new Point(14, 220);
            themeModeCombo.Name = "themeModeCombo";
            themeModeCombo.Size = new Size(359, 38);
            themeModeCombo.TabIndex = 8;
            // 
            // captureToggle
            // 
            captureToggle.AutoSize = true;
            captureToggle.Location = new Point(14, 270);
            captureToggle.Name = "captureToggle";
            captureToggle.Size = new Size(270, 34);
            captureToggle.TabIndex = 9;
            captureToggle.Text = "Enable Dev Screen Capture";
            captureToggle.UseVisualStyleBackColor = true;
            // 
            // audioToggle
            // 
            audioToggle.AutoSize = true;
            audioToggle.Location = new Point(14, 308);
            audioToggle.Name = "audioToggle";
            audioToggle.Size = new Size(233, 34);
            audioToggle.TabIndex = 10;
            audioToggle.Text = "Enable Audio Recognizer";
            audioToggle.UseVisualStyleBackColor = true;
            // 
            // tuningToggle
            // 
            tuningToggle.AutoSize = true;
            tuningToggle.Location = new Point(14, 346);
            tuningToggle.Name = "tuningToggle";
            tuningToggle.Size = new Size(241, 34);
            tuningToggle.TabIndex = 11;
            tuningToggle.Text = "Enable SmartPlay Tuning";
            tuningToggle.UseVisualStyleBackColor = true;
            // 
            // devTelemetryToggle
            // 
            devTelemetryToggle.AutoSize = true;
            devTelemetryToggle.Location = new Point(14, 422);
            devTelemetryToggle.Name = "devTelemetryToggle";
            devTelemetryToggle.Size = new Size(329, 34);
            devTelemetryToggle.TabIndex = 12;
            devTelemetryToggle.Text = "Capture Dev Telemetry (logs only)";
            devTelemetryToggle.UseVisualStyleBackColor = true;
            // 
            // devUiSnapshotsToggle
            // 
            devUiSnapshotsToggle.AutoSize = true;
            devUiSnapshotsToggle.Location = new Point(14, 460);
            devUiSnapshotsToggle.Name = "devUiSnapshotsToggle";
            devUiSnapshotsToggle.Size = new Size(296, 34);
            devUiSnapshotsToggle.TabIndex = 13;
            devUiSnapshotsToggle.Text = "Enable Dev UI Snapshots";
            devUiSnapshotsToggle.UseVisualStyleBackColor = true;
            // 
            // captureUiSnapshotButton
            // 
            captureUiSnapshotButton.Location = new Point(14, 500);
            captureUiSnapshotButton.Name = "captureUiSnapshotButton";
            captureUiSnapshotButton.Size = new Size(478, 34);
            captureUiSnapshotButton.TabIndex = 14;
            captureUiSnapshotButton.Text = "Capture UI Snapshot (Dev)";
            captureUiSnapshotButton.UseVisualStyleBackColor = true;
            captureUiSnapshotButton.Visible = false;
            captureUiSnapshotButton.Click += captureUiSnapshotButton_Click;
            // 
            // playerPreviewToggle
            // 
            playerPreviewToggle.AutoSize = true;
            playerPreviewToggle.Location = new Point(14, 542);
            playerPreviewToggle.Name = "playerPreviewToggle";
            playerPreviewToggle.Size = new Size(270, 34);
            playerPreviewToggle.TabIndex = 15;
            playerPreviewToggle.Text = "Player Mode (Preview UI)";
            playerPreviewToggle.UseVisualStyleBackColor = true;
            playerPreviewToggle.Visible = false;
            // 
            // autoPauseToggle
            // 
            autoPauseToggle.AutoSize = true;
            autoPauseToggle.Location = new Point(14, 580);
            autoPauseToggle.Name = "autoPauseToggle";
            autoPauseToggle.Size = new Size(321, 34);
            autoPauseToggle.TabIndex = 16;
            autoPauseToggle.Text = "Auto-pause SmartPlay on focus loss";
            autoPauseToggle.UseVisualStyleBackColor = true;
            // 
            // audioDeltaLabel
            // 
            audioDeltaLabel.AutoSize = true;
            audioDeltaLabel.Location = new Point(200, 312);
            audioDeltaLabel.Name = "audioDeltaLabel";
            audioDeltaLabel.Size = new Size(166, 30);
            audioDeltaLabel.TabIndex = 12;
            audioDeltaLabel.Text = "Audio Sensitivity";
            // 
            // audioDeltaNumeric
            // 
            audioDeltaNumeric.DecimalPlaces = 2;
            audioDeltaNumeric.Increment = new decimal(new int[] { 1, 0, 0, 131072 });
            audioDeltaNumeric.Location = new Point(372, 308);
            audioDeltaNumeric.Maximum = new decimal(new int[] { 50, 0, 0, 131072 });
            audioDeltaNumeric.Minimum = new decimal(new int[] { 1, 0, 0, 196608 });
            audioDeltaNumeric.Name = "audioDeltaNumeric";
            audioDeltaNumeric.Size = new Size(120, 35);
            audioDeltaNumeric.TabIndex = 15;
            audioDeltaNumeric.Value = new decimal(new int[] { 12, 0, 0, 131072 });
            // 
            // goldMinLabel
            // 
            goldMinLabel.AutoSize = true;
            goldMinLabel.Location = new Point(14, 620);
            goldMinLabel.Name = "goldMinLabel";
            goldMinLabel.Size = new Size(229, 30);
            goldMinLabel.TabIndex = 26;
            goldMinLabel.Text = "Bazaar Gold Min (stop)";
            // 
            // goldMinNumeric
            // 
            goldMinNumeric.Increment = new decimal(new int[] { 1000, 0, 0, 0 });
            goldMinNumeric.Maximum = new decimal(new int[] { 1000000, 0, 0, 0 });
            goldMinNumeric.Location = new Point(260, 616);
            goldMinNumeric.Name = "goldMinNumeric";
            goldMinNumeric.Size = new Size(232, 35);
            goldMinNumeric.TabIndex = 27;
            // 
            // goldCapLabel
            // 
            goldCapLabel.AutoSize = true;
            goldCapLabel.Location = new Point(14, 662);
            goldCapLabel.Name = "goldCapLabel";
            goldCapLabel.Size = new Size(228, 30);
            goldCapLabel.TabIndex = 28;
            goldCapLabel.Text = "Bazaar Gold Cap (pause)";
            // 
            // goldCapNumeric
            // 
            goldCapNumeric.Increment = new decimal(new int[] { 1000, 0, 0, 0 });
            goldCapNumeric.Maximum = new decimal(new int[] { 1000000, 0, 0, 0 });
            goldCapNumeric.Location = new Point(260, 658);
            goldCapNumeric.Name = "goldCapNumeric";
            goldCapNumeric.Size = new Size(232, 35);
            goldCapNumeric.TabIndex = 29;
            // 
            // updatesLabel
            // 
            updatesLabel.AutoSize = true;
            updatesLabel.Location = new Point(14, 706);
            updatesLabel.Name = "updatesLabel";
            updatesLabel.Size = new Size(130, 30);
            updatesLabel.TabIndex = 16;
            updatesLabel.Text = "Update Feed:";
            // 
            // feedUrlText
            // 
            feedUrlText.Location = new Point(14, 740);
            feedUrlText.Name = "feedUrlText";
            feedUrlText.Size = new Size(478, 35);
            feedUrlText.TabIndex = 17;
            // 
            // autoCheckUpdatesToggle
            // 
            autoCheckUpdatesToggle.AutoSize = true;
            autoCheckUpdatesToggle.Location = new Point(14, 782);
            autoCheckUpdatesToggle.Name = "autoCheckUpdatesToggle";
            autoCheckUpdatesToggle.Size = new Size(202, 34);
            autoCheckUpdatesToggle.TabIndex = 18;
            autoCheckUpdatesToggle.Text = "Auto-check on start";
            autoCheckUpdatesToggle.UseVisualStyleBackColor = true;
            // 
            // checkUpdatesButton
            // 
            checkUpdatesButton.Location = new Point(14, 822);
            checkUpdatesButton.Name = "checkUpdatesButton";
            checkUpdatesButton.Size = new Size(150, 34);
            checkUpdatesButton.TabIndex = 19;
            checkUpdatesButton.Text = "Check";
            checkUpdatesButton.UseVisualStyleBackColor = true;
            checkUpdatesButton.Click += checkUpdatesButton_Click;
            // 
            // downloadUpdateButton
            // 
            downloadUpdateButton.Location = new Point(170, 822);
            downloadUpdateButton.Name = "downloadUpdateButton";
            downloadUpdateButton.Size = new Size(150, 34);
            downloadUpdateButton.TabIndex = 20;
            downloadUpdateButton.Text = "Download";
            downloadUpdateButton.UseVisualStyleBackColor = true;
            downloadUpdateButton.Click += downloadUpdateButton_Click;
            // 
            // applyUpdateButton
            // 
            applyUpdateButton.Location = new Point(326, 822);
            applyUpdateButton.Name = "applyUpdateButton";
            applyUpdateButton.Size = new Size(150, 34);
            applyUpdateButton.TabIndex = 21;
            applyUpdateButton.Text = "Stage/Apply";
            applyUpdateButton.UseVisualStyleBackColor = true;
            applyUpdateButton.Click += applyUpdateButton_Click;
            // 
            // updaterStatusLabel
            // 
            updaterStatusLabel.AutoSize = true;
            updaterStatusLabel.Location = new Point(14, 866);
            updaterStatusLabel.Name = "updaterStatusLabel";
            updaterStatusLabel.Size = new Size(122, 30);
            updaterStatusLabel.TabIndex = 22;
            updaterStatusLabel.Text = "Status: Idle";
            // 
            // launchManagerButton
            // 
            launchManagerButton.Location = new Point(14, 901);
            launchManagerButton.Name = "launchManagerButton";
            launchManagerButton.Size = new Size(478, 34);
            launchManagerButton.TabIndex = 23;
            launchManagerButton.Text = "Open Project Manager";
            launchManagerButton.UseVisualStyleBackColor = true;
            launchManagerButton.Click += launchManagerButton_Click;
            // 
            // refreshWikiButton
            // 
            refreshWikiButton.Location = new Point(14, 941);
            refreshWikiButton.Name = "refreshWikiButton";
            refreshWikiButton.Size = new Size(478, 34);
            refreshWikiButton.TabIndex = 24;
            refreshWikiButton.Text = "Refresh Wiki Cache";
            refreshWikiButton.UseVisualStyleBackColor = true;
            refreshWikiButton.Click += refreshWikiButton_Click;
            // 
            // runDiagnosticsButton
            // 
            runDiagnosticsButton.Location = new Point(14, 981);
            runDiagnosticsButton.Name = "runDiagnosticsButton";
            runDiagnosticsButton.Size = new Size(478, 34);
            runDiagnosticsButton.TabIndex = 25;
            runDiagnosticsButton.Text = "Run Diagnostics (Dev)";
            runDiagnosticsButton.UseVisualStyleBackColor = true;
            runDiagnosticsButton.Visible = false;
            runDiagnosticsButton.Click += runDiagnosticsButton_Click;
            // 
            // viewDevSuggestionsButton
            // 
            viewDevSuggestionsButton.Location = new Point(14, 1021);
            viewDevSuggestionsButton.Name = "viewDevSuggestionsButton";
            viewDevSuggestionsButton.Size = new Size(478, 34);
            viewDevSuggestionsButton.TabIndex = 26;
            viewDevSuggestionsButton.Text = "View Dev Suggestions";
            viewDevSuggestionsButton.UseVisualStyleBackColor = true;
            viewDevSuggestionsButton.Visible = false;
            viewDevSuggestionsButton.Click += viewDevSuggestionsButton_Click;
            // 
            // SettingsForm
            // 
            AutoScaleDimensions = new SizeF(12F, 30F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(540, 1080);
            Controls.Add(goldCapNumeric);
            Controls.Add(goldCapLabel);
            Controls.Add(goldMinNumeric);
            Controls.Add(goldMinLabel);
            Controls.Add(autoPauseToggle);
            Controls.Add(refreshWikiButton);
            Controls.Add(viewDevSuggestionsButton);
            Controls.Add(runDiagnosticsButton);
            Controls.Add(launchManagerButton);
            Controls.Add(captureUiSnapshotButton);
            Controls.Add(devUiSnapshotsToggle);
            Controls.Add(playerPreviewToggle);
            Controls.Add(devTelemetryToggle);
            Controls.Add(audioDeltaNumeric);
            Controls.Add(audioDeltaLabel);
            Controls.Add(tuningToggle);
            Controls.Add(audioToggle);
            Controls.Add(captureToggle);
            Controls.Add(updaterStatusLabel);
            Controls.Add(applyUpdateButton);
            Controls.Add(downloadUpdateButton);
            Controls.Add(checkUpdatesButton);
            Controls.Add(autoCheckUpdatesToggle);
            Controls.Add(feedUrlText);
            Controls.Add(updatesLabel);
            Controls.Add(themeModeCombo);
            Controls.Add(themeModeLabel);
            Controls.Add(selectedGameResolution);
            Controls.Add(label2);
            Controls.Add(saveSettingsBtn);
            Controls.Add(ocrSpaceApiKey);
            Controls.Add(label1);
            Name = "SettingsForm";
            Text = "Settings";
            Load += SettingsForm_Load;
            ((System.ComponentModel.ISupportInitialize)audioDeltaNumeric).EndInit();
            ((System.ComponentModel.ISupportInitialize)goldMinNumeric).EndInit();
            ((System.ComponentModel.ISupportInitialize)goldCapNumeric).EndInit();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private Label label1;
        private TextBox ocrSpaceApiKey;
        private Button saveSettingsBtn;
        private Label label2;
        private ComboBox selectedGameResolution;
        private Label themeModeLabel;
        private ComboBox themeModeCombo;
        private CheckBox captureToggle;
        private CheckBox audioToggle;
        private CheckBox tuningToggle;
        private CheckBox devTelemetryToggle;
        private CheckBox devUiSnapshotsToggle;
        private Button captureUiSnapshotButton;
        private CheckBox playerPreviewToggle;
        private CheckBox autoPauseToggle;
        private Label audioDeltaLabel;
        private NumericUpDown audioDeltaNumeric;
        private Label updatesLabel;
        private TextBox feedUrlText;
        private CheckBox autoCheckUpdatesToggle;
        private Button checkUpdatesButton;
        private Button downloadUpdateButton;
        private Button applyUpdateButton;
        private Label updaterStatusLabel;
        private Button launchManagerButton;
        private Button refreshWikiButton;
        private Button runDiagnosticsButton;
        private Button viewDevSuggestionsButton;
        private Label goldMinLabel;
        private NumericUpDown goldMinNumeric;
        private Label goldCapLabel;
        private NumericUpDown goldCapNumeric;
    }
}
