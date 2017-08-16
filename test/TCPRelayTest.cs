using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using Moq;
using Shadowsocks.Controller.Service;
using Shadowsocks.Util.Sockets;
using Xunit;

namespace test
{
    public class TCPRelayTest
    {
        private static readonly byte[] FirstPacket = new byte[] { TCPHandler.Socks5Version, 0 };
        private Mock<SocketProxy> _socketMoq = CreateNewSocketProxyMoq();
        private Mock<ITCPHandler> _tcpHanlderMoq = new Mock<ITCPHandler>();

        private TCPRelay _sut;

        public TCPRelayTest()
        {
            _sut = new TCPRelay(null, null)
            {
                TCPHandlerFactory = (controller, configuration, relay, socket) => _tcpHanlderMoq.Object
            };
        }

        [Fact]
        public void handle_should_return_false_if_protocol_not_compatible()
        {
            _socketMoq.Setup(_ => _.ProtocolType).Returns(() => ProtocolType.Raw);
            var handleResult = _sut.Handle(FirstPacket, FirstPacket.Length, _socketMoq.Object, null);
            VerifyHandleFailedDueToWrongSocketProtocolType(handleResult, _socketMoq);
        }

        [Fact]
        public void handle_should_start_a_tcp_handler()
        {
            var result = _sut.Handle(FirstPacket, FirstPacket.Length, _socketMoq.Object, null);

            VerifyHandlerProperlyStarted(_sut, _tcpHanlderMoq);
        }

        [Fact]
        public void handle_should_only_close_timeout_handlers()
        {
            _tcpHanlderMoq = CreateTimeoutHandlerMock();
            _sut = new TCPRelay(null, null, DefinitelyTimeoutSweepTime());
            var newCreatedTcpHandlerMock = new Mock<ITCPHandler>();
            newCreatedTcpHandlerMock.Setup(_ => _.Start(FirstPacket, FirstPacket.Length));
            newCreatedTcpHandlerMock.Setup(_ => _.LastActivity).Returns(DateTime.Now);
            _sut.TCPHandlerFactory = (controller, configuration, arg3, arg4) => newCreatedTcpHandlerMock.Object;
            _sut.Handlers.Add(_tcpHanlderMoq.Object);

            _sut.Handle(FirstPacket, FirstPacket.Length, _socketMoq.Object, null);

            VerifyProperlyHandleTimeoutHandlers(_tcpHanlderMoq, newCreatedTcpHandlerMock);
        }

        [Fact]
        public void close_all_handlers_when_stop_relay()
        {
            var tcpRelay = new TCPRelay(null, null);
            var handlerMocks = Enumerable.Range(0, 2).Select(_ => new Mock<ITCPHandler>()).ToList();
            foreach (var handlerMock in handlerMocks)
            {
                handlerMock.Setup(_ => _.Close());
                tcpRelay.Handlers.Add(handlerMock.Object);
            }
            
            tcpRelay.Stop();

            VerifyAllHandlersClosed(handlerMocks);
        }

        private static void VerifyAllHandlersClosed(List<Mock<ITCPHandler>> handlerMocks)
        {
            foreach (var handlerMock in handlerMocks)
            {
                handlerMock.Verify(_ => _.Close(), Times.Exactly(1));
            }
        }

        private static void VerifyHandleFailedDueToWrongSocketProtocolType(bool handleResult, Mock<SocketProxy> socketMoq)
        {
            Assert.False(handleResult);
            socketMoq.Verify(_ => _.ProtocolType, Times.Exactly(1));
        }

        private static void VerifyHandlerProperlyStarted(TCPRelay relay, Mock<ITCPHandler> tcpHanlderMoq)
        {
            Assert.True(relay.Handlers.Contains(tcpHanlderMoq.Object));
            tcpHanlderMoq.Verify(_ => _.Start(FirstPacket, FirstPacket.Length), Times.Exactly(1));
        }

        private static Mock<ITCPHandler> CreateTimeoutHandlerMock()
        {
            var timeoutHandlerMock = new Mock<ITCPHandler>();
            timeoutHandlerMock.Setup(_ => _.LastActivity)
                .Returns(DefinitelyTimeoutHandlerLastActivity());
            return timeoutHandlerMock;
        }

        private void VerifyProperlyHandleTimeoutHandlers(Mock<ITCPHandler> timeoutHandlerMock, Mock<ITCPHandler> tcpHandlerMock)
        {
            VerifyHandlerClosed(timeoutHandlerMock);
            VerifyHandlerNotClosed(tcpHandlerMock);
        }

        private void VerifyHandlerNotClosed(Mock<ITCPHandler> tcpHandlerMock)
        {
            tcpHandlerMock.Verify(_=>_.Close(), Times.Never);
        }

        private static void VerifyHandlerClosed(Mock<ITCPHandler> timeoutHandlerMock)
        {
            timeoutHandlerMock.Verify(_ => _.Close(), Times.Exactly(1));
        }

        private static Mock<SocketProxy> CreateNewSocketProxyMoq()
        {
            var socketMoq = new Mock<SocketProxy>(MockBehavior.Strict, (Socket) null);
            socketMoq.Setup(_ => _.ProtocolType).Returns(ProtocolType.Tcp);
            socketMoq.Setup(_ => _.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.NoDelay, true));
            return socketMoq;
        }

        private static DateTime DefinitelyTimeoutSweepTime()
        {
            return DateTime.Now.Add(-TCPRelay.SweepPeriod).AddMilliseconds(-1);
        }

        private static DateTime DefinitelyTimeoutHandlerLastActivity()
        {
            return DateTime.Now.Add(-TCPRelay.HandlerTimeout).AddMilliseconds(-1);
        }
    }
}