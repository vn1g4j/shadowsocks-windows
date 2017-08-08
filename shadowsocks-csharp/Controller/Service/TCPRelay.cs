using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using Shadowsocks.Model;
using Shadowsocks.Util.Sockets;

namespace Shadowsocks.Controller.Service
{
    class TCPRelay : Listener.Service
    {
        private ShadowsocksController _controller;
        private DateTime _lastSweepTime;
        private Configuration _config;
        internal static readonly TimeSpan SweepPeriod = TimeSpan.FromSeconds(1);
        internal static readonly TimeSpan HandlerTimeout = TimeSpan.FromSeconds(900);

        public ISet<ITCPHandler> Handlers { get; set; }

        internal Func<ShadowsocksController, Configuration, TCPRelay, Socket, ITCPHandler> TCPHandlerFactory { get; set; }

        internal void SetLastSweepTime(DateTime lastSweepTime)
        {
            _lastSweepTime = lastSweepTime;
        }

        public TCPRelay(ShadowsocksController controller, Configuration conf) : this(controller, conf, DateTime.Now)
        {
            
        }

        internal TCPRelay(ShadowsocksController controller, Configuration conf, DateTime lastSweepTime)
        {
            _controller = controller;
            _config = conf;
            Handlers = new HashSet<ITCPHandler>();
            _lastSweepTime = lastSweepTime;
            TCPHandlerFactory =
                (shadowsocksController, configuration, tcpRelay, socket) => new TCPHandler(shadowsocksController,
                    configuration, tcpRelay, socket);
        }

        public override bool Handle(byte[] firstPacket, int length, SocketProxy socket, object state)
        {
            if (NotCompatible(firstPacket, length, socket))
            {
                return false;
            }
            socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.NoDelay, true);
            var handler = TCPHandlerFactory(_controller, _config, this, socket);

            var handlersToClose = Enumerable.Empty<ITCPHandler>();
            lock (Handlers)
            {
                Handlers.Add(handler);
                var now = DateTime.Now;
                if (NeedSweepTimeoutHandlers(now))
                {
                    _lastSweepTime = now;
                    handlersToClose = GetTimeoutHandlersList(now);
                }
            }
            foreach (var timeoutHandler in handlersToClose)
            {
                Logging.Debug("Closing timed out TCP connection.");
                timeoutHandler.Close();
            }

            /*
             * Start after we put it into Handlers set. Otherwise if it failed in handler.Start()
             * then it will call handler.Close() before we add it into the set.
             * Then the handler will never release until the next Handle call. Sometimes it will
             * cause odd problems (especially during memory profiling).
             */
            handler.Start(firstPacket, length);

            return true;
        }

        private IEnumerable<ITCPHandler> GetTimeoutHandlersList(DateTime now)
        {
            return Handlers.Where(_ => IsHandlerTimeout(now, _)).ToList();
        }

        private static bool IsHandlerTimeout(DateTime now, ITCPHandler handler1)
        {
            return now - handler1.LastActivity > HandlerTimeout;
        }

        private bool NeedSweepTimeoutHandlers(DateTime now)
        {
            return now - _lastSweepTime > SweepPeriod;
        }

        private static bool NotCompatible(byte[] firstPacket, int length, SocketProxy socket)
        {
            return socket.ProtocolType != ProtocolType.Tcp
                   || (length < 2 || firstPacket[0] != 5);
        }

        public override void Stop()
        {
            List<ITCPHandler> handlersToClose = new List<ITCPHandler>();
            lock (Handlers)
            {
                handlersToClose.AddRange(Handlers);
            }
            handlersToClose.ForEach(h => h.Close());
        }

        public void UpdateInboundCounter(Server server, long n)
        {
            _controller.UpdateInboundCounter(server, n);
        }

        public void UpdateOutboundCounter(Server server, long n)
        {
            _controller.UpdateOutboundCounter(server, n);
        }

        public void UpdateLatency(Server server, TimeSpan latency)
        {
            _controller.UpdateLatency(server, latency);
        }
    }
}