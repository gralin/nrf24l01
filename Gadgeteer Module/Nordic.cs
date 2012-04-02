using Gralin.NETMF.Nordic;
using GTM = Gadgeteer.Modules;

namespace Gadgeteer.Modules.Gralin
{
    /// <summary>
    /// A Nordic module for Microsoft .NET Gadgeteer
    /// </summary>
    public class Nordic : Module
    {
        /// <summary>
        /// Module API.
        /// </summary>
        public NRF24L01Plus Api { get; protected set; }

        /// <summary>
        /// Gadgeteer Nordic NRF24L01+ module.
        /// </summary>
        /// <param name="socketNumber">The socket that this module is plugged in to.</param>
        public Nordic(int socketNumber)
        {
            var socket = Socket.GetSocket(socketNumber, true, this, null);

            Api = new NRF24L01Plus();
            Api.Initialize(socket.SPIModule, socket.CpuPins[6], socket.CpuPins[4],socket.CpuPins[3]);
        }
    }
}
