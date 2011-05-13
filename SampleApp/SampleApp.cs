using System;
using System.Threading;
using Microsoft.SPOT;
using Microsoft.SPOT.Hardware;

namespace Gralin.NETMF.Nordic
{
    /// <summary>
    ///   NRF24L01Plus Test Application
    /// </summary>
    public class SampleApp
    {
        private readonly Cpu.Pin _chipEnablePin;
        private readonly Cpu.Pin _chipSelectPin;
        private readonly Cpu.Pin _interruptPin;
        private readonly OutputPort _led;
        private readonly NRF24L01Plus _module;
        private readonly Random _rand;
        private readonly SPI.SPI_module _spi;
        private Timer _timer;
        private DateTime _lastActivity;
        private byte _currentValue;
        private byte[] _myAddress;
        private byte[][] _otherFez;

        public SampleApp()
        {
            Debug.EnableGCMessages(false);

            switch ((Fez) SystemInfo.SystemID.Model)
            {
                case Fez.Mini:
                    _chipSelectPin = (Cpu.Pin) 11; // FEZ_Pin.Digital.UEXT5
                    _interruptPin = (Cpu.Pin) 43;  // FEZ_Pin.Interrupt.UEXT10
                    _chipEnablePin = (Cpu.Pin) 1;  // FEZ_Pin.Digital.UEXT6
                    _spi = SPI.SPI_module.SPI1;
                    _led = new OutputPort((Cpu.Pin) 4, false);
                    break;

                case Fez.Domino:
                    _chipSelectPin = (Cpu.Pin) 11; // FEZ_Pin.Digital.UEXT5
                    _interruptPin = (Cpu.Pin) 12;  // FEZ_Pin.Interrupt.UEXT10
                    _chipEnablePin = (Cpu.Pin) 1;  // FEZ_Pin.Digital.UEXT6
                    _spi = SPI.SPI_module.SPI2;
                    _led = new OutputPort((Cpu.Pin) 4, false);
                    break;

                case Fez.Cobra:
                    _chipSelectPin = (Cpu.Pin) 75; // FEZ_Pin.Digital.UEXT5
                    _interruptPin = (Cpu.Pin) 26;  // FEZ_Pin.Interrupt.UEXT10
                    _chipEnablePin = (Cpu.Pin) 48; // FEZ_Pin.Digital.UEXT6
                    _spi = SPI.SPI_module.SPI2;
                    _led = new OutputPort((Cpu.Pin) 46, false);
                    break;

                default:
                    throw new NotSupportedException("Need to add SPI configuration for this device");
            }

            _module = new NRF24L01Plus();
            _rand = new Random();
        }

        public void Run()
        {
            const byte channel = 1;
            var dominoAddress = new byte[] {0xEE, 0xDD, 0xCC, 0xBB, 0xAA};
            var miniAddress = new byte[] {0xAA, 0xBB, 0xCC, 0xDD, 0xEE};
            var cobraAddress = new byte[] {0xCC, 0xEE, 0xBB, 0xDD, 0xAA};

            switch ((Fez) SystemInfo.SystemID.Model)
            {
                case Fez.Mini:
                    _myAddress = miniAddress;
                    _otherFez = new byte[2][];
                    _otherFez[0] = dominoAddress;
                    _otherFez[1] = cobraAddress;
                    break;

                case Fez.Domino:
                    _myAddress = dominoAddress;
                    _otherFez = new byte[2][];
                    _otherFez[0] = cobraAddress;
                    _otherFez[1] = miniAddress;
                    break;

                case Fez.Cobra:
                    _myAddress = cobraAddress;
                    _otherFez = new byte[2][];
                    _otherFez[0] = miniAddress;
                    _otherFez[1] = dominoAddress;
                    break;
            }

            _module.OnDataReceived += OnReceive;
            _module.OnTransmitFailed += OnSendFailure;
            _module.OnTransmitSuccess += OnSendSuccess;

            _module.Initialize(_spi, _chipSelectPin, _chipEnablePin, _interruptPin);
            _module.Configure(_myAddress, channel);
            _module.Enable();

            _lastActivity = DateTime.MinValue;

            if ((Fez) SystemInfo.SystemID.Model == Fez.Domino)
            {
                _timer = new Timer(StartSending, null, new TimeSpan(0,0,0,5), new TimeSpan(0,0,0,5));
            }
        }

        private void StartSending(object state)
        {
            if (DateTime.Now.Subtract(_lastActivity).Seconds <= 4) return;
            _lastActivity = DateTime.Now;
            SendToRandomFez();
        }

        private void OnSendSuccess()
        {
            //Debug.Print("OK");
            _led.Write(false);
        }

        private void OnSendFailure()
        {
            //Debug.Print("ERROR");
            SendToRandomFez();
        }

        private void OnReceive(byte[] data)
        {
            Debug.Print(data[0].ToString());
            _lastActivity = DateTime.Now;
            _currentValue = (byte) (data[0] + 1);
            SendToRandomFez();
        }

        private void SendToRandomFez()
        {
            var nextIndex = _rand.Next(2);
            _led.Write(true);

            Thread.Sleep(1000);
            _module.SendTo(_otherFez[nextIndex], new[] { _currentValue });
        }
    }
}