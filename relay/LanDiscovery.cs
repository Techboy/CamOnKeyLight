using System.Net;
using System.Net.Sockets;
using CamOnKeyLight.Core;

namespace CamOnKeyLight.Relay
{
    public static class LanDiscovery
    {
        private static readonly IPAddress Multicast = IPAddress.Parse("224.0.0.251");

        public static async Task<IReadOnlyList<DiscoveredLight>> QueryAsync(TimeSpan wait, CancellationToken cancellationToken)
        {
            using Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            socket.Bind(new IPEndPoint(IPAddress.Any, 0));
            socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.AddMembership, new MulticastOption(Multicast, IPAddress.Any));
            socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastTimeToLive, 1);
            byte[] query = MdnsCodec.CreateQuery(1, unicastResponse: false);
            await socket.SendToAsync(query, SocketFlags.None, new IPEndPoint(Multicast, 5353), cancellationToken);

            List<byte[]> packets = new List<byte[]>();
            using CancellationTokenSource waitSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            waitSource.CancelAfter(wait);
            byte[] buffer = new byte[9000];
            while (!waitSource.IsCancellationRequested)
            {
                try
                {
                    SocketReceiveFromResult received = await socket.ReceiveFromAsync(
                        buffer,
                        SocketFlags.None,
                        new IPEndPoint(IPAddress.Any, 0),
                        waitSource.Token);
                    byte[] packet = new byte[received.ReceivedBytes];
                    Buffer.BlockCopy(buffer, 0, packet, 0, received.ReceivedBytes);
                    packets.Add(packet);
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    break;
                }
            }

            return ElgatoDiscovery.FromPackets(packets);
        }
    }
}
