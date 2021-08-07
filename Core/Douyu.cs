using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Net.WebSockets;
using System.Text.RegularExpressions;
using System.Net.Http;
using System.IO;
using Newtonsoft.Json.Linq;
using System.Timers;

namespace Douyu
{
    public delegate void LiveChatMessageHandler(LiveChatMessage message);
    public record LiveChatMessage(int RoomID, string Platform, string CID, string Content, int FromUID, string FromNickName, int FromLv, string FromBadge, int BadgeLv, string Icon, int TimeStamp);


    /*
       云月的斗鱼弹幕抓取lib，轻量版
       目前仅有弹幕抓取功能

       Email: ZeffiX@qq.com
       QQ: 8708534     

        Douyu.com livestream chat message receiver lib - light version
        Currently captures chat messages only

        Email: ZeffiX@qq.com
        QQ: 8708534     
    */

    class DouyuSocketPack  //Websocket Package Structure and builder
    {
        public enum SenderType : short
        {
            Server = 690,
            Client = 689,
        }

        public string this[string Key]  //message deserializer,get value of [key]
        {
            get
            {
                string _pattern = $"{Key}@=(?<value>.*?)/";
                Regex rx = new(_pattern);
                return rx.IsMatch(_body) ? UnscapeSlashAt(rx.Match(_body).Groups["value"].Value) : string.Empty;
            }
        }

        public int Length { get { return Encoding.UTF8.GetByteCount(_body) + 8; } }  //Length value of package header 
        public SenderType Sender { get { if (_sender == 690) { return SenderType.Server; } else { return SenderType.Client; } } set => _sender = (short)Sender; }  //message type of package 690 - server 689 - client
        public string Message //raw string of message, can be set
        {
            get { return _body; }
            set
            {
                _body = value.Substring(value.Length -1,1)=="\0" ? value : value+"\0" ;
                _sender = 689;
            }
        }

        private string _body;
        private short _sender;

        DouyuSocketPack() //create an empty message
        {
            _body = "\0";
            _sender = 689;
        }

        public DouyuSocketPack(string Message) //create a client message from douyu protocol formatted string
        {
            _sender = 689;
            _body = Message.Substring(Message.Length - 1, 1) == "\0" ? Message : Message + "\0";
        }

        public DouyuSocketPack(byte[] Message) //create a client message from byte array
        {
            byte[] b;
            int len;

            _body = "\0";
            _sender = 689;

            if (Message.Length > 9)
            {
                b = new byte[4];
                Array.Copy(Message, 0, b, 0, 4);
                if (!BitConverter.IsLittleEndian) Array.Reverse(b);
                len = BitConverter.ToInt32(b, 0);

                if (len == Message.Length)
                {

                    b = new byte[2];
                    Array.Copy(Message, 4, b, 0, 2);
                    if (!BitConverter.IsLittleEndian) Array.Reverse(b);
                    _sender = BitConverter.ToInt16(b, 0);

                    b = new byte[len - 8];
                    Array.Copy(Message, 8, b, 0, len - 8);
                    _body = Encoding.UTF8.GetString(b);
                }
                else if (len == Message.Length - 4)
                {
                    b = new byte[4];
                    Array.Copy(Message, 4, b, 0, 4);
                    if (!BitConverter.IsLittleEndian) Array.Reverse(b);
                    if (len == BitConverter.ToInt32(b, 0))
                    {
                        b = new byte[2];
                        Array.Copy(Message, 8, b, 0, 2);
                        if (!BitConverter.IsLittleEndian) Array.Reverse(b);
                        _sender = BitConverter.ToInt16(b, 0);

                        b = new byte[len - 8];
                        Array.Copy(Message, 12, b, 0, len - 8);
                        _body = Encoding.UTF8.GetString(b);
                    }



                }
            }


        }

        public byte[] ToBytes()   //get byte array from message pack
        {
            int len = Encoding.UTF8.GetByteCount(_body) + 8;
            byte[] result = new byte[len + 4];
            byte[] b;
            b = BitConverter.GetBytes(len);
            if (!BitConverter.IsLittleEndian) Array.Reverse(b);
            b.CopyTo(result, 0);
            b.CopyTo(result, 4);
            b = BitConverter.GetBytes(_sender);
            if (!BitConverter.IsLittleEndian) Array.Reverse(b);
            b.CopyTo(result, 8);
            result[10] = 0;
            result[11] = 0;

            b = Encoding.UTF8.GetBytes(_body);
            b.CopyTo(result, 12);

            return result;
        }


        public static string EscapeSlashAt(string str)  
        {
            return str.Replace("/", "@S").Replace("@", "@A");
        }

        public static string UnscapeSlashAt(string str)
        {
            return str.Replace("@S", "/").Replace("@A", "@");
        }

    }

    public class DouyuLiveChat  //Douyu Live stream chat handling class
    {
        public static string DocumentFolder { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), @"StreamTool\"); } }
        public enum ConnectionStatus
        {
            None,
            Connecting,
            Connected,
            Logging,
            LoggedIn,
            OK,
            Closing
        }

        public event LiveChatMessageHandler onLiveMessageReceived;  //Event handler on Chatmessage received

        // public event MessageEventHandler Send; //Event handler for future version development

        //public properties
        public int RoomID { get; private set; }
        public string Owner { get; private set; }
        public int AudienceCount { get { return audiCount; } }

        public string StatusDescripition { get; private set; } //describing module status


        //consts
        private const string vdwdae325w_64we = "220120210731";


        //private properties claim

        private string userName, wsProxyServer, danmuServer;  //guest username/websocketProxy server url/livechat websocket server url

        public ClientWebSocket wsWSProxy, wsDanmuProxy; //websocket class for wsProxy and livechat
        private HttpClient httpLoader;  //http ajax api loader

        private int groupID, userID, audiCount; //livechat group id and guest user id
        private string deviceID; //"did" or "divid" 
        private string wsProxyTimeSign;  //"kd" value for next wsproxy keeplive package
        private int wsProxyTimeStamp, danmuServerTimeStamp; //time of last "keeplive" message sent by each ws client
        private ConnectionStatus wsProxyStats, danmuStats; 
        private CancellationTokenSource canceltoken; //general cancellation token for all function in this class
        private System.Timers.Timer timer; //general timer for all functions in this class

        public List<Task> wsTasks; //tasks container will clean everytime timer elasped



        public DouyuLiveChat()  
        {
            Init();
        }

        public void Init()  //init or recet instance
        {
            StatusDescripition = "Initiating!";
            groupID = 1;
            deviceID = "10000000000000000000000000001501";
            wsProxyTimeStamp = 0;
            danmuServerTimeStamp = 0;
            timer = new(1000);
            wsTasks = new();
            httpLoader = new();
            wsProxyTimeSign = "";
            wsProxyStats = ConnectionStatus.None;
            danmuStats = ConnectionStatus.None;
            timer.Elapsed += onTimedEvent;
            timer.Stop();
        }




        public void Connect(int roomID) // start connecting to live room
        {
            Disconnect();
            RoomID = roomID;
            wsProxyTimeSign = "";
            wsProxyTimeStamp = 0;
            danmuServerTimeStamp = 0;
            StatusDescripition = "Connecting!";
            if (httpLoader == null) httpLoader = new();
            canceltoken = new();


            wsTasks.Add(wsProxyLogin());

        }



        public void Dispose() //disconnect and dispose all sources
        {
            Disconnect();

            wsDanmuProxy?.Dispose();
            wsDanmuProxy = null;
            wsWSProxy?.Dispose();
            wsWSProxy = null;
            httpLoader?.Dispose();
            httpLoader = null;


        }


        //wsProxy websocket server connection and login method
        private async Task wsProxyLogin() 
        {
            wsWSProxy = new();
            wsWSProxy.Options.AddSubProtocol("-");
            wsProxyStats = ConnectionStatus.Connecting;

            // load wsProxy websocket server list from "https://www.douyu.com/lapi/live/gateway/web/{RoomID}?isH5=1" and randomly select one

            string httpResponse = await httpLoader.GetStringAsync($"https://www.douyu.com/lapi/live/gateway/web/{RoomID}?isH5=1", canceltoken.Token);
            JObject responstObj = JObject.Parse(httpResponse);
            if (responstObj.ContainsKey("error") && responstObj["error"].ToObject<int>() == 0)
            {
                JArray wsServerArray = responstObj.SelectToken("data.wss").ToObject<JArray>();
                if (wsServerArray.Count > 0)
                {
                    int serverCount = wsServerArray.Count;
                    int selection = new Random().Next(0, serverCount);
                    wsProxyServer = $"wss://{wsServerArray[selection]["domain"]}:{wsServerArray[selection]["port"]}";
                }
            }



            //Connect to wsProxy server

            Uri uri = new(wsProxyServer);
            await wsWSProxy.ConnectAsync(uri, canceltoken.Token);



            WsProxyReceiving();

            wsProxyStats = ConnectionStatus.Logging;
            //generate and send login request message,login as a visitor
            int rt = TimeStamp.Now();
            string s = $"type@=loginreq/roomid@={RoomID}/dfl@=/username@=/password@=/ltkid@=/biz@=/stk@=/devid@={deviceID}/ct@=0/pt@=2/cvr@=0/tvr@=7/apd@=/rt@={rt}/vk@={wsProxyLoginSign(rt)}/ver@=20190610/aver@=218101901/dmbt@=chrome/dmbv@=92/er@=1/\0";
            DouyuSocketPack msg = new(s);
            await wsSend(wsWSProxy, msg.ToBytes());

        }


        //generate and send wsproxy websocket keeplive request message
        private async Task wsProxyKeepLive()
        {
            if (wsWSProxy != null && wsWSProxy.State == WebSocketState.Open)
            {
                int tick = TimeStamp.Now();
                wsProxyTimeStamp = tick;
                string s = $"type@=keeplive/vbw@=0/cdn@=tct-h5/tick@={tick}/kd@={wsProxyTimeSign}/\0";
                DouyuSocketPack msg = new(s);
                await wsSend(wsWSProxy, msg.ToBytes());

            }
        }

        private async Task wsProxyH5ckreq()
        {
            if (wsWSProxy != null && wsWSProxy.State == WebSocketState.Open)
            {
                string s = $"type@=h5ckreq/rid@={RoomID}/ti@={vdwdae325w_64we}/\0";
                DouyuSocketPack msg = new(s);
                await wsSend(wsWSProxy, msg.ToBytes());

            }
        }


        //Connect to danmuproxy ws server
        private async Task danmuLogin()
        {

            wsDanmuProxy = new();
            wsDanmuProxy.Options.AddSubProtocol("-");

            Uri uri = new(danmuServer);

            danmuStats = ConnectionStatus.Connecting;
            await wsDanmuProxy.ConnectAsync(uri, canceltoken.Token);

            danmuStats = ConnectionStatus.Logging;
            danmuReceiving();
            string s = $"type@=loginreq/roomid@={RoomID}/dfl@=/username@={userName}/uid@={userID}/ver@=20190610/aver@=218101901/ct@=0/\0";
            DouyuSocketPack msg = new(s);
            await wsSend(wsDanmuProxy, msg.ToBytes());
        }

        //join message group
        private async Task danmuJoinGroup()
        {
            string s = $"type@=joingroup/rid@={RoomID}/gid@={groupID}/\0";
            DouyuSocketPack msg = new(s);
            await wsSend(wsDanmuProxy, msg.ToBytes());
            danmuStats = ConnectionStatus.LoggedIn;
        }

        //send keep live message to danmu server
        private async Task danmuKeepLive()
        {
            if (wsDanmuProxy != null && wsDanmuProxy.State == WebSocketState.Open)
            {
                string s = $"type@=mrkl/\0";
                DouyuSocketPack msg = new(s);
                danmuServerTimeStamp = TimeStamp.Now();
                await wsSend(wsDanmuProxy, msg.ToBytes());
            }
        }


        //send binary message
        private async Task wsSend(ClientWebSocket ws, byte[] buffer)
        {
            if (!canceltoken.Token.IsCancellationRequested && ws != null && ws.State == WebSocketState.Open)
            {
                await ws.SendAsync(buffer, WebSocketMessageType.Binary, true, canceltoken.Token);
            }
        }


        //disconnect from cunnected server
        private void Disconnect()
        {
            try
            {
                wsProxyStats = ConnectionStatus.Closing;
                danmuStats = ConnectionStatus.Closing;
                timer?.Stop();
                canceltoken?.Cancel();
                wsWSProxy?.Abort();
                wsDanmuProxy?.Abort();


                wsTasks.Clear();

                canceltoken?.Dispose();
                canceltoken = null;
            }
            catch (WebSocketException e)
            {
                Console.WriteLine(e.ToString());
            }

            StatusDescripition = "Disconnected!";
        }


        //wsProxy server login hash
        private string wsProxyLoginSign(int timestamp)
        {
            StringBuilder sb = new();
            sb.Append(timestamp);
            sb.Append(@"r5*^5;}2#${XF[h+;'./.Q'1;,-]f'p[");
            sb.Append(deviceID);

            byte[] b = Encoding.Latin1.GetBytes(sb.ToString());

            byte[] r = Crypto.MD5Hash(b);

            sb.Clear();
            for (int i = 0; i < r.Length; i++) sb.Append(r[i].ToString("x2"));
            return sb.ToString();
        }


        //wsProxy server keeplive hash
        private void wsProxyKeepliveSign(string hex)
        {

            uint[] v = DataConverter.HexToUInt32(hex, true);
            uint[] key = new uint[4] { 0x174d4dc8, 0xfb8b26a6, 0x7b5a767d, 0x3b251e1f };
            uint[] v1 = new uint[2];
            for (int j = 0; j < v.Length; j += 2)
            {
                Array.Copy(v, j, v1, 0, 2);
                v1[0] = (v1[0] << 6) | (v1[0] >> 26);
                v1[0] = (v1[0] << 9) | (v1[0] >> 23);
                v1[1] ^= 0x0214f548;
                v1[1] ^= 0x393683a1;
                v1 = Crypto.XTEA(v1, key, -32);
                v1[0] ^= 0xc36ea028;
                v1[0] ^= 0xa555986e;
                v1[1] = (v1[1] << 3) | (v1[1] >> 29);
                v1[1] = (v1[1] << 8) | (v1[1] >> 24);
                Array.Copy(v1, 0, v, j, 2);
            }
            wsProxyTimeSign = DataConverter.UInt32ToHex(v, true);
        }


        //wsProxy ws receiving task
        private void WsProxyReceiving()
        {
            wsTasks.Add(Task.Factory.StartNew(async () =>
           {
               WebSocketReceiveResult result;
               byte[] buffer = new byte[2048];
               byte[] messageBuffer = Array.Empty<byte>();
               int iLength = 0, iReceived = 0, iSize;
               while (canceltoken != null && !canceltoken.IsCancellationRequested && wsWSProxy?.State == WebSocketState.Open)
               {

                   using MemoryStream ms = new();
                   iSize = 0;
                   do
                   {
                       result = await wsWSProxy.ReceiveAsync(buffer, canceltoken.Token);
                       ms.Write(buffer, 0, result.Count);
                       iSize += result.Count;
                   }
                   while (result != null && !result.EndOfMessage);
                   ms.Position = 0;
                   byte[] rcvBytes = ms.ToArray();
                   int pos = 0;
                   while (pos < iSize)
                   {
                       if (iLength == 0)
                       {
                           byte[] b = new byte[4];
                           Array.Copy(rcvBytes, pos, b, 0, 4);
                           if (!BitConverter.IsLittleEndian) Array.Reverse(b);
                           iLength = BitConverter.ToInt32(b) + 4;
                           messageBuffer = new byte[iLength];
                       }

                       int iCopied = iLength - iReceived <= iSize ? iLength - iReceived : iSize;
                       Array.Copy(rcvBytes, pos, messageBuffer, iReceived, iCopied);
                       iReceived += iCopied;
                       pos += iCopied;

                       if (iLength == iReceived)
                       {
                           iLength = 0;
                           iReceived = 0;
                           onwsProxyMessageReceived(new DouyuSocketPack(messageBuffer));

                       }


                   }
                   await Task.Delay(200);
               }
           }, TaskCreationOptions.LongRunning));

        }

        //start danmu ws receiving task
        private void danmuReceiving()
        {
            wsTasks.Add(Task.Factory.StartNew(async () =>
            {

                WebSocketReceiveResult result;
                byte[] buffer = new byte[2048];
                byte[] messageBuffer = Array.Empty<byte>();
                int iLength = 0, iReceived = 0, iSize;
                while (canceltoken != null && !canceltoken.IsCancellationRequested && wsDanmuProxy?.State == WebSocketState.Open)
                {

                    using MemoryStream ms = new();
                    iSize = 0;
                    do
                    {
                        result = await wsDanmuProxy.ReceiveAsync(buffer, canceltoken.Token);
                        ms.Write(buffer, 0, result.Count);
                        iSize += result.Count;
                    }
                    while (result != null && !result.EndOfMessage);
                    ms.Position = 0;
                    byte[] rcvBytes = ms.ToArray();
                    int pos = 0;
                    while (pos < iSize)
                    {
                        if (iLength == 0)
                        {
                            byte[] b = new byte[4];
                            Array.Copy(rcvBytes, pos, b, 0, 4);
                            if (!BitConverter.IsLittleEndian) Array.Reverse(b);
                            iLength = BitConverter.ToInt32(b) + 4;
                            messageBuffer = new byte[iLength];
                        }

                        int iCopied = iLength - iReceived <= iSize ? iLength - iReceived : iSize;
                        Array.Copy(rcvBytes, pos, messageBuffer, iReceived, iCopied);
                        iReceived += iCopied;
                        pos += iCopied;

                        if (iLength == iReceived)
                        {
                            iLength = 0;
                            iReceived = 0;
                            ondanmuMessageReceived(new DouyuSocketPack(messageBuffer));
                        }


                    }
                    await Task.Delay(200);
                }



            }, TaskCreationOptions.LongRunning));
        }

        
        private void onwsProxyMessageReceived(DouyuSocketPack message)
        {
            string msgType = message["type"];

            switch (msgType)
            {
                case "error":
                    LiveChatMessage errmsg = new(RoomID, "douyutv", "DouyuMessage", $"An error occurred while login, please check Room ID. Error Code: {message["code"]}", 0, "System", 0, "system", 0, "Error", TimeStamp.Now());
                    onLiveMessageReceived?.Invoke(errmsg);
                    Dispose();
                    break;

                case "loginres":
                    int.TryParse(message["userid"], out userID);
                    userName = message["username"];
                    wsProxyStats = ConnectionStatus.OK;
                    break;

                case "keeplive":
                    int.TryParse(message["hot"], out audiCount);
                    wsProxyKeepliveSign(message["kd"]);
                    wsProxyStats = ConnectionStatus.OK;
                    break;

                case "msgrepeaterproxylist":

                    string[] lst = message["list"].Trim('/').Split("/");

                    Random rnd = new();
                    int selection = rnd.Next(0, lst.Length);
                    string selectedServer = DouyuSocketPack.UnscapeSlashAt(lst[selection]);
                    StringBuilder sb = new("wss://");
                    sb.Append(DouyuSocketPack.UnscapeSlashAt(Regex.Match(selectedServer, @"ip@=(?<value>.*?)/").Groups["value"].Value));
                    sb.Append(':');
                    sb.Append(DouyuSocketPack.UnscapeSlashAt(Regex.Match(selectedServer, @"port@=(?<value>\d*)/").Groups["value"].Value));
                    sb.Append('/');
                    danmuServer = sb.ToString();
                    break;

                case "setmsggroup":
                    int.TryParse(message["gid"], out groupID);
                    wsTasks.Add(wsProxyH5ckreq());
                    wsTasks.Add(danmuLogin());
                    timer.Start();
                    break;
            }
        }

        private void ondanmuMessageReceived(DouyuSocketPack message)
        {
            string msgType = message["type"];

            switch (msgType)
            {
                case "error":
                    LiveChatMessage errmsg = new(RoomID, "douyutv", "DouyuMessage", $"An error occurred while login, please check Room ID. Error Code: {message["code"]}", 0, "System", 0, "system", 0, "Error", TimeStamp.Now());
                    onLiveMessageReceived?.Invoke(errmsg);
                    Dispose();
                    break;

                case "loginres":
                    wsTasks.Add(danmuJoinGroup());
                    break;

                case "pingreq":
                    wsTasks.Add(danmuKeepLive());
                    danmuStats = ConnectionStatus.OK;
                    break;

                case "mrkl":
                    danmuStats = ConnectionStatus.OK;
                    break;

                case "chatmsg":
                    int uid;
                    int.TryParse(message["uid"], out uid);
                    int ulv;
                    int.TryParse(message["level"], out ulv);
                    int blv;
                    int.TryParse(message["bl"], out blv);
                    LiveChatMessage msg = new(RoomID, "douyutv", message["cid"], message["txt"], uid, message["nn"], ulv, message["bnn"], blv, $"https://apic.douyucdn.cn/upload/{message["ic"]}_small.jpg", TimeStamp.Now());
                    onLiveMessageReceived?.Invoke(msg);
                    break;

            }
        }


        private void onTimedEvent(object source, ElapsedEventArgs e)
        {
            int timestamp = TimeStamp.Now();
            if (wsTasks != null) wsTasks.RemoveAll((x) => { return x.IsCompleted && x.CreationOptions != TaskCreationOptions.LongRunning; });
            if (wsWSProxy != null && wsWSProxy.State == WebSocketState.Open && timestamp - wsProxyTimeStamp >= 40) wsTasks.Add(wsProxyKeepLive());
            if (wsDanmuProxy != null && wsDanmuProxy.State == WebSocketState.Open && danmuStats == ConnectionStatus.OK && timestamp - danmuServerTimeStamp > 40) wsTasks.Add(danmuKeepLive());
            if (wsDanmuProxy.State != WebSocketState.Open && danmuStats == ConnectionStatus.OK) Connect(RoomID);
            if (wsWSProxy.State != WebSocketState.Open && wsProxyStats == ConnectionStatus.OK) Connect(RoomID);

        }

    }
}







