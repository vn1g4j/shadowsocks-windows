using System;
using System.Collections.Generic;
using System.Net.Sockets;
using Moq;
using Shadowsocks.Controller.Service;
using Shadowsocks.Encryption;
using Shadowsocks.Model;
using Shadowsocks.Proxy;
using Shadowsocks.Util.Sockets;
using Xunit;

namespace test
{
    public class TCPHandlerTest
    {
        [Fact]
        public void handshake_should_reject_if_protocol_is_not_sock5()
        {
            var socketMock = new Mock<SocketProxy>(MockBehavior.Loose, (Socket)null);

            byte[] firstPacket = new byte[]{ 4, 0 };
            var config = new Configuration{ proxy = new ProxyConfig{proxyTimeout = 0} };
            config.configs = new List<Server>();
            var sut = new TCPHandler(null, config, null, socketMock.Object);
            sut.Start(firstPacket, firstPacket.Length);

            var rejectResponse = new byte[] {0, 91};
            socketMock.Verify(_ => _.BeginSend(rejectResponse, 0, rejectResponse.Length, SocketFlags.None,
                It.IsAny<AsyncCallback>(), null), Times.Once());
        }

        [Fact]
        public void handshake_should_close_handler_if_packet_length_less_than_2()
        {
            var socketMock = new Mock<SocketProxy>(MockBehavior.Loose, (Socket)null);

            byte[] invalidPacket = new byte[] {4};
            var config = new Configuration { proxy = new ProxyConfig { proxyTimeout = 0 } };
            config.configs = new List<Server>();
            var relay = new TCPRelay(null, null);
            var sut = new TCPHandler(null, config, relay, socketMock.Object);
            sut.Start(invalidPacket, invalidPacket.Length);

            VerifyHandlerClosed(sut, socketMock);
        }

        [Fact]
        public void close_should_properly_clean_resources()
        {
            var socketMock = new Mock<SocketProxy>(MockBehavior.Loose, (Socket)null);

            var remoteMock = new Mock<IProxy>(MockBehavior.Loose);
            var asyncSession = new TCPHandler.AsyncSession(remoteMock.Object);

            var encryptorMock = new Mock<IEncryptor>(MockBehavior.Loose);
            
            var config = new Configuration { proxy = new ProxyConfig { proxyTimeout = 0 } };
            config.configs = new List<Server>();
            var relay = new TCPRelay(null, null);
            var sut = new TCPHandler(null, config, relay, socketMock.Object){CurrentRemoteSession = asyncSession};
            sut.Encryptor = encryptorMock.Object;
            relay.Handlers.Add(sut);
            sut.Close();

            AssertResourcesProperlyCleaned(sut, relay, socketMock, remoteMock, encryptorMock);
        }

        private static void VerifyHandlerClosed(TCPHandler sut, Mock<SocketProxy> socketMock)
        {
            Assert.True(sut.Closed);
            VerifySocketDisposed(socketMock);
        }

        private static void AssertResourcesProperlyCleaned(TCPHandler sut, TCPRelay relay, Mock<SocketProxy> socketMock, Mock<IProxy> remoteMock,
            Mock<IEncryptor> encryptorMock)
        {
            Assert.True(sut.Closed);
            AssertRelayRemoveHandler(relay, sut);
            VerifySocketDisposed(socketMock);
            VerifyRemoteDisposed(remoteMock);
            VerifyEncryptorDisposed(encryptorMock);
        }

        private static void VerifyEncryptorDisposed(Mock<IEncryptor> encryptorMock)
        {
            encryptorMock.Verify(_ => _.Dispose(), Times.Once());
        }

        private static void AssertRelayRemoveHandler(TCPRelay relay, TCPHandler sut)
        {
            Assert.False(relay.Handlers.Contains(sut));
        }

        private static void VerifyRemoteDisposed(Mock<IProxy> remoteMock)
        {
            remoteMock.Verify(_ => _.Shutdown(SocketShutdown.Both), Times.Once);
            remoteMock.Verify(_ => _.Close(), Times.Once);
        }

        private static void VerifySocketDisposed(Mock<SocketProxy> socketMock)
        {
            socketMock.Verify(_ => _.Shutdown(SocketShutdown.Both), Times.Once);
            socketMock.Verify(_ => _.Close(), Times.Once);
        }
    }
}
