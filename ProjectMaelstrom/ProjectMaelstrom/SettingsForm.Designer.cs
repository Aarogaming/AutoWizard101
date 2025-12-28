namespace ProjectMaelstrom
{
    partial class SettingsForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
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
            audioDeltaLabel = new Label();
            audioDeltaNumeric = new NumericUpDown();
            updatesLabel = new Label();
            feedUrlText = new TextBox();
            autoCheckUpdatesToggle = new CheckBox();
            checkUpdatesButton = new Button();
            downloadUpdateButton = new Button();
            applyUpdateButton = new Button();
            updaterStatusLabel = new Label();
            ((System.ComponentModel.ISupportInitialize)audioDeltaNumeric).BeginInit();
            SuspendLayout();
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new Point(14, 11);
            label1.Margin = new Padding(4, 0, 4, 0);
            label1.Name = "label1";
            label1.Size = new Size(193, 30);
            label1.TabIndex = 0;
            label1.Text = "OCR Space API Key";
            // 
            // ocrSpaceApiKey
            // 
            ocrSpaceApiKey.Location = new Point(14, 44);
            ocrSpaceApiKey.Margin = new Padding(4, 4, 4, 4);
            ocrSpaceApiKey.Name = "ocrSpaceApiKey";
            ocrSpaceApiKey.Size = new Size(359, 35);
            ocrSpaceApiKey.TabIndex = 1;
            // 
            // saveSettingsBtn
            // 
            saveSettingsBtn.Location = new Point(13, 378);
            saveSettingsBtn.Margin = new Padding(4, 4, 4, 4);
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
            audioDeltaNumeric.TabIndex = 13;
            audioDeltaNumeric.Value = new decimal(new int[] { 12, 0, 0, 131072 });
            // 
            // updatesLabel
            // 
            updatesLabel.AutoSize = true;
            updatesLabel.Location = new Point(14, 430);
            updatesLabel.Name = "updatesLabel";
            updatesLabel.Size = new Size(130, 30);
            updatesLabel.TabIndex = 14;
            updatesLabel.Text = "Update Feed:";
            // 
            // feedUrlText
            // 
            feedUrlText.Location = new Point(14, 464);
            feedUrlText.Name = "feedUrlText";
            feedUrlText.Size = new Size(478, 35);
            feedUrlText.TabIndex = 15;
            // 
            // autoCheckUpdatesToggle
            // 
            autoCheckUpdatesToggle.AutoSize = true;
            autoCheckUpdatesToggle.Location = new Point(14, 506);
            autoCheckUpdatesToggle.Name = "autoCheckUpdatesToggle";
            autoCheckUpdatesToggle.Size = new Size(202, 34);
            autoCheckUpdatesToggle.TabIndex = 16;
            autoCheckUpdatesToggle.Text = "Auto-check on start";
            autoCheckUpdatesToggle.UseVisualStyleBackColor = true;
            // 
            // checkUpdatesButton
            // 
            checkUpdatesButton.Location = new Point(14, 546);
            checkUpdatesButton.Name = "checkUpdatesButton";
            checkUpdatesButton.Size = new Size(150, 34);
            checkUpdatesButton.TabIndex = 17;
            checkUpdatesButton.Text = "Check";
            checkUpdatesButton.UseVisualStyleBackColor = true;
            checkUpdatesButton.Click += checkUpdatesButton_Click;
            // 
            // downloadUpdateButton
            // 
            downloadUpdateButton.Location = new Point(170, 546);
            downloadUpdateButton.Name = "downloadUpdateButton";
            downloadUpdateButton.Size = new Size(150, 34);
            downloadUpdateButton.TabIndex = 18;
            downloadUpdateButton.Text = "Download";
            downloadUpdateButton.UseVisualStyleBackColor = true;
            downloadUpdateButton.Click += downloadUpdateButton_Click;
            // 
            // applyUpdateButton
            // 
            applyUpdateButton.Location = new Point(326, 546);
            applyUpdateButton.Name = "applyUpdateButton";
            applyUpdateButton.Size = new Size(150, 34);
            applyUpdateButton.TabIndex = 19;
            applyUpdateButton.Text = "Stage/Apply";
            applyUpdateButton.UseVisualStyleBackColor = true;
            applyUpdateButton.Click += applyUpdateButton_Click;
            // 
            // updaterStatusLabel
            // 
            updaterStatusLabel.AutoSize = true;
            updaterStatusLabel.Location = new Point(14, 590);
            updaterStatusLabel.Name = "updaterStatusLabel";
            updaterStatusLabel.Size = new Size(122, 30);
            updaterStatusLabel.TabIndex = 20;
            updaterStatusLabel.Text = "Status: Idle";
            // 
            // SettingsForm
            // 
            AutoScaleDimensions = new SizeF(12F, 30F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(540, 640);
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
            Margin = new Padding(4, 4, 4, 4);
            Name = "SettingsForm";
            Text = "Settings";
            Load += SettingsForm_Load;
            ((System.ComponentModel.ISupportInitialize)audioDeltaNumeric).EndInit();
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
        private Label audioDeltaLabel;
        private NumericUpDown audioDeltaNumeric;
        private Label updatesLabel;
        private TextBox feedUrlText;
        private CheckBox autoCheckUpdatesToggle;
        private Button checkUpdatesButton;
        private Button downloadUpdateButton;
        private Button applyUpdateButton;
        private Label updaterStatusLabel;
    }
}
