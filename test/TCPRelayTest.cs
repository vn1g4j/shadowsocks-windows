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
            var handleResult = tcpRelay.Handle(It.IsAny<byte[]>(), It.IsAny<int>(), socketMoq.Object, It.IsAny<object>());
            Assert.False(handleResult);
            socketMoq.Verify(_=>_.ProtocolType, Times.Exactly(1));
        }

        [Fact]
        public void handler_should_start_a_tcp_handler_to_handle()
        {
            var firstPacket = new byte[] {5, 0};
            var tcpHanlderMoq = new Mock<ITCPHandler>();
            var socketMoq = new Mock<SocketProxy>(MockBehavior.Strict, (Socket)null);
            socketMoq.Setup(_ => _.ProtocolType).Returns(ProtocolType.Tcp);
            socketMoq.Setup(_ => _.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.NoDelay, true));

            var tcpRelay = new TCPRelay(null, null);

            tcpRelay.TCPHandlerFactory = (controller, configuration, relay, socket) => tcpHanlderMoq.Object;

            var result = tcpRelay.Handle(firstPacket, firstPacket.Length, socketMoq.Object, null);

            Assert.True(result);

            tcpHanlderMoq.Verify(_=>_.Start(firstPacket, firstPacket.Length), Times.Exactly(1));
        }
    }
}