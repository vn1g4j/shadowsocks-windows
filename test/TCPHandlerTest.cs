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
        private const int ValidEstablishConnectionPacketMinLength = 5;
        private Mock<SocketProxy> _socketMock = new Mock<SocketProxy>(MockBehavior.Loose, (Socket)null);
        private Configuration _configuration = CreateMockConfiguration();
        private TCPRelay _tcpRelay = new TCPRelay(null, null);
        private Mock<IProxy> _remoteMock = new Mock<IProxy>(MockBehavior.Loose);
        private TCPHandler.AsyncSession _asyncSession;
        private Mock<IEncryptor> _encryptorMock = new Mock<IEncryptor>(MockBehavior.Loose);
        private IAsyncResult _asyncResult = (new Mock<IAsyncResult>()).Object;
        private TCPHandler _sut;
        private const int ExpectedDomainLength = 16;

        public TCPHandlerTest()
        {
            _asyncSession = new TCPHandler.AsyncSession(_remoteMock.Object);
            _sut = new TCPHandler(null, _configuration, _tcpRelay, _socketMock.Object) { CurrentRemoteSession = _asyncSession };
            _sut.ConnetionRecvBuffer[4] = ExpectedDomainLength;
            _sut.Encryptor = _encryptorMock.Object;
            _tcpRelay.Handlers.Add(_sut);
        }

        [Fact]
        public void handshake_should_reject_if_protocol_is_not_sock5()
        {
            byte[] invalidFirstPacket = new byte[]{ TCPHandler.Socks5Version - 1, 0 };
            _sut.Start(invalidFirstPacket, invalidFirstPacket.Length);

            var rejectResponse = TCPHandler.HandshakeRejectResponseHead;
            _socketMock.Verify(_ => _.BeginSend(rejectResponse, 0, rejectResponse.Length, SocketFlags.None,
                It.IsAny<AsyncCallback>(), null), Times.Once());
        }

        [Fact]
        public void handshake_should_close_handler_if_packet_length_less_than_2()
        {
            byte[] invalidPacket = new byte[] { TCPHandler.Socks5Version };
            
            _sut.Start(invalidPacket, invalidPacket.Length);

            VerifyHandlerClosed(_sut, _socketMock);
        }

        [Fact]
        public void handshake_should_begin_send_version_packet_and_continue_by_handshakeSendCallback_if_first_packet_is_valid()
        {
            byte[] validPacket = new byte[] { TCPHandler.Socks5Version, 0 };
            _sut.Start(validPacket, validPacket.Length);
            VerifyHanderSendVersionPacketAndContinueByHandshakeSendCallback(_sut);
        }

        [Fact]
        public void close_should_properly_clean_resources()
        {
            _sut.Close();

            AssertResourcesProperlyCleaned(_sut, _tcpRelay, _socketMock, _remoteMock, _encryptorMock);
        }

        [Fact]
        public void handshakeSendCallback_should_begin_receive_following_command()
        {
            _sut.HandshakeSendCallback(_asyncResult);

            VerifyHandlerProperlyBeginReceiveCommand(_asyncResult, _sut);
        }

        [Fact]
        public void establish_tcp_connection()
        {
            _socketMock.Setup(_ => _.EndReceive(_asyncResult)).Returns(ValidEstablishConnectionPacketMinLength);
            
            _sut.ConnetionRecvBuffer[1] = TCPHandler.CMD_CONNECT;
            _sut.HandshakeReceive2Callback(_asyncResult);
            
            VerifySendBackEstablishedResponse(_sut);
        }

        [Fact]
        public void establish_should_close_socket_if_client_send_establish_packet_length_less_than_5()
        {
            _socketMock.Setup(_ => _.EndReceive(_asyncResult)).Returns(ValidEstablishConnectionPacketMinLength - 1);
            _sut.ConnetionRecvBuffer[1] = TCPHandler.CMD_CONNECT;

            _sut.HandshakeReceive2Callback(_asyncResult);

            VerifySocketClosed();
        }

        private void VerifySocketClosed()
        {
            _socketMock.Verify(_ => _.Close());
        }

        [Fact]
        public void establish_should_close_socket_if_command_unsupported()
        {
            _socketMock.Setup(_ => _.EndReceive(_asyncResult)).Returns(ValidEstablishConnectionPacketMinLength);
            var unsupportedCmd = (byte)(TCPHandler.CMD_CONNECT - 1);
            _sut.ConnetionRecvBuffer[1] = unsupportedCmd;

            _sut.HandshakeReceive2Callback(_asyncResult);

            VerifySocketClosed();
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

        [Fact]
        public void read_address_should_erase_first_3_bytes()
        {
            byte[] buffer = new byte[3 + EncryptorBase.ADDR_ATYP_LEN + 1];
            for(int i = 0; i < buffer.Length; i++)
            {
                buffer[i] = (byte) i;
            }
            Array.Copy(buffer, _sut.ConnetionRecvBuffer, buffer.Length);

            _sut.ReadAddress(0, null);

            AssertFirstThreeBytesErased(buffer);
        }

        [Theory]
        [InlineData(EncryptorBase.ATYP_IPv4, TCPHandler.IPV4Length + EncryptorBase.ADDR_PORT_LEN - 1)]
        [InlineData(EncryptorBase.ATYP_DOMAIN, ExpectedDomainLength + EncryptorBase.ADDR_PORT_LEN)]
        [InlineData(EncryptorBase.ATYP_IPv6, TCPHandler.IPV6Length + EncryptorBase.ADDR_PORT_LEN - 1)]
        public void read_address_should_map_bytes_remain_corretly(int addressType, int bytesRemain)
        {
            _sut.ConnetionRecvBuffer[3] = (byte)addressType;
            _sut.ReadAddress(null);
            VerifySocketBeginReceiveExpectedBytes(bytesRemain);
        }

        private void VerifySocketBeginReceiveExpectedBytes(int bytesRemain)
        {
            _socketMock.Verify(_ => _.BeginReceive(It.IsAny<byte[]>(), 2, It.IsAny<int>(), SocketFlags.None,
                It.IsAny<AsyncCallback>(),
                new object[] {bytesRemain, null}));
        }


        private void AssertFirstThreeBytesErased(byte[] buffer)
        {
            for (int i = 0; i < buffer.Length - 3; i++)
            {
                Assert.Equal(buffer[i + 3], _sut.ConnetionRecvBuffer[i]);
            }
        }

        private void VerifySendBackEstablishedResponse(TCPHandler sut)
        {
            var expectedResponse = new byte[] { TCPHandler.Socks5Version, TCPHandler.SuccessREP, TCPHandler.Reserve, TCPHandler.IpV4, 0, 0, 0, 0, 0, 0 };

            _socketMock.Verify(_ => _.BeginSend(expectedResponse, 0, expectedResponse.Length, SocketFlags.None,
                new AsyncCallback(sut.ResponseConnectCallback), null));
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
            var config = new Configuration { proxy = new ProxyConfig { proxyTimeout = 0 } };
            config.configs = new List<Server>();
            return config;
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
