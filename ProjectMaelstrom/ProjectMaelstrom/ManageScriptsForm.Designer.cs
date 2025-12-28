namespace ProjectMaelstrom
{
    partial class ManageScriptsForm
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
            scriptListBox = new ListBox();
            runScriptButton = new Button();
            stopScriptButton = new Button();
            refreshScriptsButton = new Button();
            importFromGithubButton = new Button();
            scriptStatusLabel = new Label();
            loadLogButton = new Button();
            logPreviewLabel = new Label();
            logPreviewTextBox = new TextBox();
            dryRunCheckBox = new CheckBox();
            openScriptFolderButton = new Button();
            openFullLogButton = new Button();
            openLibraryRootButton = new Button();
            SuspendLayout();
            // 
            // scriptListBox
            // 
            scriptListBox.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left;
            scriptListBox.FormattingEnabled = true;
            scriptListBox.ItemHeight = 20;
            scriptListBox.Location = new Point(12, 12);
            scriptListBox.Name = "scriptListBox";
            scriptListBox.Size = new Size(360, 464);
            scriptListBox.TabIndex = 0;
            // 
            // runScriptButton
            // 
            runScriptButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            runScriptButton.Location = new Point(384, 12);
            runScriptButton.Name = "runScriptButton";
            runScriptButton.Size = new Size(130, 32);
            runScriptButton.TabIndex = 1;
            runScriptButton.Text = "Run";
            runScriptButton.UseVisualStyleBackColor = true;
            runScriptButton.Click += runScriptButton_Click;
            // 
            // stopScriptButton
            // 
            stopScriptButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            stopScriptButton.Location = new Point(524, 12);
            stopScriptButton.Name = "stopScriptButton";
            stopScriptButton.Size = new Size(130, 32);
            stopScriptButton.TabIndex = 2;
            stopScriptButton.Text = "Stop";
            stopScriptButton.UseVisualStyleBackColor = true;
            stopScriptButton.Click += stopScriptButton_Click;
            // 
            // refreshScriptsButton
            // 
            refreshScriptsButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            refreshScriptsButton.Location = new Point(664, 12);
            refreshScriptsButton.Name = "refreshScriptsButton";
            refreshScriptsButton.Size = new Size(130, 32);
            refreshScriptsButton.TabIndex = 3;
            refreshScriptsButton.Text = "Refresh Library";
            refreshScriptsButton.UseVisualStyleBackColor = true;
            refreshScriptsButton.Click += refreshScriptsButton_Click;
            // 
            // importFromGithubButton
            // 
            importFromGithubButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            importFromGithubButton.Location = new Point(384, 52);
            importFromGithubButton.Name = "importFromGithubButton";
            importFromGithubButton.Size = new Size(410, 28);
            importFromGithubButton.TabIndex = 4;
            importFromGithubButton.Text = "Add from GitHub";
            importFromGithubButton.UseVisualStyleBackColor = true;
            importFromGithubButton.Click += importFromGithubButton_Click;
            // 
            // scriptStatusLabel
            // 
            scriptStatusLabel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            scriptStatusLabel.AutoSize = true;
            scriptStatusLabel.Location = new Point(384, 86);
            scriptStatusLabel.Name = "scriptStatusLabel";
            scriptStatusLabel.Size = new Size(88, 20);
            scriptStatusLabel.TabIndex = 4;
            scriptStatusLabel.Text = "Status: Idle";
            // 
            // loadLogButton
            // 
            loadLogButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            loadLogButton.Location = new Point(384, 116);
            loadLogButton.Name = "loadLogButton";
            loadLogButton.Size = new Size(410, 30);
            loadLogButton.TabIndex = 5;
            loadLogButton.Text = "Load Log";
            loadLogButton.UseVisualStyleBackColor = true;
            loadLogButton.Click += loadLogButton_Click;
            // 
            // logPreviewLabel
            // 
            logPreviewLabel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            logPreviewLabel.AutoSize = true;
            logPreviewLabel.Location = new Point(384, 152);
            logPreviewLabel.Name = "logPreviewLabel";
            logPreviewLabel.Size = new Size(84, 20);
            logPreviewLabel.TabIndex = 6;
            logPreviewLabel.Text = "Log Preview";
            // 
            // logPreviewTextBox
            // 
            logPreviewTextBox.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Right;
            logPreviewTextBox.Location = new Point(384, 176);
            logPreviewTextBox.Multiline = true;
            logPreviewTextBox.Name = "logPreviewTextBox";
            logPreviewTextBox.ReadOnly = true;
            logPreviewTextBox.ScrollBars = ScrollBars.Vertical;
            logPreviewTextBox.Size = new Size(410, 200);
            logPreviewTextBox.TabIndex = 7;
            // 
            // dryRunCheckBox
            // 
            dryRunCheckBox.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            dryRunCheckBox.AutoSize = true;
            dryRunCheckBox.Location = new Point(384, 386);
            dryRunCheckBox.Name = "dryRunCheckBox";
            dryRunCheckBox.Size = new Size(77, 24);
            dryRunCheckBox.TabIndex = 8;
            dryRunCheckBox.Text = "Dry run";
            dryRunCheckBox.UseVisualStyleBackColor = true;
            dryRunCheckBox.CheckedChanged += dryRunCheckBox_CheckedChanged;
            // 
            // openScriptFolderButton
            // 
            openScriptFolderButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            openScriptFolderButton.Location = new Point(384, 416);
            openScriptFolderButton.Name = "openScriptFolderButton";
            openScriptFolderButton.Size = new Size(410, 30);
            openScriptFolderButton.TabIndex = 9;
            openScriptFolderButton.Text = "Open Script Folder";
            openScriptFolderButton.UseVisualStyleBackColor = true;
            openScriptFolderButton.Click += openScriptFolderButton_Click;
            // 
            // openLibraryRootButton
            // 
            openLibraryRootButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            openLibraryRootButton.Location = new Point(384, 384);
            openLibraryRootButton.Name = "openLibraryRootButton";
            openLibraryRootButton.Size = new Size(410, 30);
            openLibraryRootButton.TabIndex = 11;
            openLibraryRootButton.Text = "Open Script Library";
            openLibraryRootButton.UseVisualStyleBackColor = true;
            openLibraryRootButton.Click += openLibraryRootButton_Click;
            // 
            // openFullLogButton
            // 
            openFullLogButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            openFullLogButton.Location = new Point(384, 452);
            openFullLogButton.Name = "openFullLogButton";
            openFullLogButton.Size = new Size(410, 30);
            openFullLogButton.TabIndex = 10;
            openFullLogButton.Text = "Open Full Log";
            openFullLogButton.UseVisualStyleBackColor = true;
            openFullLogButton.Click += openFullLogButton_Click;
            // 
            // ManageScriptsForm
            // 
            AutoScaleDimensions = new SizeF(8F, 20F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(820, 520);
            Controls.Add(openFullLogButton);
            Controls.Add(openLibraryRootButton);
            Controls.Add(openScriptFolderButton);
            Controls.Add(dryRunCheckBox);
            Controls.Add(logPreviewTextBox);
            Controls.Add(logPreviewLabel);
            Controls.Add(loadLogButton);
            Controls.Add(scriptStatusLabel);
            Controls.Add(importFromGithubButton);
            Controls.Add(refreshScriptsButton);
            Controls.Add(stopScriptButton);
            Controls.Add(runScriptButton);
            Controls.Add(scriptListBox);
            Name = "ManageScriptsForm";
            Text = "Manage Scripts";
            Load += ManageScriptsForm_Load;
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private ListBox scriptListBox;
        private Button runScriptButton;
        private Button stopScriptButton;
        private Button refreshScriptsButton;
        private Label scriptStatusLabel;
        private Button loadLogButton;
        private Label logPreviewLabel;
        private TextBox logPreviewTextBox;
        private CheckBox dryRunCheckBox;
        private Button openScriptFolderButton;
        private Button openFullLogButton;
        private Button openLibraryRootButton;
        private Button importFromGithubButton;
    }
}
