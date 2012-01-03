using System;
using System.Text;
using System.Threading;
using GHIElectronics.NETMF.System;
using Gralin.NETMF.Nordic;
using Microsoft.SPOT;
using Microsoft.SPOT.Hardware;

namespace Gralin.NETMF.USBizi
{
    public class Program
    {
        // values adjusted for FEZ Domino board
        const Cpu.Pin LedPin = (Cpu.Pin)4;  // FEZ_Pin.Digital.LED
        const Cpu.Pin LdrPin = (Cpu.Pin)0;  // FEZ_Pin.Digital.LDR

        // values adjusted for FEZ Domino UEXT connector
        const SPI.SPI_module Spi = SPI.SPI_module.SPI2;
        const Cpu.Pin ChipSelectPin = (Cpu.Pin)11;  // FEZ_Pin.Digital.UEXT5
        const Cpu.Pin InterruptPin = (Cpu.Pin)12;   // FEZ_Pin.Interrupt.UEXT10
        const Cpu.Pin ChipEnablePin = (Cpu.Pin)1;   // FEZ_Pin.Digital.UEXT6

        // sample values independent of board
        const byte Channel = 10;
        const string Address = "BOOTL";

        static Led _led;
        static InterruptPort _ldrButton;
        static Bootloader _bootloader;
        static Timer _watchdog;
        static NRF24L01Plus _radio;
        static DateTime _updateStartTime;

        public static void Main()
        {
            if (SystemUpdate.GetMode() != SystemUpdate.SystemUpdateMode.Bootloader)
                throw new InvalidOperationException("We must be in bootloader mode!");

            Debug.EnableGCMessages(false);

            InitializeLed();
            InitializeLoaderButton();
            InitializeBootloader();
            InitializeWatchdog();
            InitializeRadio();

            Debug.Print("Waiting for data");
            Thread.Sleep(Timeout.Infinite);
        }

        private static void InitializeLed()
        {
            Debug.Print("Initializing led...");
            _led = new Led(LedPin);
            _led.Off();
        }

        private static void InitializeLoaderButton()
        {
            Debug.Print("Initializing loader button...");
            _ldrButton = new InterruptPort(LdrPin, true, Port.ResistorMode.PullUp, Port.InterruptMode.InterruptEdgeLow);
            _ldrButton.OnInterrupt += (d1, d2, t) => SystemUpdate.AccessApplication();
        }

        private static void InitializeBootloader()
        {
            Debug.Print("Initializing bootloader...");
            _bootloader = new Bootloader();

            _bootloader.OnStart += () =>
            {
                Debug.Print("Application update started");
                _led.BlinkOn(100);
                _updateStartTime = DateTime.Now;
            };

            _bootloader.OnFinish += () =>
            {
                Debug.Print("Application update finished in " + DateTime.Now.Subtract(_updateStartTime));
                _led.BlinkOff();
                _led.On();
            };
        }

        private static void InitializeWatchdog()
        {
            _watchdog = new Timer(s =>
            {
                if (_bootloader.Started)
                {
                    Debug.Print(_bootloader.CurrentProgress + "%");
                    _watchdog.Change(5000, -1);
                }
                else
                {
                    Debug.Print("Exiting bootloader");
                    _watchdog.Dispose();
                    _radio.Disable();
                    _bootloader.Stop();
                    _led.BlinkOff();
                    _led.Off();
                    SystemUpdate.AccessApplication();
                }
            },
            null, 5000, -1);
        }

        private static void InitializeRadio()
        {
            Debug.Print("Initializing radio...");
            _radio = new NRF24L01Plus();
            _radio.Initialize(Spi, ChipSelectPin, ChipEnablePin, InterruptPin);
            _radio.Configure(Encoding.UTF8.GetBytes(Address), Channel);
            _radio.OnDataReceived += data => _bootloader.Process(data);
            _radio.Enable();
        }
    }
}
