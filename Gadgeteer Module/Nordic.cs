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
            Api.OnDataReceived += OnDataReceived;
            Api.OnTransmitFailed += OnTransmitFailed;
            Api.OnTransmitSuccess += OnTransmitSuccess;
        }

        /// <summary>
        ///   Gets a value indicating whether module is enabled (RX or TX mode).
        /// </summary>
        public bool IsEnabled
        {
            get { return Api.IsEnabled; }
        }

        /// <summary>
        /// Configure the module basic settings. Module needs to be initiaized.
        /// </summary>
        /// <param name="address">RF address (3-5 bytes). The width of this address determins the width of all addresses used for sending/receiving.</param>
        /// <param name="channel">RF channel (0-127)</param>
        public void Configure(byte[] address, byte channel)
        {
            Api.Configure(address, channel);
        }

        /// <summary>
        ///   Enables the module
        /// </summary>
        public void Enable()
        {
            Api.Enable();
        }

        /// <summary>
        ///   Disables the module
        /// </summary>
        public void Disable()
        {
            Api.Disable();
        }

        /// <summary>
        ///   Reads the current rf channel value set in module
        /// </summary>
        /// <returns></returns>
        public byte GetChannel()
        {
            return Api.GetChannel();
        }

        /// <summary>
        ///   Gets the module radio frequency [MHz]
        /// </summary>
        /// <returns>Frequency in MHz</returns>
        public int GetFrequency()
        {
            return Api.GetFrequency();
        }

        /// <summary>
        ///   Send <param name = "bytes">bytes</param> to given <param name = "address">address</param>
        ///   This is a non blocking method.
        /// </summary>
        public void SendTo(byte[] address, byte[] bytes)
        {
            Api.SendTo(address, bytes);
        }

        /// <summary>
        ///   Sends <param name = "bytes">bytes</param> to given <param name = "address">address</param>
        ///   This is a blocking method that returns true if data was received by the recipient or false if timeout occured.
        /// </summary>
        public bool SendTo(byte[] address, byte[] bytes, int timeout)
        {
            return Api.SendTo(address, bytes, timeout);
        }

        #region DataReceived event

        /// <summary>
        ///   Occurs when data packet has been received
        /// </summary>
        public event NRF24L01Plus.OnDataRecievedHandler DataReceived;
        private NRF24L01Plus.OnDataRecievedHandler _onDataReceived;

        /// <summary>
        /// Raises the <see cref="DataReceived"/> event.
        /// </summary>
        /// <param name="data">Received data bytes.</param>  
        protected virtual void OnDataReceived(byte[] data)
        {
            if (_onDataReceived == null) _onDataReceived = OnDataReceived;
            if (Program.CheckAndInvoke(DataReceived, _onDataReceived, data))
                DataReceived(data);
        }

        #endregion

        #region TransmitSuccess event

        /// <summary>
        ///   Occurs when ack has been received for send packet
        /// </summary>
        public event NRF24L01Plus.EventHandler TransmitSuccess;
        private NRF24L01Plus.EventHandler _onTransmitSuccess;

        /// <summary>
        /// Raises the <see cref="TransmitSuccess"/> event.
        /// </summary>
        protected virtual void OnTransmitSuccess()
        {
            if (_onTransmitSuccess == null) _onTransmitSuccess = OnTransmitSuccess;
            if (Program.CheckAndInvoke(TransmitSuccess, _onTransmitSuccess))
                TransmitSuccess();
        }

        #endregion

        #region TransmitFailed event

        /// <summary>
        ///   Occurs when no ack has been received for send packet
        /// </summary>
        public event NRF24L01Plus.EventHandler TransmitFailed;
        private NRF24L01Plus.EventHandler _onTransmitFailed;

        /// <summary>
        /// Raises the <see cref="TransmitFailed"/> event.
        /// </summary>
        protected virtual void OnTransmitFailed()
        {
            if (_onTransmitFailed == null) _onTransmitFailed = OnTransmitFailed;
            if (Program.CheckAndInvoke(TransmitFailed, _onTransmitFailed))
                TransmitFailed();
        }

        #endregion
    }
}
