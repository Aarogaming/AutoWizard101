namespace ProjectMaelstrom
{
    partial class Main
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
            panel1 = new Panel();
            syncStatusValueLabel = new Label();
            syncStatusLabel = new Label();
            smartPlayHeaderLabel = new Label();
            audioHeaderLabel = new Label();
            navPanel = new Panel();
            manageScriptsButton = new Button();
            startConfigurationBtn = new Button();
            loadHalfangBotBtn = new Button();
            loadBazaarReagentBot = new Button();
            launchWizardButton = new Button();
            miniModeButton = new Button();
            captureScreenButton = new Button();
            designManagerButton = new Button();
            openDesignFolderButton = new Button();
            panicStopButton = new Button();
            smartPlayGroup = new GroupBox();
            potionRefillButton = new Button();
            goPetPavilionButton = new Button();
            goMiniGamesButton = new Button();
            goBazaarButton = new Button();
            trainerListView = new ListView();
            trainerTaskColumn = new ColumnHeader();
            trainerStatusColumn = new ColumnHeader();
            trainerIssuesColumn = new ColumnHeader();
            childHostPanel = new Panel();
            speedPanel = new Panel();
            speedNumeric = new NumericUpDown();
            resetTuningButton = new Button();
            speedLabel = new Label();
            dashboardGroupBox = new GroupBox();
            runHistoryListBox = new ListBox();
            runHistoryLabel = new Label();
            dashboardStatusLabel = new Label();
            smartPlayStatusLabel = new Label();
            dashboardWarningsTextBox = new TextBox();
            dashboardWarningsLabel = new Label();
            dashboardStatsLabel = new Label();
            snapshotButton = new Button();
            healthLabel = new Label();
            manaLabel = new Label();
            goldLabel = new Label();
            energyLabel = new Label();
            potionsLabel = new Label();
            snapshotWarningsTextBox = new TextBox();
            snapshotWarningsLabel = new Label();
            panel1.SuspendLayout();
            navPanel.SuspendLayout();
            smartPlayGroup.SuspendLayout();
            speedPanel.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)speedNumeric).BeginInit();
            dashboardGroupBox.SuspendLayout();
            SuspendLayout();
            // 
            // panel1
            // 
            panel1.Controls.Add(syncStatusValueLabel);
            panel1.Controls.Add(syncStatusLabel);
            panel1.Controls.Add(smartPlayHeaderLabel);
            panel1.Controls.Add(audioHeaderLabel);
            panel1.Dock = DockStyle.Top;
            panel1.Location = new Point(0, 0);
            panel1.Name = "panel1";
            panel1.Size = new Size(1100, 50);
            panel1.TabIndex = 0;
            // 
            // syncStatusValueLabel
            // 
            syncStatusValueLabel.AutoSize = true;
            syncStatusValueLabel.Location = new Point(60, 20);
            syncStatusValueLabel.Name = "syncStatusValueLabel";
            syncStatusValueLabel.Size = new Size(33, 20);
            syncStatusValueLabel.TabIndex = 5;
            syncStatusValueLabel.Text = "Idle";
            // 
            // syncStatusLabel
            // 
            syncStatusLabel.AutoSize = true;
            syncStatusLabel.Location = new Point(12, 20);
            syncStatusLabel.Name = "syncStatusLabel";
            syncStatusLabel.Size = new Size(39, 20);
            syncStatusLabel.TabIndex = 4;
            syncStatusLabel.Text = "Sync:";
            // 
            // navPanel
            // 
            navPanel.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left;
            navPanel.AutoScroll = true;
            navPanel.Controls.Add(manageScriptsButton);
            navPanel.Controls.Add(startConfigurationBtn);
            navPanel.Controls.Add(loadHalfangBotBtn);
            navPanel.Controls.Add(loadBazaarReagentBot);
            navPanel.Controls.Add(launchWizardButton);
            navPanel.Controls.Add(miniModeButton);
            navPanel.Controls.Add(captureScreenButton);
            navPanel.Controls.Add(designManagerButton);
            navPanel.Controls.Add(openDesignFolderButton);
            navPanel.Controls.Add(panicStopButton);
            navPanel.Controls.Add(smartPlayGroup);
            navPanel.Location = new Point(12, 70);
            navPanel.Name = "navPanel";
            navPanel.Padding = new Padding(4, 8, 4, 8);
            navPanel.Size = new Size(280, 640);
            navPanel.TabIndex = 14;
            // 
            // manageScriptsButton
            // 
            manageScriptsButton.Location = new Point(16, 12);
            manageScriptsButton.Name = "manageScriptsButton";
            manageScriptsButton.Size = new Size(248, 34);
            manageScriptsButton.TextAlign = ContentAlignment.MiddleCenter;
            manageScriptsButton.AutoEllipsis = true;
            manageScriptsButton.TabIndex = 0;
            manageScriptsButton.Text = "Manage Scripts";
            manageScriptsButton.UseVisualStyleBackColor = true;
            manageScriptsButton.Click += manageScriptsButton_Click;
            // 
            // startConfigurationBtn
            // 
            startConfigurationBtn.Location = new Point(16, 60);
            startConfigurationBtn.Name = "startConfigurationBtn";
            startConfigurationBtn.Size = new Size(248, 40);
            startConfigurationBtn.TextAlign = ContentAlignment.MiddleCenter;
            startConfigurationBtn.AutoEllipsis = true;
            startConfigurationBtn.TabIndex = 1;
            startConfigurationBtn.Text = "Combat (Halfang)";
            startConfigurationBtn.UseVisualStyleBackColor = true;
            startConfigurationBtn.Click += loadHalfangBotBtn_Click;
            // 
            // loadHalfangBotBtn
            // 
            loadHalfangBotBtn.Location = new Point(16, 110);
            loadHalfangBotBtn.Name = "loadHalfangBotBtn";
            loadHalfangBotBtn.Size = new Size(248, 40);
            loadHalfangBotBtn.TextAlign = ContentAlignment.MiddleCenter;
            loadHalfangBotBtn.AutoEllipsis = true;
            loadHalfangBotBtn.TabIndex = 3;
            loadHalfangBotBtn.Text = "Reagents (Bazaar)";
            loadHalfangBotBtn.UseVisualStyleBackColor = true;
            loadHalfangBotBtn.Click += loadBazaarReagentBot_Click;
            // 
            // loadBazaarReagentBot
            // 
            loadBazaarReagentBot.Location = new Point(16, 160);
            loadBazaarReagentBot.Name = "loadBazaarReagentBot";
            loadBazaarReagentBot.Size = new Size(248, 40);
            loadBazaarReagentBot.TextAlign = ContentAlignment.MiddleCenter;
            loadBazaarReagentBot.AutoEllipsis = true;
            loadBazaarReagentBot.TabIndex = 4;
            loadBazaarReagentBot.Text = "Pets (DanceBot)";
            loadBazaarReagentBot.UseVisualStyleBackColor = true;
            loadBazaarReagentBot.Click += runPetDanceScriptBtn_Click;
            // 
            // launchWizardButton
            // 
            launchWizardButton.Location = new Point(16, 210);
            launchWizardButton.Name = "launchWizardButton";
            launchWizardButton.Size = new Size(248, 34);
            launchWizardButton.TextAlign = ContentAlignment.MiddleCenter;
            launchWizardButton.AutoEllipsis = true;
            launchWizardButton.TabIndex = 11;
            launchWizardButton.Text = "Launch Wizard101";
            launchWizardButton.UseVisualStyleBackColor = true;
            launchWizardButton.Click += launchWizardButton_Click;
            // 
            // miniModeButton
            // 
            miniModeButton.Location = new Point(16, 250);
            miniModeButton.Name = "miniModeButton";
            miniModeButton.Size = new Size(248, 34);
            miniModeButton.TextAlign = ContentAlignment.MiddleCenter;
            miniModeButton.AutoEllipsis = true;
            miniModeButton.TabIndex = 13;
            miniModeButton.Text = "Mini Mode: On";
            miniModeButton.UseVisualStyleBackColor = true;
            miniModeButton.Click += miniModeButton_Click;
            // 
            // captureScreenButton
            // 
            captureScreenButton.Location = new Point(16, 294);
            captureScreenButton.Name = "captureScreenButton";
            captureScreenButton.Size = new Size(248, 34);
            captureScreenButton.TabIndex = 14;
            captureScreenButton.Text = "Capture Screen";
            captureScreenButton.TextAlign = ContentAlignment.MiddleCenter;
            captureScreenButton.AutoEllipsis = true;
            captureScreenButton.UseVisualStyleBackColor = true;
            captureScreenButton.Visible = false;
            captureScreenButton.Click += captureScreenButton_Click;
            // 
            // designManagerButton
            // 
            designManagerButton.Location = new Point(16, 334);
            designManagerButton.Name = "designManagerButton";
            designManagerButton.Size = new Size(248, 34);
            designManagerButton.TabIndex = 16;
            designManagerButton.Text = "Design Manager";
            designManagerButton.TextAlign = ContentAlignment.MiddleCenter;
            designManagerButton.AutoEllipsis = true;
            designManagerButton.UseVisualStyleBackColor = true;
            designManagerButton.Click += designManagerButton_Click;
            // 
            // openDesignFolderButton
            // 
            openDesignFolderButton.Location = new Point(16, 374);
            openDesignFolderButton.Name = "openDesignFolderButton";
            openDesignFolderButton.Size = new Size(248, 34);
            openDesignFolderButton.TabIndex = 17;
            openDesignFolderButton.Text = "Open Design Folder";
            openDesignFolderButton.TextAlign = ContentAlignment.MiddleCenter;
            openDesignFolderButton.AutoEllipsis = true;
            openDesignFolderButton.UseVisualStyleBackColor = true;
            openDesignFolderButton.Click += openDesignFolderButton_Click;
            // 
            // panicStopButton
            // 
            panicStopButton.Location = new Point(16, 414);
            panicStopButton.Name = "panicStopButton";
            panicStopButton.Size = new Size(248, 34);
            panicStopButton.TabIndex = 15;
            panicStopButton.Text = "Panic Stop";
            panicStopButton.TextAlign = ContentAlignment.MiddleCenter;
            panicStopButton.AutoEllipsis = true;
            panicStopButton.UseVisualStyleBackColor = true;
            panicStopButton.Click += panicStopButton_Click;
            // 
            // smartPlayGroup
            // 
            smartPlayGroup.Controls.Add(potionRefillButton);
            smartPlayGroup.Controls.Add(goPetPavilionButton);
            smartPlayGroup.Controls.Add(goMiniGamesButton);
            smartPlayGroup.Controls.Add(goBazaarButton);
            smartPlayGroup.Location = new Point(16, 454);
            smartPlayGroup.Name = "smartPlayGroup";
            smartPlayGroup.Size = new Size(248, 172);
            smartPlayGroup.TabIndex = 14;
            smartPlayGroup.TabStop = false;
            smartPlayGroup.Text = "Smart Travel";
            // 
            // potionRefillButton
            // 
            potionRefillButton.Location = new Point(12, 114);
            potionRefillButton.Name = "potionRefillButton";
            potionRefillButton.Size = new Size(224, 30);
            potionRefillButton.Font = new Font("Segoe UI", 9F, FontStyle.Bold, GraphicsUnit.Point);
            potionRefillButton.TabIndex = 3;
            potionRefillButton.Text = "Start Potion Refill Run";
            potionRefillButton.UseVisualStyleBackColor = true;
            potionRefillButton.Click += potionRefillButton_Click;
            // 
            // goPetPavilionButton
            // 
            goPetPavilionButton.Location = new Point(12, 82);
            goPetPavilionButton.Name = "goPetPavilionButton";
            goPetPavilionButton.Size = new Size(224, 30);
            goPetPavilionButton.Font = new Font("Segoe UI", 9F, FontStyle.Bold, GraphicsUnit.Point);
            goPetPavilionButton.TabIndex = 2;
            goPetPavilionButton.Text = "Go to Pet Mini Games";
            goPetPavilionButton.UseVisualStyleBackColor = true;
            goPetPavilionButton.Click += goPetPavilionButton_Click;
            // 
            // goMiniGamesButton
            // 
            goMiniGamesButton.Location = new Point(12, 50);
            goMiniGamesButton.Name = "goMiniGamesButton";
            goMiniGamesButton.Size = new Size(224, 30);
            goMiniGamesButton.Font = new Font("Segoe UI", 9F, FontStyle.Bold, GraphicsUnit.Point);
            goMiniGamesButton.TabIndex = 1;
            goMiniGamesButton.Text = "Potion Refill (Mini Games)";
            goMiniGamesButton.UseVisualStyleBackColor = true;
            goMiniGamesButton.Click += goMiniGamesButton_Click;
            // 
            // goBazaarButton
            // 
            goBazaarButton.Location = new Point(12, 18);
            goBazaarButton.Name = "goBazaarButton";
            goBazaarButton.Size = new Size(224, 30);
            goBazaarButton.Font = new Font("Segoe UI", 9F, FontStyle.Bold, GraphicsUnit.Point);
            goBazaarButton.TabIndex = 0;
            goBazaarButton.Text = "Go to Bazaar";
            goBazaarButton.UseVisualStyleBackColor = true;
            goBazaarButton.Click += goBazaarButton_Click;
            // 
            // trainerListView
            // 
            trainerListView.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            trainerListView.Columns.AddRange(new ColumnHeader[] { trainerTaskColumn, trainerStatusColumn, trainerIssuesColumn });
            trainerListView.FullRowSelect = true;
            trainerListView.GridLines = true;
            trainerListView.HeaderStyle = ColumnHeaderStyle.Nonclickable;
            trainerListView.HideSelection = false;
            trainerListView.Location = new Point(304, 110);
            trainerListView.MultiSelect = false;
            trainerListView.Name = "trainerListView";
            trainerListView.Size = new Size(796, 348);
            trainerListView.TabIndex = 15;
            trainerListView.UseCompatibleStateImageBehavior = false;
            trainerListView.View = View.Details;
            trainerListView.SelectedIndexChanged += trainerListView_SelectedIndexChanged;
            // 
            // childHostPanel
            // 
            childHostPanel.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            childHostPanel.Location = new Point(304, 110);
            childHostPanel.Name = "childHostPanel";
            childHostPanel.Size = new Size(796, 348);
            childHostPanel.TabIndex = 16;
            childHostPanel.Visible = false;
            // 
            // speedPanel
            // 
            speedPanel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            speedPanel.Controls.Add(speedNumeric);
            speedPanel.Controls.Add(resetTuningButton);
            speedPanel.Controls.Add(speedLabel);
            speedPanel.Location = new Point(304, 60);
            speedPanel.Name = "speedPanel";
            speedPanel.Size = new Size(796, 40);
            speedPanel.TabIndex = 18;
            // 
            // speedNumeric
            // 
            speedNumeric.DecimalPlaces = 1;
            speedNumeric.Increment = new decimal(new int[] { 1, 0, 0, 65536 });
            speedNumeric.Location = new Point(160, 7);
            speedNumeric.Maximum = new decimal(new int[] { 30, 0, 0, 65536 });
            speedNumeric.Minimum = new decimal(new int[] { 0, 0, 0, 0 });
            speedNumeric.Name = "speedNumeric";
            speedNumeric.Size = new Size(80, 27);
            speedNumeric.TabIndex = 1;
            speedNumeric.Value = new decimal(new int[] { 10, 0, 0, 65536 });
            // 
            // resetTuningButton
            // 
            resetTuningButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            resetTuningButton.Location = new Point(680, 6);
            resetTuningButton.Name = "resetTuningButton";
            resetTuningButton.Size = new Size(110, 29);
            resetTuningButton.TabIndex = 2;
            resetTuningButton.Text = "Reset Tuning";
            resetTuningButton.UseVisualStyleBackColor = true;
            resetTuningButton.Click += resetTuningButton_Click;
            // 
            // speedLabel
            // 
            speedLabel.AutoSize = true;
            speedLabel.Location = new Point(10, 9);
            speedLabel.Name = "speedLabel";
            speedLabel.Size = new Size(144, 20);
            speedLabel.TabIndex = 0;
            speedLabel.Text = "Speed Multiplier [x]";
            // 
            // trainerTaskColumn
            // 
            trainerTaskColumn.Text = "Task";
            trainerTaskColumn.Width = 300;
            // 
            // trainerStatusColumn
            // 
            trainerStatusColumn.Text = "Status";
            trainerStatusColumn.Width = 192;
            // 
            // trainerIssuesColumn
            // 
            trainerIssuesColumn.Text = "Issues";
            trainerIssuesColumn.Width = 192;
            // 
            // dashboardGroupBox
            // 
            dashboardGroupBox.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            dashboardGroupBox.Controls.Add(runHistoryListBox);
            dashboardGroupBox.Controls.Add(runHistoryLabel);
            dashboardGroupBox.Controls.Add(dashboardStatusLabel);
            dashboardGroupBox.Controls.Add(smartPlayStatusLabel);
            dashboardGroupBox.Controls.Add(dashboardWarningsTextBox);
            dashboardGroupBox.Controls.Add(dashboardWarningsLabel);
            dashboardGroupBox.Controls.Add(dashboardStatsLabel);
            dashboardGroupBox.Location = new Point(304, 464);
            dashboardGroupBox.Name = "dashboardGroupBox";
            dashboardGroupBox.Size = new Size(796, 234);
            dashboardGroupBox.TabIndex = 13;
            dashboardGroupBox.TabStop = false;
            dashboardGroupBox.Text = "Dashboard";
            // 
            // runHistoryListBox
            // 
            runHistoryListBox.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            runHistoryListBox.FormattingEnabled = true;
            runHistoryListBox.ItemHeight = 20;
            runHistoryListBox.Location = new Point(440, 30);
            runHistoryListBox.Name = "runHistoryListBox";
            runHistoryListBox.Size = new Size(286, 84);
            runHistoryListBox.TabIndex = 4;
            // 
            // runHistoryLabel
            // 
            runHistoryLabel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            runHistoryLabel.AutoSize = true;
            runHistoryLabel.Location = new Point(440, 10);
            runHistoryLabel.Name = "runHistoryLabel";
            runHistoryLabel.Size = new Size(82, 20);
            runHistoryLabel.TabIndex = 5;
            runHistoryLabel.Text = "Run History";
            // 
            // dashboardStatusLabel
            // 
            dashboardStatusLabel.AutoSize = true;
            dashboardStatusLabel.Location = new Point(10, 30);
            dashboardStatusLabel.Name = "dashboardStatusLabel";
            dashboardStatusLabel.Size = new Size(136, 20);
            dashboardStatusLabel.TabIndex = 3;
            dashboardStatusLabel.Text = "Status: Idle | Sync: -";
            // 
            // smartPlayStatusLabel
            // 
            smartPlayStatusLabel.AutoSize = true;
            smartPlayStatusLabel.Location = new Point(10, 52);
            smartPlayStatusLabel.Name = "smartPlayStatusLabel";
            smartPlayStatusLabel.Size = new Size(108, 20);
            smartPlayStatusLabel.TabIndex = 6;
            smartPlayStatusLabel.Text = "SmartPlay: Idle";
            // 
            // dashboardWarningsTextBox
            // 
            dashboardWarningsTextBox.Location = new Point(10, 116);
            dashboardWarningsTextBox.Multiline = true;
            dashboardWarningsTextBox.Name = "dashboardWarningsTextBox";
            dashboardWarningsTextBox.ReadOnly = true;
            dashboardWarningsTextBox.ScrollBars = ScrollBars.Vertical;
            dashboardWarningsTextBox.Size = new Size(716, 110);
            dashboardWarningsTextBox.TabIndex = 2;
            // 
            // dashboardWarningsLabel
            // 
            dashboardWarningsLabel.AutoSize = true;
            dashboardWarningsLabel.Location = new Point(10, 94);
            dashboardWarningsLabel.Name = "dashboardWarningsLabel";
            dashboardWarningsLabel.Size = new Size(68, 20);
            dashboardWarningsLabel.TabIndex = 1;
            dashboardWarningsLabel.Text = "Warnings";
            // 
            // dashboardStatsLabel
            // 
            dashboardStatsLabel.AutoSize = true;
            dashboardStatsLabel.Location = new Point(10, 58);
            dashboardStatsLabel.Name = "dashboardStatsLabel";
            dashboardStatsLabel.Size = new Size(129, 20);
            dashboardStatsLabel.TabIndex = 0;
            dashboardStatsLabel.Text = "Health: -, Mana: -";
            // 
            // snapshotButton
            // 
            snapshotButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            snapshotButton.Location = new Point(12, 706);
            snapshotButton.Name = "snapshotButton";
            snapshotButton.Size = new Size(140, 30);
            snapshotButton.TabIndex = 6;
            snapshotButton.Text = "Refresh Stats";
            snapshotButton.UseVisualStyleBackColor = true;
            snapshotButton.Click += snapshotButton_Click;
            // 
            // healthLabel
            // 
            healthLabel.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            healthLabel.AutoSize = true;
            healthLabel.Location = new Point(160, 712);
            healthLabel.Name = "healthLabel";
            healthLabel.Size = new Size(67, 20);
            healthLabel.TabIndex = 7;
            healthLabel.Text = "Health: -";
            // 
            // manaLabel
            // 
            manaLabel.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            manaLabel.AutoSize = true;
            manaLabel.Location = new Point(260, 712);
            manaLabel.Name = "manaLabel";
            manaLabel.Size = new Size(60, 20);
            manaLabel.TabIndex = 8;
            manaLabel.Text = "Mana: -";
            // 
            // goldLabel
            // 
            goldLabel.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            goldLabel.AutoSize = true;
            goldLabel.Location = new Point(360, 712);
            goldLabel.Name = "goldLabel";
            goldLabel.Size = new Size(55, 20);
            goldLabel.TabIndex = 9;
            goldLabel.Text = "Gold: -";
            // 
            // energyLabel
            // 
            energyLabel.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            energyLabel.AutoSize = true;
            energyLabel.Location = new Point(460, 712);
            energyLabel.Name = "energyLabel";
            energyLabel.Size = new Size(70, 20);
            energyLabel.TabIndex = 16;
            energyLabel.Text = "Energy: -";
            // 
            // potionsLabel
            // 
            potionsLabel.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            potionsLabel.AutoSize = true;
            potionsLabel.Location = new Point(580, 712);
            potionsLabel.Name = "potionsLabel";
            potionsLabel.Size = new Size(71, 20);
            potionsLabel.TabIndex = 17;
            potionsLabel.Text = "Potions: -";
            // 
            // snapshotWarningsTextBox
            // 
            snapshotWarningsTextBox.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            snapshotWarningsTextBox.Location = new Point(304, 706);
            snapshotWarningsTextBox.Multiline = true;
            snapshotWarningsTextBox.Name = "snapshotWarningsTextBox";
            snapshotWarningsTextBox.ReadOnly = true;
            snapshotWarningsTextBox.ScrollBars = ScrollBars.Vertical;
            snapshotWarningsTextBox.Size = new Size(796, 40);
            snapshotWarningsTextBox.TabIndex = 11;
            // 
            // snapshotWarningsLabel
            // 
            snapshotWarningsLabel.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            snapshotWarningsLabel.AutoSize = true;
            snapshotWarningsLabel.Location = new Point(304, 684);
            snapshotWarningsLabel.Name = "snapshotWarningsLabel";
            snapshotWarningsLabel.Size = new Size(124, 20);
            snapshotWarningsLabel.TabIndex = 10;
            snapshotWarningsLabel.Text = "Snapshot Notes";
            // 
            // Main
            // 
            AutoScaleDimensions = new SizeF(8F, 20F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1100, 820);
            Controls.Add(trainerListView);
            Controls.Add(childHostPanel);
            Controls.Add(speedPanel);
            Controls.Add(navPanel);
            Controls.Add(snapshotWarningsTextBox);
            Controls.Add(snapshotWarningsLabel);
            Controls.Add(potionsLabel);
            Controls.Add(energyLabel);
            Controls.Add(goldLabel);
            Controls.Add(manaLabel);
            Controls.Add(healthLabel);
            Controls.Add(snapshotButton);
            Controls.Add(dashboardGroupBox);
            Controls.Add(panel1);
            MinimumSize = new Size(1000, 760);
            Name = "Main";
            Text = "W101Trainer";
            Load += Main_Load;
            panel1.ResumeLayout(false);
            panel1.PerformLayout();
            navPanel.ResumeLayout(false);
            navPanel.PerformLayout();
            smartPlayGroup.ResumeLayout(false);
            speedPanel.ResumeLayout(false);
            speedPanel.PerformLayout();
            dashboardGroupBox.ResumeLayout(false);
            dashboardGroupBox.PerformLayout();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private Panel panel1;
        private Label syncStatusValueLabel;
        private Label syncStatusLabel;
        private Label smartPlayHeaderLabel;
        private Label audioHeaderLabel;
        private Panel navPanel;
        private Button startConfigurationBtn;
        private Button loadHalfangBotBtn;
        private Button loadBazaarReagentBot;
        private Button launchWizardButton;
        private Button miniModeButton;
        private GroupBox smartPlayGroup;
        private Button potionRefillButton;
        private Button goPetPavilionButton;
        private Button goMiniGamesButton;
        private Button goBazaarButton;
        private ListView trainerListView;
        private ColumnHeader trainerTaskColumn;
        private ColumnHeader trainerStatusColumn;
        private ColumnHeader trainerIssuesColumn;
        private GroupBox dashboardGroupBox;
        private ListBox runHistoryListBox;
        private Label runHistoryLabel;
        private Label dashboardStatusLabel;
        private TextBox dashboardWarningsTextBox;
        private Label dashboardWarningsLabel;
        private Label dashboardStatsLabel;
        private Label smartPlayStatusLabel;
        private Button snapshotButton;
        private Label healthLabel;
        private Label manaLabel;
        private Label goldLabel;
        private Label energyLabel;
        private Label potionsLabel;
        private TextBox snapshotWarningsTextBox;
        private Label snapshotWarningsLabel;
        private Panel speedPanel;
        private Label speedLabel;
        private NumericUpDown speedNumeric;
        private Panel childHostPanel;
        private Button manageScriptsButton;
        private Button captureScreenButton;
        private Button panicStopButton;
        private Button designManagerButton;
        private Button openDesignFolderButton;
        private Button resetTuningButton;
    }
}
