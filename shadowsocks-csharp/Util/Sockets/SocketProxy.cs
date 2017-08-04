using System.Net;
using System.Net.Sockets;

namespace Shadowsocks.Util.Sockets
{
    public class SocketProxy
    {
        private Socket _socket;
        public virtual ProtocolType ProtocolType => _socket.ProtocolType;
        public virtual EndPoint LocalEndPoint => _socket.LocalEndPoint;
        
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
    }
}
