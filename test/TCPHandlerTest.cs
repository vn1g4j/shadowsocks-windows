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
        private Mock<SocketProxy> _socketMock = new Mock<SocketProxy>(MockBehavior.Loose, (Socket)null);
        private Configuration _configuration = CreateMockConfiguration();

        [Fact]
        public void handshake_should_reject_if_protocol_is_not_sock5()
        {
            byte[] firstPacket = new byte[]{ 4, 0 };
            var sut = new TCPHandler(null, _configuration, null, _socketMock.Object);
            sut.Start(firstPacket, firstPacket.Length);

            var rejectResponse = TCPHandler.HandshakeRejectResponseHead;
            _socketMock.Verify(_ => _.BeginSend(rejectResponse, 0, rejectResponse.Length, SocketFlags.None,
                It.IsAny<AsyncCallback>(), null), Times.Once());
        }

        [Fact]
        public void handshake_should_close_handler_if_packet_length_less_than_2()
        {
            byte[] invalidPacket = new byte[] {4};
            var relay = new TCPRelay(null, null);
            var sut = new TCPHandler(null, _configuration, relay, _socketMock.Object);
            sut.Start(invalidPacket, invalidPacket.Length);

            VerifyHandlerClosed(sut, _socketMock);
        }

        [Fact]
        public void handshake_should_begin_send_version_packet_and_continue_by_handshakeSendCallback_if_first_packet_is_valid()
        {
            byte[] validPacket = new byte[] { TCPHandler.Socks5Version, 0 };
            var relay = new TCPRelay(null, null);
            var sut = new TCPHandler(null, _configuration, relay, _socketMock.Object);
            sut.Start(validPacket, validPacket.Length);
            VerifyHanderSendVersionPacketAndContinueByHandshakeSendCallback(sut);
        }

        [Fact]
        public void close_should_properly_clean_resources()
        {
            var remoteMock = new Mock<IProxy>(MockBehavior.Loose);
            var asyncSession = new TCPHandler.AsyncSession(remoteMock.Object);
            var encryptorMock = new Mock<IEncryptor>(MockBehavior.Loose);

            var relay = new TCPRelay(null, null);
            var sut = new TCPHandler(null, _configuration, relay, _socketMock.Object){CurrentRemoteSession = asyncSession};
            sut.Encryptor = encryptorMock.Object;
            relay.Handlers.Add(sut);
            sut.Close();

            AssertResourcesProperlyCleaned(sut, relay, _socketMock, remoteMock, encryptorMock);
        }

        [Fact]
        public void handshakeSendCallback_should_begin_receive_following_command()
        {
            var asyncResultMock = new Mock<IAsyncResult>();
            var asyncResult = asyncResultMock.Object;

            var sut = new TCPHandler(null, _configuration, null, _socketMock.Object);
            
            sut.HandshakeSendCallback(asyncResult);

            VerifyHandlerProperlyBeginReceiveCommand(asyncResult, sut);
        }

        [Fact]
        public void establish_tcp_connection()
        {
            var asyncResultMock = new Mock<IAsyncResult>();
            var asyncResult = asyncResultMock.Object;

            _socketMock.Setup(_ => _.EndReceive(asyncResult)).Returns(5);

            var sut = new TCPHandler(null, _configuration, null, _socketMock.Object);
            sut.ConnetionRecvBuffer[1] = TCPHandler.CMD_CONNECT;
            sut.HandshakeReceive2Callback(asyncResult);
            
            VerifySendBackEstablishedResponse(sut);
        }

        [Fact]
        public void establish_should_close_socket_if_client_send_establish_packet_length_less_than_5()
        {
            var asyncResultMock = new Mock<IAsyncResult>();
            var asyncResult = asyncResultMock.Object;
            _socketMock.Setup(_ => _.EndReceive(asyncResult)).Returns(4);
            var relay = new TCPRelay(null, null);
            var sut = new TCPHandler(null, _configuration, relay, _socketMock.Object);
            sut.ConnetionRecvBuffer[1] = TCPHandler.CMD_CONNECT;
            relay.Handlers.Add(sut);

            sut.HandshakeReceive2Callback(asyncResult);

            VerifySocketClosed();
        }

        private void VerifySocketClosed()
        {
            _socketMock.Verify(_ => _.Close());
        }

        [Fact]
        public void establish_should_close_socket_if_command_unsupported()
        {
            var asyncResultMock = new Mock<IAsyncResult>();
            var asyncResult = asyncResultMock.Object;
            _socketMock.Setup(_ => _.EndReceive(asyncResult)).Returns(5);
            var relay = new TCPRelay(null, null);
            var sut = new TCPHandler(null, _configuration, relay, _socketMock.Object);
            sut.ConnetionRecvBuffer[1] = TCPHandler.CMD_CONNECT-1;
            relay.Handlers.Add(sut);

            sut.HandshakeReceive2Callback(asyncResult);

            VerifySocketClosed();
        }

        private void VerifySendBackEstablishedResponse(TCPHandler sut)
        {
            var expectedResponse = new byte[] { TCPHandler.Socks5Version, TCPHandler.SuccessREP, TCPHandler.Reserve, TCPHandler.IpV4, 0, 0, 0, 0, 0, 0 };

            _socketMock.Verify(_ => _.BeginSend(expectedResponse, 0, expectedResponse.Length, SocketFlags.None,
                new AsyncCallback(sut.ResponseCallback), null));
        }

        private void VerifyHanderSendVersionPacketAndContinueByHandshakeSendCallback(TCPHandler sut)
        {
            var expectedSendBack = TCPHandler.Socks5HandshakeResponseHead;
            _socketMock.Verify(_ => _.BeginSend(expectedSendBack, 0, expectedSendBack.Length, SocketFlags.None,
                new AsyncCallback(sut.HandshakeSendCallback), null));
        }

        private void VerifyHandlerProperlyBeginReceiveCommand(IAsyncResult asyncResult, TCPHandler sut)
        {
            _socketMock.Verify(_ => _.EndSend(asyncResult));
            _socketMock.Verify(_ => _.BeginReceive(It.IsAny<byte[]>(), 0, 3 + EncryptorBase.ADDR_ATYP_LEN + 1, SocketFlags.None,
                new AsyncCallback(sut.HandshakeReceive2Callback), null));
        }

        private static Configuration CreateMockConfiguration()
        {
            var config = new Configuration {proxy = new ProxyConfig {proxyTimeout = 0}};
            config.configs = new List<Server>();
            return config;
        }

        [Fact]
        public void close_should_ignore_closed_handler()
        {
            var socketMock = new Mock<SocketProxy>(MockBehavior.Loose, (Socket)null);
            var config = CreateMockConfiguration();
            var relay = new TCPRelay(null, null);
            var sut = new TCPHandler(null, config, relay, socketMock.Object);
            sut.Closed = true;
            sut.Close();

            VerifyHandlerDoNotCloseSocket(socketMock);
        }

        private static void VerifyHandlerDoNotCloseSocket(Mock<SocketProxy> socketMock)
        {
            socketMock.Verify(_ => _.Close(), Times.Never);
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
