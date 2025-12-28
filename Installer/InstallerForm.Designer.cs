namespace Installer
{
    partial class InstallerForm
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
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
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            titleLabel = new Label();
            feedLabel = new Label();
            feedText = new TextBox();
            checkButton = new Button();
            installButton = new Button();
            uninstallButton = new Button();
            createPortableButton = new Button();
            updateButton = new Button();
            statusLabel = new Label();
            installedVersionLabel = new Label();
            latestVersionLabel = new Label();
            progressBar = new ProgressBar();
            stepLabel = new Label();
            activityList = new ListBox();
            SuspendLayout();
            // 
            // titleLabel
            // 
            titleLabel.AutoSize = true;
            titleLabel.Font = new Font("Segoe UI", 12F, FontStyle.Bold, GraphicsUnit.Point);
            titleLabel.Location = new Point(12, 9);
            titleLabel.Name = "titleLabel";
            titleLabel.Size = new Size(295, 28);
            titleLabel.TabIndex = 0;
            titleLabel.Text = "Project Maelstrom Installer";
            // 
            // feedLabel
            // 
            feedLabel.AutoSize = true;
            feedLabel.Location = new Point(12, 50);
            feedLabel.Name = "feedLabel";
            feedLabel.Size = new Size(187, 20);
            feedLabel.TabIndex = 1;
            feedLabel.Text = "Update feed (optional):";
            // 
            // feedText
            // 
            feedText.Location = new Point(12, 74);
            feedText.Name = "feedText";
            feedText.Size = new Size(500, 27);
            feedText.TabIndex = 2;
            // 
            // checkButton
            // 
            checkButton.Location = new Point(518, 74);
            checkButton.Name = "checkButton";
            checkButton.Size = new Size(94, 29);
            checkButton.TabIndex = 3;
            checkButton.Text = "Check";
            checkButton.UseVisualStyleBackColor = true;
            checkButton.Click += checkButton_Click;
            // 
            // installButton
            // 
            installButton.Location = new Point(12, 190);
            installButton.Name = "installButton";
            installButton.Size = new Size(150, 34);
            installButton.TabIndex = 4;
            installButton.Text = "Fresh Install";
            installButton.UseVisualStyleBackColor = true;
            installButton.Click += installButton_Click;
            // 
            // uninstallButton
            // 
            uninstallButton.Location = new Point(168, 190);
            uninstallButton.Name = "uninstallButton";
            uninstallButton.Size = new Size(150, 34);
            uninstallButton.TabIndex = 5;
            uninstallButton.Text = "Uninstall";
            uninstallButton.UseVisualStyleBackColor = true;
            uninstallButton.Click += uninstallButton_Click;
            // 
            // createPortableButton
            // 
            createPortableButton.Location = new Point(324, 190);
            createPortableButton.Name = "createPortableButton";
            createPortableButton.Size = new Size(150, 34);
            createPortableButton.TabIndex = 6;
            createPortableButton.Text = "Create Portable";
            createPortableButton.UseVisualStyleBackColor = true;
            createPortableButton.Click += createPortableButton_Click;
            // 
            // updateButton
            // 
            updateButton.Location = new Point(480, 190);
            updateButton.Name = "updateButton";
            updateButton.Size = new Size(132, 34);
            updateButton.TabIndex = 7;
            updateButton.Text = "Update";
            updateButton.UseVisualStyleBackColor = true;
            updateButton.Click += updateButton_Click;
            // 
            // statusLabel
            // 
            statusLabel.AutoSize = true;
            statusLabel.Location = new Point(12, 144);
            statusLabel.Name = "statusLabel";
            statusLabel.Size = new Size(114, 20);
            statusLabel.TabIndex = 6;
            statusLabel.Text = "Status: Waiting";
            // 
            // installedVersionLabel
            // 
            installedVersionLabel.AutoSize = true;
            installedVersionLabel.Location = new Point(12, 112);
            installedVersionLabel.Name = "installedVersionLabel";
            installedVersionLabel.Size = new Size(138, 20);
            installedVersionLabel.TabIndex = 7;
            installedVersionLabel.Text = "Installed: Unknown";
            // 
            // latestVersionLabel
            // 
            latestVersionLabel.AutoSize = true;
            latestVersionLabel.Location = new Point(250, 112);
            latestVersionLabel.Name = "latestVersionLabel";
            latestVersionLabel.Size = new Size(121, 20);
            latestVersionLabel.TabIndex = 8;
            latestVersionLabel.Text = "Latest: Unknown";
            // 
            // progressBar
            // 
            progressBar.Location = new Point(12, 230);
            progressBar.Name = "progressBar";
            progressBar.Size = new Size(600, 20);
            progressBar.Style = ProgressBarStyle.Continuous;
            progressBar.TabIndex = 9;
            // 
            // stepLabel
            // 
            stepLabel.AutoSize = true;
            stepLabel.Location = new Point(12, 260);
            stepLabel.Name = "stepLabel";
            stepLabel.Size = new Size(145, 20);
            stepLabel.TabIndex = 10;
            stepLabel.Text = "Step: Ready to start";
            // 
            // activityList
            // 
            activityList.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            activityList.FormattingEnabled = true;
            activityList.ItemHeight = 20;
            activityList.Location = new Point(12, 285);
            activityList.Name = "activityList";
            activityList.Size = new Size(600, 84);
            activityList.TabIndex = 11;
            // 
            // InstallerForm
            // 
            AutoScaleDimensions = new SizeF(8F, 20F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(628, 382);
            Controls.Add(progressBar);
            Controls.Add(activityList);
            Controls.Add(stepLabel);
            Controls.Add(latestVersionLabel);
            Controls.Add(installedVersionLabel);
            Controls.Add(statusLabel);
            Controls.Add(createPortableButton);
            Controls.Add(uninstallButton);
            Controls.Add(installButton);
            Controls.Add(checkButton);
            Controls.Add(feedText);
            Controls.Add(feedLabel);
            Controls.Add(titleLabel);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "InstallerForm";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "Project Maelstrom Installer";
            Load += InstallerForm_Load;
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private Label titleLabel;
        private Label feedLabel;
        private TextBox feedText;
        private Button checkButton;
        private Button installButton;
        private Button uninstallButton;
        private Button createPortableButton;
        private Button updateButton;
        private Label statusLabel;
        private Label installedVersionLabel;
        private Label latestVersionLabel;
        private ProgressBar progressBar;
        private Label stepLabel;
        private ListBox activityList;
    }
}
