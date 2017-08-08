using System;
using System.Collections.Generic;
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

        public ISet<ITCPHandler> Handlers { get; set; }

        internal Func<ShadowsocksController, Configuration, TCPRelay, Socket, ITCPHandler> TCPHandlerFactory { get; set; }

        public TCPRelay(ShadowsocksController controller, Configuration conf)
        {
            _controller = controller;
            _config = conf;
            Handlers = new HashSet<ITCPHandler>();
            _lastSweepTime = DateTime.Now;
            TCPHandlerFactory =
                (shadowsocksController, configuration, tcpRelay, socket) => new TCPHandler(shadowsocksController,
                    configuration, tcpRelay, socket);
        }

        public override bool Handle(byte[] firstPacket, int length, SocketProxy socket, object state)
        {
            if (socket.ProtocolType != ProtocolType.Tcp
                || (length < 2 || firstPacket[0] != 5))
                return false;
            socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.NoDelay, true);
            ITCPHandler handler = TCPHandlerFactory(_controller, _config, this, socket);

            IList<ITCPHandler> handlersToClose = new List<ITCPHandler>();
            lock (Handlers)
            {
                Handlers.Add(handler);
                DateTime now = DateTime.Now;
                if (now - _lastSweepTime > TimeSpan.FromSeconds(1))
                {
                    _lastSweepTime = now;
                    foreach (ITCPHandler handler1 in Handlers)
                        if (now - handler1.LastActivity > TimeSpan.FromSeconds(900))
                            handlersToClose.Add(handler1);
                }
            }
            foreach (ITCPHandler handler1 in handlersToClose)
            {
                Logging.Debug("Closing timed out TCP connection.");
                handler1.Close();
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