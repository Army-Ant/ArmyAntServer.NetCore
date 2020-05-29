using System;
using System.Threading;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;

namespace ArmyAntServer_TestClient_CSharp
{
    class Network
    {
        public delegate bool OnReadCallback(int serials, int type, long appid, int messageCode, int conversationCode, int conversationStepIndex, byte[] data);
        public Network(OnReadCallback cb)
        {
            tcp = null;
            web = null;
            readCanceller = new CancellationTokenSource();
            socketCanceller = new CancellationTokenSource();
            buffer = new Queue<byte>();
            onReadCallback = cb;
            readTask = null;
        }

        public async Task<bool> ConnectToServer(IPAddress address, int port, bool isWeb)
        {
            try
            {
                if (isWeb)
                {
                    web = new ClientWebSocket();
                    var uriStr = "ws://" + address.ToString() + ":" + port.ToString() + "/";
                    var uri = new Uri(uriStr);
                    socketCanceller = new CancellationTokenSource();
                    await web.ConnectAsync(uri, socketCanceller.Token);
                }
                else
                {
                    tcp = new TcpClient();
                    await tcp.ConnectAsync(address, port);
                }
            }
            catch (SocketException e)
            {
                return false;
            }
            catch (WebSocketException e)
            {
                return false;
            }
            readTask = OnRead();
            return true;
        }

        public async Task DisconnectServer()
        {
            try
            {
                readCanceller.Cancel(true);
                if (web != null)
                {
                    await web.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client disconnect", socketCanceller.Token);
                }
                if (tcp != null)
                {
                    tcp.Close();
                }
            }
            catch (WebSocketException e)
            {
                var txt = e.Message;
            }
            finally
            {
                tcp = null;
                web = null;
                if (readTask != null && readTask.Status == TaskStatus.Running)
                    await readTask;
                readTask = null;
                if (!socketCanceller.IsCancellationRequested)
                    socketCanceller.Cancel();
            }
        } 

        public async Task SendToServerAsync(int serials, int type /* = 1 */, long appid, int messageCode, int conversationCode, int conversationStepIndex, ArmyAntMessage.System.ConversationStepType convType, byte[] content)
        {
            var extendHead = new ArmyAntMessage.System.SocketExtendNormal_V0_0_0_1();
            extendHead.ContentLength = content.Length;
            extendHead.AppId = appid;
            extendHead.MessageCode = messageCode;
            extendHead.ConversationCode = conversationCode;
            extendHead.ConversationStepIndex = conversationStepIndex;
            extendHead.ConversationStepType = convType;
            var extendBytes = new byte[extendHead.CalculateSize()];
            extendHead.WriteTo(new Google.Protobuf.CodedOutputStream(extendBytes));
            var baseHead = new BaseHead(serials, type, 1, extendBytes.Length);

            var retList = new List<byte>();
            retList.AddRange(baseHead.Bytes);
            retList.AddRange(extendBytes);
            retList.AddRange(content);
            var sendBytes = retList.ToArray();

            if(tcp != null)
                await tcp.GetStream().WriteAsync(sendBytes, 0, sendBytes.Length);
            if (web != null)
                await web.SendAsync(new ArraySegment<byte>(sendBytes), WebSocketMessageType.Binary, true, socketCanceller.Token);
        }

        private async Task OnRead()
        {
            readCanceller = new CancellationTokenSource();
            while (!readCanceller.IsCancellationRequested)
            {
                var byteBuffer = new byte[8192];
                int len = 0;
                if (tcp != null)
                    len = await tcp.GetStream().ReadAsync(byteBuffer, 0, 8192, readCanceller.Token);
                else if (web != null)
                {
                    try
                    {
                        var result = await web.ReceiveAsync(new ArraySegment<byte>(byteBuffer), readCanceller.Token);
                        len = result.Count;
                    }
                    catch (WebSocketException e)
                    {
                        var msg = e.Message;
                        msg = msg + "";
                    }
                }
                if (len <= 0)
                    continue;
                var tarBuffer = new byte[len];
                Array.Copy(byteBuffer, 0, tarBuffer, 0, len);
                for (var i = 0; i < len; ++i)
                    buffer.Enqueue(tarBuffer[i]);
                var headBuffer = new byte[16];
                for (var i = 0; i < 16; ++i)
                    headBuffer[i] = buffer.Dequeue();
                var baseHead = new BaseHead();
                baseHead.Bytes = headBuffer;
                var extendBuffer = new byte[baseHead.extendLength];
                for (var i = 0; i < baseHead.extendLength; ++i)
                    extendBuffer[i] = buffer.Dequeue();
                var extend = ArmyAntMessage.System.SocketExtendNormal_V0_0_0_1.Parser.ParseFrom(extendBuffer);
                var contentBuffer = new Byte[extend.ContentLength];
                for (var i = 0; i < extend.ContentLength; ++i)
                    contentBuffer[i] = buffer.Dequeue();
                bool ret = onReadCallback(baseHead.serials, baseHead.type, extend.AppId, extend.MessageCode, extend.ConversationCode, extend.ConversationStepIndex, contentBuffer);
                buffer.Clear();
                if (!ret)
                    await DisconnectServer();
            }
        }

        private TcpClient tcp;
        private ClientWebSocket web;
        private CancellationTokenSource readCanceller;
        private CancellationTokenSource socketCanceller;
        private Queue<byte> buffer;
        private OnReadCallback onReadCallback;
        private Task readTask;

        private struct BaseHead
        {
            internal int serials;
            internal int type;
            internal int extendVersion;
            internal int extendLength;

            internal BaseHead(int s, int t, int v, int l)
            {
                serials = s;
                type = t;
                extendVersion = v;
                extendLength = l;
            }

            internal byte[] Bytes {
                get {
                    var retList = new List<byte>();
                    retList.AddRange(BitConverter.GetBytes(serials));
                    retList.AddRange(BitConverter.GetBytes(type));
                    retList.AddRange(BitConverter.GetBytes(extendVersion));
                    retList.AddRange(BitConverter.GetBytes(extendLength));
                    return retList.ToArray();
                }
                set {
                    serials = BitConverter.ToInt32(value, 0);
                    type = BitConverter.ToInt32(value, 4);
                    extendVersion = BitConverter.ToInt32(value, 8);
                    extendLength = BitConverter.ToInt32(value, 12);
                }
            }
        }
    }
}
