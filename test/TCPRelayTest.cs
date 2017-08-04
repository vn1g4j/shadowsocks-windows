using System.Net.Sockets;
using Moq;
using Shadowsocks.Controller;
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
    }
}