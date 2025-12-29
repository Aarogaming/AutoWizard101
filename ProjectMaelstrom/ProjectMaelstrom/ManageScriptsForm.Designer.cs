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
            updateScriptButton = new Button();
            removeScriptButton = new Button();
            scriptStatusLabel = new Label();
            sourceLabel = new Label();
            loadLogButton = new Button();
            logPreviewLabel = new Label();
            logPreviewTextBox = new TextBox();
            dryRunCheckBox = new CheckBox();
            openScriptFolderButton = new Button();
            openFullLogButton = new Button();
            viewSourceButton = new Button();
            authorLabel = new Label();
            openLibraryRootButton = new Button();
            filterNoteLabel = new Label();
            SuspendLayout();
            // 
            // scriptListBox
            // 
            scriptListBox.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left;
            scriptListBox.FormattingEnabled = true;
            scriptListBox.ItemHeight = 20;
            scriptListBox.Location = new Point(12, 12);
            scriptListBox.Name = "scriptListBox";
            scriptListBox.Size = new Size(520, 660);
            scriptListBox.HorizontalScrollbar = true;
            scriptListBox.TabIndex = 0;
            // 
            // runScriptButton
            // 
            runScriptButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            runScriptButton.Location = new Point(540, 12);
            runScriptButton.Name = "runScriptButton";
            runScriptButton.Size = new Size(140, 32);
            runScriptButton.TabIndex = 1;
            runScriptButton.Text = "Run";
            runScriptButton.UseVisualStyleBackColor = true;
            runScriptButton.Click += runScriptButton_Click;
            // 
            // stopScriptButton
            // 
            stopScriptButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            stopScriptButton.Location = new Point(692, 12);
            stopScriptButton.Name = "stopScriptButton";
            stopScriptButton.Size = new Size(140, 32);
            stopScriptButton.TabIndex = 2;
            stopScriptButton.Text = "Stop";
            stopScriptButton.UseVisualStyleBackColor = true;
            stopScriptButton.Click += stopScriptButton_Click;
            // 
            // refreshScriptsButton
            // 
            refreshScriptsButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            refreshScriptsButton.Location = new Point(844, 12);
            refreshScriptsButton.Name = "refreshScriptsButton";
            refreshScriptsButton.Size = new Size(170, 32);
            refreshScriptsButton.TabIndex = 3;
            refreshScriptsButton.Text = "Refresh Library";
            refreshScriptsButton.UseVisualStyleBackColor = true;
            refreshScriptsButton.Click += refreshScriptsButton_Click;
            // 
            // importFromGithubButton
            // 
            importFromGithubButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            importFromGithubButton.Location = new Point(540, 52);
            importFromGithubButton.Name = "importFromGithubButton";
            importFromGithubButton.Size = new Size(474, 28);
            importFromGithubButton.TabIndex = 4;
            importFromGithubButton.Text = "Add from GitHub";
            importFromGithubButton.UseVisualStyleBackColor = true;
            importFromGithubButton.Click += importFromGithubButton_Click;
            // 
            // updateScriptButton
            // 
            updateScriptButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            updateScriptButton.Location = new Point(540, 86);
            updateScriptButton.Name = "updateScriptButton";
            updateScriptButton.Size = new Size(140, 28);
            updateScriptButton.TabIndex = 5;
            updateScriptButton.Text = "Update";
            updateScriptButton.UseVisualStyleBackColor = true;
            updateScriptButton.Click += updateScriptButton_Click;
            // 
            // removeScriptButton
            // 
            removeScriptButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            removeScriptButton.Location = new Point(692, 86);
            removeScriptButton.Name = "removeScriptButton";
            removeScriptButton.Size = new Size(140, 28);
            removeScriptButton.TabIndex = 6;
            removeScriptButton.Text = "Remove";
            removeScriptButton.UseVisualStyleBackColor = true;
            removeScriptButton.Click += removeScriptButton_Click;
            // 
            // scriptStatusLabel
            // 
            scriptStatusLabel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            scriptStatusLabel.AutoSize = true;
            scriptStatusLabel.Location = new Point(540, 120);
            scriptStatusLabel.Name = "scriptStatusLabel";
            scriptStatusLabel.Size = new Size(88, 20);
            scriptStatusLabel.TabIndex = 4;
            scriptStatusLabel.Text = "Status: Idle";
            // 
            // sourceLabel
            // 
            sourceLabel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            sourceLabel.AutoSize = true;
            sourceLabel.Location = new Point(540, 144);
            sourceLabel.Name = "sourceLabel";
            sourceLabel.Size = new Size(69, 20);
            sourceLabel.TabIndex = 7;
            sourceLabel.Text = "Source: -";
            // 
            // loadLogButton
            // 
            loadLogButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            loadLogButton.Location = new Point(540, 168);
            loadLogButton.Name = "loadLogButton";
            loadLogButton.Size = new Size(474, 30);
            loadLogButton.TabIndex = 8;
            loadLogButton.Text = "Load Log";
            loadLogButton.UseVisualStyleBackColor = true;
            loadLogButton.Click += loadLogButton_Click;
            // 
            // logPreviewLabel
            // 
            logPreviewLabel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            logPreviewLabel.AutoSize = true;
            logPreviewLabel.Location = new Point(540, 204);
            logPreviewLabel.Name = "logPreviewLabel";
            logPreviewLabel.Size = new Size(84, 20);
            logPreviewLabel.TabIndex = 6;
            logPreviewLabel.Text = "Log Preview";
            // 
            // logPreviewTextBox
            // 
            logPreviewTextBox.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Right;
            logPreviewTextBox.Location = new Point(540, 228);
            logPreviewTextBox.Multiline = true;
            logPreviewTextBox.Name = "logPreviewTextBox";
            logPreviewTextBox.ReadOnly = true;
            logPreviewTextBox.ScrollBars = ScrollBars.Vertical;
            logPreviewTextBox.Size = new Size(500, 300);
            logPreviewTextBox.TabIndex = 9;
            // 
            // dryRunCheckBox
            // 
            dryRunCheckBox.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            dryRunCheckBox.AutoSize = true;
            dryRunCheckBox.Location = new Point(540, 540);
            dryRunCheckBox.Name = "dryRunCheckBox";
            dryRunCheckBox.Size = new Size(77, 24);
            dryRunCheckBox.TabIndex = 10;
            dryRunCheckBox.Text = "Dry run";
            dryRunCheckBox.UseVisualStyleBackColor = true;
            dryRunCheckBox.CheckedChanged += dryRunCheckBox_CheckedChanged;
            // 
            // openScriptFolderButton
            // 
            openScriptFolderButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            openScriptFolderButton.Location = new Point(540, 572);
            openScriptFolderButton.Name = "openScriptFolderButton";
            openScriptFolderButton.Size = new Size(500, 30);
            openScriptFolderButton.TabIndex = 12;
            openScriptFolderButton.Text = "Open Script Folder";
            openScriptFolderButton.UseVisualStyleBackColor = true;
            openScriptFolderButton.Click += openScriptFolderButton_Click;
            // 
            // openLibraryRootButton
            // 
            openLibraryRootButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            openLibraryRootButton.Location = new Point(540, 606);
            openLibraryRootButton.Name = "openLibraryRootButton";
            openLibraryRootButton.Size = new Size(500, 30);
            openLibraryRootButton.TabIndex = 11;
            openLibraryRootButton.Text = "Open Script Library";
            openLibraryRootButton.UseVisualStyleBackColor = true;
            openLibraryRootButton.Click += openLibraryRootButton_Click;
            // 
            // openFullLogButton
            // 
            openFullLogButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            openFullLogButton.Location = new Point(540, 680);
            openFullLogButton.Name = "openFullLogButton";
            openFullLogButton.Size = new Size(500, 30);
            openFullLogButton.TabIndex = 13;
            openFullLogButton.Text = "Open Full Log";
            openFullLogButton.UseVisualStyleBackColor = true;
            openFullLogButton.Click += openFullLogButton_Click;
            // 
            // viewSourceButton
            // 
            viewSourceButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            viewSourceButton.Location = new Point(540, 642);
            viewSourceButton.Name = "viewSourceButton";
            viewSourceButton.Size = new Size(500, 30);
            viewSourceButton.TabIndex = 14;
            viewSourceButton.Text = "View Source (GitHub)";
            viewSourceButton.UseVisualStyleBackColor = true;
            viewSourceButton.Click += viewSourceButton_Click;
            // 
            // authorLabel
            // 
            authorLabel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            authorLabel.AutoSize = true;
            authorLabel.Location = new Point(540, 168);
            authorLabel.Name = "authorLabel";
            authorLabel.Size = new Size(68, 20);
            authorLabel.TabIndex = 15;
            authorLabel.Text = "Author: -";
            // 
            // filterNoteLabel
            // 
            filterNoteLabel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            filterNoteLabel.AutoSize = true;
            filterNoteLabel.Location = new Point(540, 200);
            filterNoteLabel.Name = "filterNoteLabel";
            filterNoteLabel.Size = new Size(0, 20);
            filterNoteLabel.TabIndex = 17;
            filterNoteLabel.Text = "";
            filterNoteLabel.Visible = false;
            // 
            // ManageScriptsForm
            // 
            AutoScaleDimensions = new SizeF(8F, 20F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1100, 720);
            Controls.Add(filterNoteLabel);
            Controls.Add(sourceLabel);
            Controls.Add(authorLabel);
            Controls.Add(viewSourceButton);
            Controls.Add(removeScriptButton);
            Controls.Add(updateScriptButton);
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
        private Button viewSourceButton;
        private Button openLibraryRootButton;
        private Button importFromGithubButton;
        private Button updateScriptButton;
        private Button removeScriptButton;
        private Label sourceLabel;
        private Label authorLabel;
        private Label filterNoteLabel;
    }
}
