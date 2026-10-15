namespace VoidSniffer
{
    partial class MainForm
    {
        /// <summary>Required designer variable.</summary>
        private System.ComponentModel.IContainer components = null;

        private System.Windows.Forms.ToolStrip toolStrip;
        private System.Windows.Forms.ToolStripLabel pathLabel;
        private System.Windows.Forms.ToolStripTextBox pathTextBox;
        private System.Windows.Forms.ToolStripButton scanButton;
        private System.Windows.Forms.ToolStripSeparator separator1;
        private System.Windows.Forms.ToolStripButton backButton;
        private System.Windows.Forms.ToolStripButton forwardButton;
        private System.Windows.Forms.ToolStripButton upButton;
        private System.Windows.Forms.ToolStripButton homeButton;
        private System.Windows.Forms.ToolStrip filterStrip;
        private System.Windows.Forms.ToolStripLabel filterLabel;
        private System.Windows.Forms.ToolStripTextBox nameFilterTextBox;
        private System.Windows.Forms.ToolStripLabel minSizeLabel;
        private System.Windows.Forms.ToolStripTextBox minSizeTextBox;
        private System.Windows.Forms.ToolStripLabel maxSizeLabel;
        private System.Windows.Forms.ToolStripTextBox maxSizeTextBox;
        private System.Windows.Forms.ToolStripButton applyFilterButton;
        private System.Windows.Forms.ToolStripButton resetFilterButton;
        private System.Windows.Forms.ToolStripStatusLabel statusLabel;
        private System.Windows.Forms.ToolStripProgressBar progressBar;
        private System.Windows.Forms.StatusStrip statusStrip;
        private Treemap.TreemapControl treemap;

        /// <summary>Clean up any resources being used.</summary>
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
            this.components = new System.ComponentModel.Container();
            this.toolStrip = new System.Windows.Forms.ToolStrip();
            this.pathLabel = new System.Windows.Forms.ToolStripLabel();
            this.pathTextBox = new System.Windows.Forms.ToolStripTextBox();
            this.scanButton = new System.Windows.Forms.ToolStripButton();
            this.separator1 = new System.Windows.Forms.ToolStripSeparator();
            this.backButton = new System.Windows.Forms.ToolStripButton();
            this.forwardButton = new System.Windows.Forms.ToolStripButton();
            this.upButton = new System.Windows.Forms.ToolStripButton();
            this.homeButton = new System.Windows.Forms.ToolStripButton();
            this.filterStrip = new System.Windows.Forms.ToolStrip();
            this.filterLabel = new System.Windows.Forms.ToolStripLabel();
            this.nameFilterTextBox = new System.Windows.Forms.ToolStripTextBox();
            this.minSizeLabel = new System.Windows.Forms.ToolStripLabel();
            this.minSizeTextBox = new System.Windows.Forms.ToolStripTextBox();
            this.maxSizeLabel = new System.Windows.Forms.ToolStripLabel();
            this.maxSizeTextBox = new System.Windows.Forms.ToolStripTextBox();
            this.applyFilterButton = new System.Windows.Forms.ToolStripButton();
            this.resetFilterButton = new System.Windows.Forms.ToolStripButton();
            this.statusStrip = new System.Windows.Forms.StatusStrip();
            this.statusLabel = new System.Windows.Forms.ToolStripStatusLabel();
            this.progressBar = new System.Windows.Forms.ToolStripProgressBar();
            this.treemap = new VoidSniffer.Treemap.TreemapControl();
            this.toolStrip.SuspendLayout();
            this.filterStrip.SuspendLayout();
            this.statusStrip.SuspendLayout();
            this.SuspendLayout();
            // 
            // toolStrip
            // 
            this.toolStrip.ImageScalingSize = new System.Drawing.Size(20, 20);
            this.toolStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
                this.pathLabel,
                this.pathTextBox,
                this.scanButton,
                this.separator1,
                this.backButton,
                this.forwardButton,
                this.upButton,
                this.homeButton});
            this.toolStrip.Location = new System.Drawing.Point(0, 0);
            this.toolStrip.Name = "toolStrip";
            this.toolStrip.Size = new System.Drawing.Size(960, 27);
            this.toolStrip.TabIndex = 0;
            // 
            // pathLabel
            // 
            this.pathLabel.Name = "pathLabel";
            this.pathLabel.Size = new System.Drawing.Size(40, 24);
            this.pathLabel.Text = "Путь:";
            // 
            // pathTextBox
            // 
            this.pathTextBox.Name = "pathTextBox";
            this.pathTextBox.Size = new System.Drawing.Size(320, 27);
            this.pathTextBox.Text = "C:\\";
            // 
            // scanButton
            // 
            this.scanButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.scanButton.Name = "scanButton";
            this.scanButton.Size = new System.Drawing.Size(90, 24);
            this.scanButton.Text = "Сканировать";
            // 
            // separator1
            // 
            this.separator1.Name = "separator1";
            this.separator1.Size = new System.Drawing.Size(6, 27);
            // 
            // backButton
            // 
            this.backButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.backButton.Enabled = false;
            this.backButton.Name = "backButton";
            this.backButton.Size = new System.Drawing.Size(62, 24);
            this.backButton.Text = "Назад";
            // 
            // forwardButton
            // 
            this.forwardButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.forwardButton.Enabled = false;
            this.forwardButton.Name = "forwardButton";
            this.forwardButton.Size = new System.Drawing.Size(64, 24);
            this.forwardButton.Text = "Вперёд";
            // 
            // upButton
            // 
            this.upButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.upButton.Enabled = false;
            this.upButton.Name = "upButton";
            this.upButton.Size = new System.Drawing.Size(58, 24);
            this.upButton.Text = "Вверх";
            // 
            // homeButton
            // 
            this.homeButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.homeButton.Enabled = false;
            this.homeButton.Name = "homeButton";
            this.homeButton.Size = new System.Drawing.Size(70, 24);
            this.homeButton.Text = "К корню";
            // 
            // filterStrip
            // 
            this.filterStrip.ImageScalingSize = new System.Drawing.Size(20, 20);
            this.filterStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
                this.filterLabel,
                this.nameFilterTextBox,
                this.minSizeLabel,
                this.minSizeTextBox,
                this.maxSizeLabel,
                this.maxSizeTextBox,
                this.applyFilterButton,
                this.resetFilterButton});
            this.filterStrip.Location = new System.Drawing.Point(0, 27);
            this.filterStrip.Name = "filterStrip";
            this.filterStrip.Size = new System.Drawing.Size(960, 27);
            this.filterStrip.TabIndex = 1;
            // 
            // filterLabel
            // 
            this.filterLabel.Name = "filterLabel";
            this.filterLabel.Size = new System.Drawing.Size(52, 24);
            this.filterLabel.Text = "Фильтр:";
            // 
            // nameFilterTextBox
            // 
            this.nameFilterTextBox.Name = "nameFilterTextBox";
            this.nameFilterTextBox.Size = new System.Drawing.Size(200, 27);
            // 
            // minSizeLabel
            // 
            this.minSizeLabel.Name = "minSizeLabel";
            this.minSizeLabel.Size = new System.Drawing.Size(64, 24);
            this.minSizeLabel.Text = "Мин, МБ:";
            // 
            // minSizeTextBox
            // 
            this.minSizeTextBox.Name = "minSizeTextBox";
            this.minSizeTextBox.Size = new System.Drawing.Size(70, 27);
            // 
            // maxSizeLabel
            // 
            this.maxSizeLabel.Name = "maxSizeLabel";
            this.maxSizeLabel.Size = new System.Drawing.Size(68, 24);
            this.maxSizeLabel.Text = "Макс, МБ:";
            // 
            // maxSizeTextBox
            // 
            this.maxSizeTextBox.Name = "maxSizeTextBox";
            this.maxSizeTextBox.Size = new System.Drawing.Size(70, 27);
            // 
            // applyFilterButton
            // 
            this.applyFilterButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.applyFilterButton.Name = "applyFilterButton";
            this.applyFilterButton.Size = new System.Drawing.Size(84, 24);
            this.applyFilterButton.Text = "Применить";
            // 
            // resetFilterButton
            // 
            this.resetFilterButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.resetFilterButton.Name = "resetFilterButton";
            this.resetFilterButton.Size = new System.Drawing.Size(76, 24);
            this.resetFilterButton.Text = "Сбросить";
            // 
            // statusStrip
            // 
            this.statusStrip.ImageScalingSize = new System.Drawing.Size(20, 20);
            this.statusStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
                this.statusLabel,
                this.progressBar});
            this.statusStrip.Location = new System.Drawing.Point(0, 517);
            this.statusStrip.Name = "statusStrip";
            this.statusStrip.Size = new System.Drawing.Size(960, 26);
            this.statusStrip.TabIndex = 3;
            // 
            // statusLabel
            // 
            this.statusLabel.Name = "statusLabel";
            this.statusLabel.Size = new System.Drawing.Size(50, 20);
            this.statusLabel.Text = "Готово";
            this.statusLabel.Spring = true;
            this.statusLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // progressBar
            // 
            this.progressBar.Name = "progressBar";
            this.progressBar.Size = new System.Drawing.Size(120, 20);
            this.progressBar.Style = System.Windows.Forms.ProgressBarStyle.Marquee;
            this.progressBar.Visible = false;
            // 
            // treemap
            // 
            this.treemap.Dock = System.Windows.Forms.DockStyle.Fill;
            this.treemap.Location = new System.Drawing.Point(0, 54);
            this.treemap.Name = "treemap";
            this.treemap.Size = new System.Drawing.Size(960, 463);
            this.treemap.TabIndex = 2;
            // 
            // MainForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(960, 543);
            this.Controls.Add(this.treemap);
            this.Controls.Add(this.filterStrip);
            this.Controls.Add(this.toolStrip);
            this.Controls.Add(this.statusStrip);
            this.MinimumSize = new System.Drawing.Size(720, 420);
            this.Name = "MainForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "VoidSniffer";
            this.toolStrip.ResumeLayout(false);
            this.toolStrip.PerformLayout();
            this.filterStrip.ResumeLayout(false);
            this.filterStrip.PerformLayout();
            this.statusStrip.ResumeLayout(false);
            this.statusStrip.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        #endregion
    }
}
