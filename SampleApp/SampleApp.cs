using System;
using System.Text;
using System.Threading;
using Microsoft.SPOT;
using Microsoft.SPOT.Hardware;

namespace Gralin.NETMF.Nordic
{
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

                    // IO46 pin is used instead of Cobra onboard led
                    // because the led is connected in parallel with chipEnablePin
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
            const byte channel = 10;

            // all addresses need to have the same length
            var dominoAddress = Encoding.UTF8.GetBytes("DOMIN");
            var miniAddress   = Encoding.UTF8.GetBytes("MINI.");
            var cobraAddress  = Encoding.UTF8.GetBytes("COBRA");

            // here we determine on which device the code is running on
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

            // here we attatch event listener
            _module.OnDataReceived += OnReceive;
            _module.OnTransmitFailed += OnSendFailure;
            _module.OnTransmitSuccess += OnSendSuccess;

            // we nned to call Initialize() and Configure() befeore we start using the module
            _module.Initialize(_spi, _chipSelectPin, _chipEnablePin, _interruptPin);
            _module.Configure(_myAddress, channel);

            // to start receiveing we need to call Enable(), call Disable() to stop/pause
            _module.Enable();


            // example of reading your own address
            var myAddress = _module.GetAddress(AddressSlot.Zero, 5);
            Debug.Print("I am " + new string(Encoding.UTF8.GetChars(myAddress)));

            _lastActivity = DateTime.MinValue;

            // Domino board is the one that starts the token passing and monitors if a token was lost
            // The timer checks each 10 sec if any token has been received since last check
            // If not than it might mean that a token was lost or never send - a new is created
            // A token may be lost if a board is reseted before sending token back
            if ((Fez) SystemInfo.SystemID.Model == Fez.Domino)
            {
                _timer = new Timer(CreateToken, null, new TimeSpan(0, 0, 0, 10), new TimeSpan(0, 0, 0, 10));
            }
        }

        private void CreateToken(object state)
        {
            if (DateTime.Now.Subtract(_lastActivity).Seconds < 10) return;
            _lastActivity = DateTime.Now;
            SendTokenToFez();
        }

        private void OnSendSuccess()
        {
            _led.Write(false);
        }

        private void OnSendFailure()
        {
            SendTokenToFez();
        }

        private void OnReceive(byte[] data)
        {
            Debug.Print(data[0].ToString());
            _lastActivity = DateTime.Now;
            _currentValue = (byte) (data[0] + 1);
            SendTokenToFez();
        }

        private void SendTokenToFez()
        {
            //SendTokenToRandomFez();
            SendTokenToNextFez();
        }

        private void SendTokenToRandomFez()
        {
            var nextIndex = _rand.Next(2);
            _led.Write(true);

            // delay added to see LED blink
            Thread.Sleep(1000);

            _module.SendTo(_otherFez[nextIndex], new[] { _currentValue });
        }

        private void SendTokenToNextFez()
        {
            _led.Write(true);

            // delay added to see LED blink
            Thread.Sleep(1000);

            _module.SendTo(_otherFez[0], new[] { _currentValue });
        }
    }
}