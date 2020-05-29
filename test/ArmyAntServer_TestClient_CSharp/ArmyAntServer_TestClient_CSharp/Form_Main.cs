using System;
using System.Text;
using System.Net;
using System.Windows.Forms;

namespace ArmyAntServer_TestClient_CSharp
{
    public partial class Form_Main : Form
    {
        public const long appid = 1001;
        public const int optionNumber = 50001;

        public readonly int loginRequestMessageCode;
        public readonly int logoutRequestMessageCode;
        public readonly int sendRequestMessageCode;
        public readonly int broadcastRequestMessageCode;

        public readonly int loginResponseMessageCode;
        public readonly int logoutResponseMessageCode;
        public readonly int errorResponseMessageCode;
        public readonly int broadcastResponseMessageCode;
        public readonly int sendResponseMessageCode;
        public readonly int receiveMessageCode;
        public Form_Main()
        {
            InitializeComponent();
            net = new Network(onReceiveCallback);

            loginRequestMessageCode = ArmyAntMessage.SubApps.C2SM_EchoLoginRequest.Descriptor.GetOptions().GetExtension(BaseExtensions.MsgCode);
            logoutRequestMessageCode = ArmyAntMessage.SubApps.C2SM_EchoLogoutRequest.Descriptor.GetOptions().GetExtension(BaseExtensions.MsgCode);
            sendRequestMessageCode = ArmyAntMessage.SubApps.C2SM_EchoSendRequest.Descriptor.GetOptions().GetExtension(BaseExtensions.MsgCode);
            broadcastRequestMessageCode = ArmyAntMessage.SubApps.C2SM_EchoBroadcastRequest.Descriptor.GetOptions().GetExtension(BaseExtensions.MsgCode);

            loginResponseMessageCode = ArmyAntMessage.SubApps.SM2C_EchoLoginResponse.Descriptor.GetOptions().GetExtension(BaseExtensions.MsgCode);
            logoutResponseMessageCode = ArmyAntMessage.SubApps.SM2C_EchoLogoutResponse.Descriptor.GetOptions().GetExtension(BaseExtensions.MsgCode);
            errorResponseMessageCode = ArmyAntMessage.SubApps.SM2C_EchoError.Descriptor.GetOptions().GetExtension(BaseExtensions.MsgCode);
            broadcastResponseMessageCode = ArmyAntMessage.SubApps.SM2C_EchoBroadcastResponse.Descriptor.GetOptions().GetExtension(BaseExtensions.MsgCode);
            sendResponseMessageCode = ArmyAntMessage.SubApps.SM2C_EchoSendResponse.Descriptor.GetOptions().GetExtension(BaseExtensions.MsgCode);
            receiveMessageCode = ArmyAntMessage.SubApps.SM2C_EchoReceiveNotice.Descriptor.GetOptions().GetExtension(BaseExtensions.MsgCode);
        }

        private bool onReceiveCallback(int serials, int type, long appid, int messageCode, int conversationCode, int conversationStepIndex, byte[] data)
        {
            if (messageCode == loginResponseMessageCode)
            {
                var msg = ArmyAntMessage.SubApps.SM2C_EchoLoginResponse.Parser.ParseFrom(data);
                if (msg.Result == 0)
                {
                    logged = true;
                    receiveTextBox.Text += "[" + DateTime.Now.ToLongTimeString() + "] 登录成功!" + Environment.NewLine;
                    btnLoginout.Text = "退出登录";
                }
                else
                {
                    receiveTextBox.Text += "[" + DateTime.Now.ToLongTimeString() + "] 登录失败! 消息" + msg.Message + Environment.NewLine;
                    loginNameTextBox.Enabled = true;
                }
                btnLoginout.Enabled = true;
            }
            else if (messageCode == logoutResponseMessageCode)
            {
                var msg = ArmyAntMessage.SubApps.SM2C_EchoLogoutResponse.Parser.ParseFrom(data);
                if (msg.Result == 0)
                {
                    logged = false;
                    receiveTextBox.Text += "[" + DateTime.Now.ToLongTimeString() + "] 退出登录成功!" + Environment.NewLine;
                    btnLoginout.Text = "登录EchoServer";
                    loginNameTextBox.Enabled = true;
                }
                else
                {
                    receiveTextBox.Text += "[" + DateTime.Now.ToLongTimeString() + "] 退出登录失败! 消息" + msg.Message + Environment.NewLine;
                }
                btnLoginout.Enabled = true;
            }
            else if (messageCode == errorResponseMessageCode)
            {
                var msg = ArmyAntMessage.SubApps.SM2C_EchoError.Parser.ParseFrom(data);
                receiveTextBox.Text += "[" + DateTime.Now.ToLongTimeString() + "] 发生错误! 消息" + msg.Message + Environment.NewLine;
            }
            else if (messageCode == broadcastResponseMessageCode)
            {
                var msg = ArmyAntMessage.SubApps.SM2C_EchoBroadcastResponse.Parser.ParseFrom(data);
                if (msg.Result == 0)
                {
                    receiveTextBox.Text += "[" + DateTime.Now.ToLongTimeString() + "] 发送广播成功!" + Environment.NewLine;
                }
                else
                {
                    receiveTextBox.Text += "[" + DateTime.Now.ToLongTimeString() + "] 发送广播失败! 消息" + msg.Message + Environment.NewLine;
                }
            }
            else if (messageCode == sendResponseMessageCode)
            {
                var msg = ArmyAntMessage.SubApps.SM2C_EchoSendResponse.Parser.ParseFrom(data);
                if (msg.Result == 0)
                {
                    receiveTextBox.Text += "[" + DateTime.Now.ToLongTimeString() + "] 向用户[" + msg.Request.Target + "]发送消息成功! 消息内容: " + msg.Request.Message + Environment.NewLine;
                }
                else
                {
                    receiveTextBox.Text += "[" + DateTime.Now.ToLongTimeString() + "] 向用户[" + msg.Request.Target + "]发送消息失败! 消息内容: [" + msg.Request.Message + "], 错误信息: " + msg.Message + Environment.NewLine;
                }
            }
            else if (messageCode == receiveMessageCode)
            {
                var msg = ArmyAntMessage.SubApps.SM2C_EchoReceiveNotice.Parser.ParseFrom(data);
                if (msg.IsBroadcast)
                {
                    receiveTextBox.Text += "[" + DateTime.Now.ToLongTimeString() + "] [" + msg.From + "] [广播] " + msg.Message + Environment.NewLine;
                }
                else
                {
                    receiveTextBox.Text += "[" + DateTime.Now.ToLongTimeString() + "] [" + msg.From + "] " + msg.Message + Environment.NewLine;
                }
            }
            else
            {
                receiveTextBox.Text += "[" + DateTime.Now.ToLongTimeString() + "] 收到服务器未知数据:" + Environment.NewLine;
                receiveTextBox.Text += Encoding.Default.GetString(data) + Environment.NewLine + Environment.NewLine;
            }
            return true;
        }

        private void Form_onLoad(object sender, EventArgs e)
        {
            btnLoginout.Enabled = false;
            btnBroadcast.Enabled = false;
            btnSend.Enabled = false;
        }

        private async void Form_Main_FormClosed(object sender, FormClosedEventArgs e)
        {
            await net.DisconnectServer();
        }

        private void loginNameTextBox_TextChanged(object sender, EventArgs e)
        {
            if (connected)
            {
                var haveText = loginNameTextBox.Text.Length > 0;
                btnLoginout.Enabled = haveText;
            }
        }

        private void sendTextBox_TextChanged(object sender, EventArgs e)
        {
            if (logged)
            {
                var haveText = sendTextBox.Text.Length > 0;
                var haveTarget = targetUserTextBox.Text.Length > 0;
                btnSend.Enabled = logged && haveText && haveTarget;
                btnBroadcast.Enabled = logged && haveText;
            }
        }

        private void targetUserTextBox_TextChanged(object sender, EventArgs e)
        {
            if (logged)
            {
                var haveText = sendTextBox.Text.Length > 0;
                var haveTarget = targetUserTextBox.Text.Length > 0;
                btnSend.Enabled = logged && haveText && haveTarget;
                btnBroadcast.Enabled = logged && haveText;
            }
        }

        private async void btnConnectinout_Click(object sender, EventArgs e)
        {
            if (connected)
            {
                await net.DisconnectServer();
                btnConnectinout.Text = "连接服务器";
                connected = false;
                btnLoginout.Text = "登录EchoServer";
                logged = false;
                btnLoginout.Enabled = false;
                btnBroadcast.Enabled = false;
                btnSend.Enabled = false;
                MessageBox.Show(this, "连接已断开", "断开连接", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
                receiveTextBox.Text += "[" + DateTime.Now.ToLongTimeString() + "] 已断开服务器" + Environment.NewLine;
            }
            else
            {
                connected = await net.ConnectToServer(IPAddress.Loopback, cbWebsocket.Checked ? 8080 : 14774, cbWebsocket.Checked);
                if (connected)
                {
                    btnConnectinout.Text = "断开连接";
                    btnLoginout.Enabled = true;
                    loginNameTextBox_TextChanged(sender, e);
                    sendTextBox_TextChanged(sender, e);
                    MessageBox.Show(this, "连接成功", "连接服务器", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
                    receiveTextBox.Text += "[" + DateTime.Now.ToLongTimeString() + "] 已连接服务器" + Environment.NewLine;
                }
                else
                {
                    MessageBox.Show(this, "连接失败", "连接服务器", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    receiveTextBox.Text += "[" + DateTime.Now.ToLongTimeString() + "] 连接服务器失败" + Environment.NewLine;
                }
            }
        }

        private async void btnSend_ClickAsync(object sender, EventArgs e)
        {
            var request = new ArmyAntMessage.SubApps.C2SM_EchoSendRequest();
            request.Target = targetUserTextBox.Text;
            request.Message = sendTextBox.Text;

            var ms = new System.IO.MemoryStream();
            var cos = new Google.Protobuf.CodedOutputStream(ms);
            request.WriteTo(cos);
            cos.Flush();
            await net.SendToServerAsync(0, 1, appid, sendRequestMessageCode, 3, 0, ArmyAntMessage.System.ConversationStepType.AskFor, ms.ToArray());
        }

        private async void btnBroadcast_Click(object sender, EventArgs e)
        {
            var request = new ArmyAntMessage.SubApps.C2SM_EchoBroadcastRequest();
            request.Message = sendTextBox.Text;

            var ms = new System.IO.MemoryStream();
            var cos = new Google.Protobuf.CodedOutputStream(ms);
            request.WriteTo(cos);
            cos.Flush();
            await net.SendToServerAsync(0, 1, appid, broadcastRequestMessageCode, 4, 0, ArmyAntMessage.System.ConversationStepType.AskFor, ms.ToArray());
        }

        private async void btnLoginout_Click(object sender, EventArgs e)
        {
            if (logged)
            {
                var request = new ArmyAntMessage.SubApps.C2SM_EchoLogoutRequest();
                request.UserName = loginNameTextBox.Text;

                var ms = new System.IO.MemoryStream();
                var cos = new Google.Protobuf.CodedOutputStream(ms);
                request.WriteTo(cos);
                cos.Flush();
                await net.SendToServerAsync(0, 1, appid, logoutRequestMessageCode, 2, 0, ArmyAntMessage.System.ConversationStepType.AskFor, ms.ToArray());
            }
            else
            {
                var request = new ArmyAntMessage.SubApps.C2SM_EchoLoginRequest();
                request.UserName = loginNameTextBox.Text;

                var ms = new System.IO.MemoryStream();
                var cos = new Google.Protobuf.CodedOutputStream(ms);
                request.WriteTo(cos);
                cos.Flush();
                await net.SendToServerAsync(0, 1, appid, loginRequestMessageCode, 1, 0, ArmyAntMessage.System.ConversationStepType.AskFor, ms.ToArray());
                loginNameTextBox.Enabled = false;
                btnLoginout.Enabled = false;
            }
        }

        private Network net = null;
        private bool connected = false;
        private bool logged = false;
    }
}
