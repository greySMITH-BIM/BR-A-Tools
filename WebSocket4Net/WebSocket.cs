﻿using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using SuperSocket.ClientEngine.Common;
using SuperSocket.ClientEngine.Core;
using WebSocket4Net.Command;
using WebSocket4Net.Common;
using WebSocket4Net.Protocol;

namespace WebSocket4Net
{
    public partial class WebSocket : IDisposable
    {
        protected const string UserAgentKey = "User-Agent";

        public const int DefaultReceiveBufferSize = 4096;

        private const string m_UriScheme = "ws";

        private const string m_UriPrefix = m_UriScheme + "://";

        private const string m_SecureUriScheme = "wss";
        private const int m_SecurePort = 44301;

        private const string m_SecureUriPrefix = m_SecureUriScheme + "://";

        private const string m_NotOpenSendingMessage = "You must send data by websocket after websocket is opened!";

        private static readonly ProtocolProcessorFactory m_ProtocolProcessorFactory;

        private EventHandler m_Closed;

        private ClosedEventArgs m_ClosedArgs;

        private readonly Dictionary<string, ICommand<WebSocket, WebSocketCommandInfo>> m_CommandDict
            = new Dictionary<string, ICommand<WebSocket, WebSocketCommandInfo>>(StringComparer.OrdinalIgnoreCase);

        private EventHandler<DataReceivedEventArgs> m_DataReceived;

        private bool m_Disposed;

        private EventHandler<ErrorEventArgs> m_Error;

        private string m_LastPingRequest;

        private EventHandler<MessageReceivedEventArgs> m_MessageReceived;

        private EventHandler m_Opened;


        private EndPoint m_RemoteEndPoint;


        private int m_StateCode;

        /// <summary>
        ///     It is used for ping/pong and closing handshake checking
        /// </summary>
        private Timer m_WebSocketTimer;

        static WebSocket()
        {
            m_ProtocolProcessorFactory = new ProtocolProcessorFactory(new Rfc6455Processor(),
                new DraftHybi10Processor(), new DraftHybi00Processor());
        }

        internal TcpClientSession Client { get; private set; }

        /// <summary>
        ///     Gets the version of the websocket protocol.
        /// </summary>
        public WebSocketVersion Version { get; private set; }

        /// <summary>
        ///     Gets the last active time of the websocket.
        /// </summary>
        public DateTime LastActiveTime { get; internal set; }

        /// <summary>
        ///     Gets or sets a value indicating whether [enable auto send ping].
        /// </summary>
        /// <value>
        ///     <c>true</c> if [enable auto send ping]; otherwise, <c>false</c>.
        /// </value>
        public bool EnableAutoSendPing { get; set; }

        /// <summary>
        ///     Gets or sets the interval of ping auto sending, in seconds.
        /// </summary>
        /// <value>
        ///     The auto send ping internal.
        /// </value>
        public int AutoSendPingInterval { get; set; }

        internal IProtocolProcessor ProtocolProcessor { get; private set; }

        public bool SupportBinary => ProtocolProcessor.SupportBinary;

        internal Uri TargetUri { get; private set; }

        internal string SubProtocol { get; private set; }

        internal IDictionary<string, object> Items { get; private set; }

        internal List<KeyValuePair<string, string>> Cookies { get; private set; }

        internal List<KeyValuePair<string, string>> CustomHeaderItems { get; private set; }

        internal int StateCode => m_StateCode;

        public WebSocketState State => (WebSocketState) m_StateCode;

        public bool Handshaked { get; private set; }

        public IProxyConnector Proxy { get; set; }

        internal EndPoint HttpConnectProxy { get; private set; }

        protected IClientCommandReader<WebSocketCommandInfo> CommandReader { get; private set; }

        internal bool NotSpecifiedVersion { get; private set; }

        internal string LastPongResponse { get; set; }

        internal string HandshakeHost { get; private set; }

        internal string Origin { get; private set; }

#if !__IOS__
        public bool NoDelay { get; set; }
#endif

#if !SILVERLIGHT
        /// <summary>
        ///     set/get the local bind endpoint
        /// </summary>
        public EndPoint LocalEndPoint
        {
            get
            {
                if (Client == null)
                    return null;

                return Client.LocalEndPoint;
            }

            set
            {
                if (Client == null)
                    throw new Exception("Websocket client is not initilized.");

                Client.LocalEndPoint = value;
            }
        }
#endif

        public int ReceiveBufferSize
        {
            get => Client.ReceiveBufferSize;
            set => Client.ReceiveBufferSize = value;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private EndPoint ResolveUri(string uri, int defaultPort, out int port)
        {
            TargetUri = new Uri(uri);

            if (string.IsNullOrEmpty(Origin))
                Origin = TargetUri.GetOrigin();

            IPAddress ipAddress;

            EndPoint remoteEndPoint;

            port = TargetUri.Port;

            if (port <= 0)
                port = defaultPort;

            if (IPAddress.TryParse(TargetUri.Host, out ipAddress))
                remoteEndPoint = new IPEndPoint(ipAddress, port);
            else
                remoteEndPoint = new DnsEndPoint(TargetUri.Host, port);

            return remoteEndPoint;
        }

        private TcpClientSession CreateClient(string uri)
        {
            int port;
            var targetEndPoint = m_RemoteEndPoint = ResolveUri(uri, 80, out port);

            if (port == 80)
                HandshakeHost = TargetUri.Host;
            else
                HandshakeHost = TargetUri.Host + ":" + port;

            return new AsyncTcpSession();
        }


#if !NETFX_CORE

        private TcpClientSession CreateSecureClient(string uri)
        {
            var hostPos = uri.IndexOf('/', m_SecureUriPrefix.Length);

            if (hostPos < 0) //wss://localhost or wss://localhost:xxxx
            {
                hostPos = uri.IndexOf(':', m_SecureUriPrefix.Length, uri.Length - m_SecureUriPrefix.Length);

                if (hostPos < 0)
                    uri = uri + ":" + m_SecurePort + "/";
                else
                    uri = uri + "/";
            }
            else if (hostPos == m_SecureUriPrefix.Length) //wss://
            {
                throw new ArgumentException("Invalid uri", "uri");
            }
            else //wss://xxx/xxx
            {
                var colonPos = uri.IndexOf(':', m_SecureUriPrefix.Length, hostPos - m_SecureUriPrefix.Length);

                if (colonPos < 0) uri = uri.Substring(0, hostPos) + ":" + m_SecurePort + uri.Substring(hostPos);
            }

            int port;
            var targetEndPoint = m_RemoteEndPoint = ResolveUri(uri, m_SecurePort, out port);

            if (HttpConnectProxy != null) m_RemoteEndPoint = HttpConnectProxy;

            if (port == m_SecurePort)
                HandshakeHost = TargetUri.Host;
            else
                HandshakeHost = TargetUri.Host + ":" + port;

            return CreateSecureTcpSession();
        }
#endif

        private void Initialize(string uri, string subProtocol, List<KeyValuePair<string, string>> cookies,
            List<KeyValuePair<string, string>> customHeaderItems, string userAgent, string origin,
            WebSocketVersion version, EndPoint httpConnectProxy, int receiveBufferSize)
        {
            if (version == WebSocketVersion.None)
            {
                NotSpecifiedVersion = true;
                version = WebSocketVersion.Rfc6455;
            }

            Version = version;
            ProtocolProcessor = GetProtocolProcessor(version);

            Cookies = cookies;

            Origin = origin;

            if (!string.IsNullOrEmpty(userAgent))
            {
                if (customHeaderItems == null)
                    customHeaderItems = new List<KeyValuePair<string, string>>();

                customHeaderItems.Add(new KeyValuePair<string, string>(UserAgentKey, userAgent));
            }

            if (customHeaderItems != null && customHeaderItems.Count > 0)
                CustomHeaderItems = customHeaderItems;

            var handshakeCmd = new Handshake();
            m_CommandDict.Add(handshakeCmd.Name, handshakeCmd);
            var textCmd = new Text();
            m_CommandDict.Add(textCmd.Name, textCmd);
            var dataCmd = new Binary();
            m_CommandDict.Add(dataCmd.Name, dataCmd);
            var closeCmd = new Close();
            m_CommandDict.Add(closeCmd.Name, closeCmd);
            var pingCmd = new Ping();
            m_CommandDict.Add(pingCmd.Name, pingCmd);
            var pongCmd = new Pong();
            m_CommandDict.Add(pongCmd.Name, pongCmd);
            var badRequestCmd = new BadRequest();
            m_CommandDict.Add(badRequestCmd.Name, badRequestCmd);

            m_StateCode = WebSocketStateConst.None;

            SubProtocol = subProtocol;

            Items = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

            HttpConnectProxy = httpConnectProxy;


            TcpClientSession client;

            if (uri.StartsWith(m_UriPrefix, StringComparison.OrdinalIgnoreCase))
            {
                client = CreateClient(uri);
            }
            else if (uri.StartsWith(m_SecureUriPrefix, StringComparison.OrdinalIgnoreCase))
            {
#if !NETFX_CORE
                client = CreateSecureClient(uri);
#else
                throw new NotSupportedException("WebSocket4Net still has not supported secure websocket for UWP yet.");
#endif
            }
            else
            {
                throw new ArgumentException("Invalid uri", "uri");
            }

            client.ReceiveBufferSize = receiveBufferSize > 0 ? receiveBufferSize : DefaultReceiveBufferSize;
            client.Connected += client_Connected;
            client.Closed += client_Closed;
            client.Error += client_Error;
            client.DataReceived += client_DataReceived;

            Client = client;

            //Ping auto sending is enabled by default
            EnableAutoSendPing = true;
        }

        private void client_DataReceived(object sender, DataEventArgs e)
        {
            OnDataReceived(e.Data, e.Offset, e.Length);
        }

        partial void OnInternalError();

        private void client_Error(object sender, ErrorEventArgs e)
        {
            OnError(e);
            //Also fire close event if the connection fail to connect
            OnClosed();
        }

        partial void OnInternalClosed();


        private void client_Closed(object sender, EventArgs e)
        {
            OnClosed();
        }

        private void client_Connected(object sender, EventArgs e)
        {
            OnConnected();
        }

        internal bool GetAvailableProcessor(int[] availableVersions)
        {
            var processor = m_ProtocolProcessorFactory.GetPreferedProcessorFromAvialable(availableVersions);

            if (processor == null)
                return false;

            ProtocolProcessor = processor;
            return true;
        }

        public void Open()
        {
            m_StateCode = WebSocketStateConst.Connecting;

            if (Proxy != null)
                Client.Proxy = Proxy;

#if !__IOS__
            Client.NoDelay = NoDelay;
#endif

#if SILVERLIGHT
#if !WINDOWS_PHONE
            Client.ClientAccessPolicyProtocol = ClientAccessPolicyProtocol;
#endif
#endif
            Client.Connect(m_RemoteEndPoint);
        }

        private static IProtocolProcessor GetProtocolProcessor(WebSocketVersion version)
        {
            var processor = m_ProtocolProcessorFactory.GetProcessorByVersion(version);

            if (processor == null)
                throw new ArgumentException("Invalid websocket version");

            return processor;
        }

        private void OnConnected()
        {
            CommandReader = ProtocolProcessor.CreateHandshakeReader(this);

            if (Items.Count > 0)
                Items.Clear();

            ProtocolProcessor.SendHandshake(this);
        }

        protected internal virtual void OnHandshaked()
        {
            m_StateCode = WebSocketStateConst.Open;

            Handshaked = true;

            if (EnableAutoSendPing && ProtocolProcessor.SupportPingPong)
            {
                //Ping auto sending interval's default value is 60 seconds
                if (AutoSendPingInterval <= 0)
                    AutoSendPingInterval = 60;

                m_WebSocketTimer = new Timer(OnPingTimerCallback, ProtocolProcessor, AutoSendPingInterval * 1000,
                    AutoSendPingInterval * 1000);
            }

            OnInternalOpened();

            var opened = m_Opened;

            if (opened != null)
                opened(this, EventArgs.Empty);
        }

        partial void OnInternalOpened();


        private void OnPingTimerCallback(object state)
        {
            var protocolProcessor = state as IProtocolProcessor;

            if (!string.IsNullOrEmpty(m_LastPingRequest) && !m_LastPingRequest.Equals(LastPongResponse))
                try
                {
                    protocolProcessor.SendPong(this, "");
                }
                catch (Exception e)
                {
                    OnError(e);
                    return;
                }

            m_LastPingRequest = DateTime.Now.ToString();

            try
            {
                protocolProcessor.SendPing(this, m_LastPingRequest);
            }
            catch (Exception e)
            {
                OnError(e);
            }
        }

        public event EventHandler Opened
        {
            add => m_Opened += value;
            remove => m_Opened -= value;
        }

        public event EventHandler<MessageReceivedEventArgs> MessageReceived
        {
            add => m_MessageReceived += value;
            remove => m_MessageReceived -= value;
        }

        internal void FireMessageReceived(string message)
        {
            if (m_MessageReceived == null)
                return;

            m_MessageReceived(this, new MessageReceivedEventArgs(message));
        }

        public event EventHandler<DataReceivedEventArgs> DataReceived
        {
            add => m_DataReceived += value;
            remove => m_DataReceived -= value;
        }

        internal void FireDataReceived(byte[] data)
        {
            if (m_DataReceived == null)
                return;

            m_DataReceived(this, new DataReceivedEventArgs(data));
        }

        private bool EnsureWebSocketOpen()
        {
            if (!Handshaked)
            {
                OnError(new Exception(m_NotOpenSendingMessage));
                return false;
            }

            return true;
        }

        public void Send(string message)
        {
            if (!EnsureWebSocketOpen())
                return;

            ProtocolProcessor.SendMessage(this, message);
        }

        public void Send(byte[] data, int offset, int length)
        {
            if (!EnsureWebSocketOpen())
                return;

            ProtocolProcessor.SendData(this, data, offset, length);
        }

        public void Send(IList<ArraySegment<byte>> segments)
        {
            if (!EnsureWebSocketOpen())
                return;

            ProtocolProcessor.SendData(this, segments);
        }

        private void OnClosed()
        {
            OnInternalClosed();

            var fireBaseClose = false;

            if (m_StateCode == WebSocketStateConst.Closing || m_StateCode == WebSocketStateConst.Open ||
                m_StateCode == WebSocketStateConst.Connecting)
                fireBaseClose = true;

            m_StateCode = WebSocketStateConst.Closed;

            if (fireBaseClose)
                FireClosed();
        }

        public void Close()
        {
            Close(string.Empty);
        }

        public void Close(string reason)
        {
            Close(ProtocolProcessor.CloseStatusCode.NormalClosure, reason);
        }

        public void Close(int statusCode, string reason)
        {
            m_ClosedArgs = new ClosedEventArgs((short) statusCode, reason);

            //The websocket never be opened
            if (Interlocked.CompareExchange(ref m_StateCode, WebSocketStateConst.Closed, WebSocketStateConst.None)
                == WebSocketStateConst.None)
            {
                OnClosed();
                return;
            }

            //The websocket is connecting or in handshake
            if (Interlocked.CompareExchange(ref m_StateCode, WebSocketStateConst.Closing,
                    WebSocketStateConst.Connecting)
                == WebSocketStateConst.Connecting)
            {
                var client = Client;

                if (client != null && client.IsConnected)
                {
                    client.Close();
                    return;
                }

                OnClosed();
                return;
            }

            m_StateCode = WebSocketStateConst.Closing;

            //Disable auto ping
            ClearTimer();
            //Set closing hadnshake checking timer
            m_WebSocketTimer = new Timer(CheckCloseHandshake, null, 5 * 1000, Timeout.Infinite);

            try
            {
                ProtocolProcessor.SendCloseHandshake(this, statusCode, reason);
            }
            catch (Exception e)
            {
                if (Client != null) OnError(e);
            }
        }

        private void CheckCloseHandshake(object state)
        {
            if (m_StateCode == WebSocketStateConst.Closed)
                return;

            try
            {
                CloseWithoutHandshake();
            }
            catch (Exception e)
            {
                OnError(e);
            }
        }

        internal void CloseWithoutHandshake()
        {
            var client = Client;

            if (client != null)
                client.Close();
        }

        protected void ExecuteCommand(WebSocketCommandInfo commandInfo)
        {
            ICommand<WebSocket, WebSocketCommandInfo> command;

            if (m_CommandDict.TryGetValue(commandInfo.Key, out command)) command.ExecuteCommand(this, commandInfo);
        }

        private void OnDataReceived(byte[] data, int offset, int length)
        {
            while (true)
            {
                int left;

                var commandInfo = CommandReader.GetCommandInfo(data, offset, length, out left);

                if (CommandReader.NextCommandReader != null)
                    CommandReader = CommandReader.NextCommandReader;

                if (commandInfo != null)
                    ExecuteCommand(commandInfo);

                if (left <= 0)
                    break;

                offset = offset + length - left;
                length = left;
            }
        }

        internal void FireError(Exception error)
        {
            OnError(error);
        }

        public event EventHandler Closed
        {
            add => m_Closed += value;
            remove => m_Closed -= value;
        }

        private void ClearTimer()
        {
            var timer = m_WebSocketTimer;

            if (timer == null)
                return;

            lock (this)
            {
                if (m_WebSocketTimer == null)
                    return;

                timer.Change(Timeout.Infinite, Timeout.Infinite);
                timer.Dispose();

                m_WebSocketTimer = null;
            }
        }

        private void FireClosed()
        {
            ClearTimer();

            var handler = m_Closed;

            if (handler != null)
                handler(this, m_ClosedArgs ?? EventArgs.Empty);
        }

        public event EventHandler<ErrorEventArgs> Error
        {
            add => m_Error += value;
            remove => m_Error -= value;
        }

        private void OnError(ErrorEventArgs e)
        {
            OnInternalError();

            var handler = m_Error;

            if (handler == null)
                return;

            handler(this, e);
        }

        private void OnError(Exception e)
        {
            OnError(new ErrorEventArgs(e));
        }

        protected virtual void Dispose(bool disposing)
        {
            if (m_Disposed)
                return;

            if (disposing)
            {
                var client = Client;

                if (client != null)
                {
                    client.Connected -= client_Connected;
                    client.Closed -= client_Closed;
                    client.Error -= client_Error;
                    client.DataReceived -= client_DataReceived;

                    if (client.IsConnected)
                        client.Close();

                    Client = null;
                }

                ClearTimer();
            }

            m_Disposed = true;
        }

        ~WebSocket()
        {
            Dispose(false);
        }

#if !SILVERLIGHT

        private SecurityOption m_Security;

        /// <summary>
        ///     get the websocket's security options
        /// </summary>
        public SecurityOption Security
        {
            get
            {
                if (m_Security != null)
                    return m_Security;

                var secureClient = Client as SslStreamTcpSession;

                if (secureClient == null)
                    return m_Security = new SecurityOption();

                return m_Security = secureClient.Security;
            }
        }
#endif
    }
}