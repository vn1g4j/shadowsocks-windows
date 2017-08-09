using System;
using System.Net;
using System.Net.Sockets;

namespace Shadowsocks.Util.Sockets
{
    public class SocketProxy
    {
        private Socket _socket;
        public virtual ProtocolType ProtocolType => _socket.ProtocolType;
        public virtual EndPoint LocalEndPoint => _socket.LocalEndPoint;
        public virtual EndPoint RemoteEndPoint => _socket.RemoteEndPoint;
        public virtual int Available => _socket.Available;

        public SocketProxy(Socket socket)
        {
            _socket = socket;
        }
        public static implicit operator SocketProxy(Socket socket)
        {
            return new SocketProxy(socket);
        }

        public static implicit operator Socket(SocketProxy socketProxy)
        {
            return socketProxy._socket;
        }

        public virtual void SetSocketOption(SocketOptionLevel optionLevel, SocketOptionName optionName, bool optionValue)
        {
            _socket.SetSocketOption(optionLevel, optionName, optionValue);
        }

        public virtual void Close()
        {
            _socket.Close();
        }

        public virtual void Shutdown(SocketShutdown how)
        {
            _socket.Shutdown(how);
        }

        public virtual IAsyncResult BeginSend(byte[] buffer, int offset, int size, SocketFlags socketFlags, AsyncCallback callback, object state)
        {
            return _socket.BeginSend(buffer, offset, size, socketFlags, callback, state);
        }

        public virtual int EndSend(IAsyncResult asyncResult)
        {
            return _socket.EndSend(asyncResult);
        }

        public virtual IAsyncResult BeginReceive(byte[] buffer, int offset, int size, SocketFlags socketFlags, AsyncCallback callback, object state)
        {
            return _socket.BeginReceive(buffer, offset, size, socketFlags, callback, state);
        }

        public virtual int EndReceive(IAsyncResult asyncResult)
        {
            return _socket.EndReceive(asyncResult);
        }

        public virtual int Receive(byte[] buffer, int offset, int size, SocketFlags socketFlags)
        {
            return _socket.Receive(buffer, offset, size, socketFlags);
        }
    }
}
