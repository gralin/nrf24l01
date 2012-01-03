using System;
using System.Text;
using System.Threading;
using GHIElectronics.NETMF.Hardware;
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
        private byte _token;
        private byte _delay;
        private byte[] _myAddress;
        private byte[][] _otherBoards;
        private AnalogIn _variableResistor;

        public SampleApp()
        {
            Debug.EnableGCMessages(true);

            switch (SystemInfo.OEMString)
            {
                case "GHI Electronics, LLC":
                    switch ((Fez)SystemInfo.SystemID.Model)
                    {
                        case Fez.Mini:
                            _chipSelectPin = (Cpu.Pin)11; // FEZ_Pin.Digital.UEXT5
                            _interruptPin = (Cpu.Pin)43;  // FEZ_Pin.Interrupt.UEXT10
                            _chipEnablePin = (Cpu.Pin)1;  // FEZ_Pin.Digital.UEXT6
                            _spi = SPI.SPI_module.SPI1;
                            _led = new OutputPort((Cpu.Pin)28, false); // FEZ_Pin.Digital.An0
                            break;

                        case Fez.Domino:
                            _chipSelectPin = (Cpu.Pin)11; // FEZ_Pin.Digital.UEXT5
                            _interruptPin = (Cpu.Pin)12;  // FEZ_Pin.Interrupt.UEXT10
                            _chipEnablePin = (Cpu.Pin)1;  // FEZ_Pin.Digital.UEXT6
                            _spi = SPI.SPI_module.SPI2;
                            _led = new OutputPort((Cpu.Pin)20, false); // FEZ_Pin.Digital.Di0
                            break;

                        case Fez.Cobra:
                            _chipSelectPin = (Cpu.Pin)75; // FEZ_Pin.Digital.UEXT5
                            _interruptPin = (Cpu.Pin)26;  // FEZ_Pin.Interrupt.UEXT10
                            _chipEnablePin = (Cpu.Pin)48; // FEZ_Pin.Digital.UEXT6
                            _spi = SPI.SPI_module.SPI2;
                            _led = new OutputPort((Cpu.Pin)46, false); // FEZ_Pin.Digital.IO46
                            break;

                        default:
                            throw new NotSupportedException("Unknown board!");
                    }
                    break;

                case "Netduino Mini by Secret Labs LLC":
                    // this example will be extended to Netduino family
                    throw new NotSupportedException("Netduino mini is not yet supported!");

                default:
                    throw new NotSupportedException("Unknown board!");
            }

            _module = new NRF24L01Plus();
            _rand = new Random();
        }

        public void Run()
        {
            const byte channel = 10;

            // all addresses need to have the same length
            var fezDominoAddress = Encoding.UTF8.GetBytes("DOMIN");
            var fezMiniAddress   = Encoding.UTF8.GetBytes("MINI.");
            var fezCobraAddress  = Encoding.UTF8.GetBytes("COBRA");

            // here we determine on which device the code is running on
            switch (SystemInfo.OEMString)
            {
                case "GHI Electronics, LLC":
                    switch ((Fez)SystemInfo.SystemID.Model)
                    {
                        case Fez.Mini:
                            _myAddress = fezMiniAddress;
                            _otherBoards = new byte[2][];
                            _otherBoards[0] = fezDominoAddress;
                            _otherBoards[1] = fezCobraAddress;
                            break;

                        case Fez.Domino:
                            _myAddress = fezDominoAddress;
                            _otherBoards = new byte[2][];
                            _otherBoards[0] = fezCobraAddress;
                            _otherBoards[1] = fezMiniAddress;
                            _variableResistor = new AnalogIn(AnalogIn.Pin.Ain0);
                            _variableResistor.SetLinearScale(0,3300);
                            break;

                        case Fez.Cobra:
                            _myAddress = fezCobraAddress;
                            _otherBoards = new byte[2][];
                            _otherBoards[0] = fezMiniAddress;
                            _otherBoards[1] = fezDominoAddress;
                            break;
                    }
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

            // Fez Domino board is the one that starts the token passing and monitors if a token was lost
            // The timer checks each 10 sec if any token has been received since last check
            // If not than it might mean that a token was lost or never send - a new is created
            // A token may be lost if a board is reseted before sending token back
            if ((Fez)SystemInfo.SystemID.Model == Fez.Domino)
            {
                _timer = new Timer(CreateToken, null, new TimeSpan(0, 0, 0, 10), new TimeSpan(0, 0, 0, 10));
            }
        }

        private void CreateToken(object state)
        {
            if (DateTime.Now.Subtract(_lastActivity).Seconds < 10) return;
            _lastActivity = DateTime.Now;
            _token = 0;
            Send();
        }

        private void UpdateDelay()
        {
            // only Cobra has a variable resistor connected
            if ((Fez)SystemInfo.SystemID.Model == Fez.Domino)
            {
                _delay = (byte)(((_variableResistor.Read() - 1100) / 2200.0) * 255);
            }
        }

        private void OnSendSuccess()
        {
            _led.Write(false);
        }

        private void OnSendFailure()
        {
            Debug.Print("Send failed!");
            Send();
        }

        private void OnReceive(byte[] data)
        {
            Debug.Print("Token = " + data[0] + ", Delay = " + data[1]);
            _lastActivity = DateTime.Now;
            _token = (byte) (data[0] + 1);
            _delay = data[1];
            Send();
        }

        private void Send()
        {
            // update the delay value by reading the varaible resistor
            UpdateDelay();

            // uncomment this line for a random passing version
            //SendTokenToRandomBoard();

            SendTokenToNextBoard();
        }

        private void SendTokenToRandomBoard()
        {
            var nextIndex = _rand.Next(2);
            _led.Write(true);

            // delay added to see LED blink
            Thread.Sleep(_delay*2 + 100);

            _module.SendTo(_otherBoards[nextIndex], new[] { _token });
        }

        private void SendTokenToNextBoard()
        {
            _led.Write(true);

            // delay added to see LED blink
            // the _delay value is 0...255
            Thread.Sleep(_delay*4 + 20);

            _module.SendTo(_otherBoards[0], new[] { _token, _delay });
        }
    }
}