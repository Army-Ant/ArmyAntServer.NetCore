namespace ArmyAntServer_TestClient_CSharp
{
    partial class Form_Main
    {
        /// <summary>
        /// 必需的设计器变量。
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// 清理所有正在使用的资源。
        /// </summary>
        /// <param name="disposing">如果应释放托管资源，为 true；否则为 false。</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows 窗体设计器生成的代码

        /// <summary>
        /// 设计器支持所需的方法 - 不要修改
        /// 使用代码编辑器修改此方法的内容。
        /// </summary>
        private void InitializeComponent()
        {
            this.receiveTextBox = new System.Windows.Forms.TextBox();
            this.sendTextBox = new System.Windows.Forms.TextBox();
            this.btnConnectinout = new System.Windows.Forms.Button();
            this.btnLoginout = new System.Windows.Forms.Button();
            this.btnSend = new System.Windows.Forms.Button();
            this.loginNameTextBox = new System.Windows.Forms.TextBox();
            this.btnBroadcast = new System.Windows.Forms.Button();
            this.targetUserTextBox = new System.Windows.Forms.TextBox();
            this.statusStrip = new System.Windows.Forms.StatusStrip();
            this.toolStripStatus_Label = new System.Windows.Forms.ToolStripStatusLabel();
            this.cbWebsocket = new System.Windows.Forms.CheckBox();
            this.statusStrip.SuspendLayout();
            this.SuspendLayout();
            // 
            // receiveTextBox
            // 
            this.receiveTextBox.AcceptsReturn = true;
            this.receiveTextBox.AcceptsTab = true;
            this.receiveTextBox.Location = new System.Drawing.Point(12, 39);
            this.receiveTextBox.Multiline = true;
            this.receiveTextBox.Name = "receiveTextBox";
            this.receiveTextBox.ReadOnly = true;
            this.receiveTextBox.ScrollBars = System.Windows.Forms.ScrollBars.Horizontal;
            this.receiveTextBox.Size = new System.Drawing.Size(617, 279);
            this.receiveTextBox.TabIndex = 0;
            // 
            // sendTextBox
            // 
            this.sendTextBox.AcceptsReturn = true;
            this.sendTextBox.AcceptsTab = true;
            this.sendTextBox.Location = new System.Drawing.Point(12, 351);
            this.sendTextBox.Multiline = true;
            this.sendTextBox.Name = "sendTextBox";
            this.sendTextBox.Size = new System.Drawing.Size(617, 87);
            this.sendTextBox.TabIndex = 1;
            this.sendTextBox.TextChanged += new System.EventHandler(this.sendTextBox_TextChanged);
            // 
            // btnConnectinout
            // 
            this.btnConnectinout.Location = new System.Drawing.Point(635, 36);
            this.btnConnectinout.Name = "btnConnectinout";
            this.btnConnectinout.Size = new System.Drawing.Size(153, 34);
            this.btnConnectinout.TabIndex = 2;
            this.btnConnectinout.Text = "连接服务器";
            this.btnConnectinout.UseVisualStyleBackColor = true;
            this.btnConnectinout.Click += new System.EventHandler(this.btnConnectinout_Click);
            // 
            // btnLoginout
            // 
            this.btnLoginout.Location = new System.Drawing.Point(635, 76);
            this.btnLoginout.Name = "btnLoginout";
            this.btnLoginout.Size = new System.Drawing.Size(153, 34);
            this.btnLoginout.TabIndex = 3;
            this.btnLoginout.Text = "登录EchoServer";
            this.btnLoginout.UseVisualStyleBackColor = true;
            this.btnLoginout.Click += new System.EventHandler(this.btnLoginout_Click);
            // 
            // btnSend
            // 
            this.btnSend.Location = new System.Drawing.Point(635, 404);
            this.btnSend.Name = "btnSend";
            this.btnSend.Size = new System.Drawing.Size(153, 34);
            this.btnSend.TabIndex = 4;
            this.btnSend.Text = "发送给指定用户";
            this.btnSend.UseVisualStyleBackColor = true;
            this.btnSend.Click += new System.EventHandler(this.btnSend_ClickAsync);
            // 
            // loginNameTextBox
            // 
            this.loginNameTextBox.Location = new System.Drawing.Point(12, 12);
            this.loginNameTextBox.Name = "loginNameTextBox";
            this.loginNameTextBox.Size = new System.Drawing.Size(617, 21);
            this.loginNameTextBox.TabIndex = 5;
            this.loginNameTextBox.TextChanged += new System.EventHandler(this.loginNameTextBox_TextChanged);
            // 
            // btnBroadcast
            // 
            this.btnBroadcast.Location = new System.Drawing.Point(635, 364);
            this.btnBroadcast.Name = "btnBroadcast";
            this.btnBroadcast.Size = new System.Drawing.Size(153, 34);
            this.btnBroadcast.TabIndex = 6;
            this.btnBroadcast.Text = "发送群体消息";
            this.btnBroadcast.UseVisualStyleBackColor = true;
            this.btnBroadcast.Click += new System.EventHandler(this.btnBroadcast_Click);
            // 
            // targetUserTextBox
            // 
            this.targetUserTextBox.Location = new System.Drawing.Point(12, 324);
            this.targetUserTextBox.Name = "targetUserTextBox";
            this.targetUserTextBox.Size = new System.Drawing.Size(617, 21);
            this.targetUserTextBox.TabIndex = 7;
            this.targetUserTextBox.TextChanged += new System.EventHandler(this.targetUserTextBox_TextChanged);
            // 
            // statusStrip
            // 
            this.statusStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.toolStripStatus_Label});
            this.statusStrip.Location = new System.Drawing.Point(0, 450);
            this.statusStrip.Name = "statusStrip";
            this.statusStrip.Size = new System.Drawing.Size(800, 22);
            this.statusStrip.TabIndex = 8;
            this.statusStrip.Text = "statusStrip1";
            // 
            // toolStripStatus_Label
            // 
            this.toolStripStatus_Label.Name = "toolStripStatus_Label";
            this.toolStripStatus_Label.Size = new System.Drawing.Size(124, 17);
            this.toolStripStatus_Label.Text = "就绪                       ";
            // 
            // cbWebsocket
            // 
            this.cbWebsocket.AutoSize = true;
            this.cbWebsocket.Location = new System.Drawing.Point(662, 14);
            this.cbWebsocket.Name = "cbWebsocket";
            this.cbWebsocket.Size = new System.Drawing.Size(102, 16);
            this.cbWebsocket.TabIndex = 9;
            this.cbWebsocket.Text = "使用websocket";
            this.cbWebsocket.UseVisualStyleBackColor = true;
            // 
            // Form_Main
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Center;
            this.ClientSize = new System.Drawing.Size(800, 472);
            this.Controls.Add(this.cbWebsocket);
            this.Controls.Add(this.statusStrip);
            this.Controls.Add(this.targetUserTextBox);
            this.Controls.Add(this.btnBroadcast);
            this.Controls.Add(this.loginNameTextBox);
            this.Controls.Add(this.btnSend);
            this.Controls.Add(this.btnLoginout);
            this.Controls.Add(this.btnConnectinout);
            this.Controls.Add(this.sendTextBox);
            this.Controls.Add(this.receiveTextBox);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "Form_Main";
            this.Text = "ArmyAntServer测试客户端";
            this.FormClosed += new System.Windows.Forms.FormClosedEventHandler(this.Form_Main_FormClosed);
            this.Load += new System.EventHandler(this.Form_onLoad);
            this.statusStrip.ResumeLayout(false);
            this.statusStrip.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.TextBox receiveTextBox;
        private System.Windows.Forms.TextBox sendTextBox;
        private System.Windows.Forms.Button btnConnectinout;
        private System.Windows.Forms.Button btnLoginout;
        private System.Windows.Forms.Button btnSend;
        private System.Windows.Forms.TextBox loginNameTextBox;
        private System.Windows.Forms.Button btnBroadcast;
        private System.Windows.Forms.TextBox targetUserTextBox;
        private System.Windows.Forms.StatusStrip statusStrip;
        private System.Windows.Forms.ToolStripStatusLabel toolStripStatus_Label;
        private System.Windows.Forms.CheckBox cbWebsocket;
    }
}

