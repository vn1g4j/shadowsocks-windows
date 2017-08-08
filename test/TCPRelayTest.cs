using System;
using System.Net.Sockets;
using Moq;
using Shadowsocks.Controller;
using Shadowsocks.Controller.Service;
using Shadowsocks.Util.Sockets;
using Xunit;

namespace test
{
    public class TCPRelayTest
    {
        [Fact]
        public void handle_should_return_false_if_protocol_not_compatible()
        {
            var socketMoq = new Mock<SocketProxy>(MockBehavior.Strict, (Socket)null);
            socketMoq.Setup(_ => _.ProtocolType).Returns(() => ProtocolType.Raw);
            var tcpRelay = new TCPRelay(null, null);
            var handleResult = tcpRelay.Handle(FirstPacket, FirstPacket.Length, socketMoq.Object, null);
            VerifyHandleFailedDueToWrongSocketProtocolType(handleResult, socketMoq);
        }

        [Fact]
        public void handle_should_start_a_tcp_handler()
        {
            var tcpHanlderMoq = new Mock<ITCPHandler>();
            var socketMoq = CreateNewSocketProxyMoq();

            var tcpRelay = new TCPRelay(null, null);
            tcpRelay.TCPHandlerFactory = (controller, configuration, relay, socket) => tcpHanlderMoq.Object;

            var result = tcpRelay.Handle(FirstPacket, FirstPacket.Length, socketMoq.Object, null);

            VerifyHandlerProperlyStarted(tcpRelay, tcpHanlderMoq);
        }

        [Fact]
        public void handle_should_only_close_timeout_handlers()
        {
            var timeoutHandlerMock = CreateTimeoutHandlerMock();
            var socketMoq = CreateNewSocketProxyMoq();

            var tcpRelay = new TCPRelay(null, null, DefinitelyTimeoutSweepTime());
            var tcpHandlerMock = new Mock<ITCPHandler>();
            tcpHandlerMock.Setup(_ => _.Start(FirstPacket, FirstPacket.Length));
            tcpHandlerMock.Setup(_ => _.LastActivity).Returns(DateTime.Now);
            tcpRelay.TCPHandlerFactory = (controller, configuration, arg3, arg4) => tcpHandlerMock.Object;
            tcpRelay.Handlers.Add(timeoutHandlerMock.Object);
            tcpRelay.Handle(FirstPacket, FirstPacket.Length, socketMoq.Object, null);
            VerifyProperlyHandleTimeoutHandlers(timeoutHandlerMock, tcpHandlerMock);
        }

        private static void VerifyHandleFailedDueToWrongSocketProtocolType(bool handleResult, Mock<SocketProxy> socketMoq)
        {
            Assert.False(handleResult);
            socketMoq.Verify(_ => _.ProtocolType, Times.Exactly(1));
        }

        private static readonly byte[] FirstPacket = new byte[] {5, 0};

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