using System;
using System.IO;
using System.Text;
using System.Threading;
using GHIElectronics.NETMF.IO;
using Gralin.NETMF.Nordic;
using Microsoft.SPOT;
using Microsoft.SPOT.Hardware;
using Microsoft.SPOT.IO;

namespace Gralin.NETMF
{
    public class Program
    {
        // values adjusted for FEZ Rhino board
        const Cpu.Pin LedPin = (Cpu.Pin)65;  // FEZ_Pin.Digital.LED
        const Cpu.Pin LdrPin = (Cpu.Pin)0;  // FEZ_Pin.Digital.LDR

        // values adjusted for FEZ Rhino UEXT connector
        const SPI.SPI_module Spi = SPI.SPI_module.SPI2;
        const Cpu.Pin ChipSelectPin = (Cpu.Pin)11;  // FEZ_Pin.Digital.UEXT5
        const Cpu.Pin InterruptPin = (Cpu.Pin)12;   // FEZ_Pin.Interrupt.UEXT10
        const Cpu.Pin ChipEnablePin = (Cpu.Pin)1;   // FEZ_Pin.Digital.UEXT6

        // sample values independent of board
        const byte Channel = 10;
        const string Address = "RHINO";

        const string FileName = "1.hex";
        const uint ApplicationSize = 96 * 1024;
        const int SrecLineLength = 16;

        static readonly byte[] TargetAddress = Encoding.UTF8.GetBytes("BOOTL");

        static OutputPort _led;
        static NRF24L01Plus _radio;
        static bool _sending;
        static object _sendLock;

        public static void Main()
        {
            Debug.EnableGCMessages(false);
            InitializeLed();
            InitializeRadio();

            _sendLock = new object();
            _sending = false;

            var sd = new PersistentStorage("SD");
            sd.MountFileSystem();

            // this is very important to run the button interrupt handler in a new thread
            // if you run the handler in the interrupt thread than you will not get the driver events (e.g. OnTransmitSuccess)
            // the driver events are based on i/o interrupts as well and net mf doesn't seem to allow nested i/o interrupts
            var ldrButton = new InterruptPort(LdrPin, true, Port.ResistorMode.PullUp, Port.InterruptMode.InterruptEdgeLow);
            ldrButton.OnInterrupt += (d1, d2, t) => new Thread(OnButtonClick).Start();

            Thread.Sleep(Timeout.Infinite);
        }

        private static void OnButtonClick()
        {
            lock (_sendLock)
            {
                if (_sending)
                    return;

                _sending = true;   
            }

            try
            {
                _led.Write(true);

                var volume = VolumeInfo.GetVolumes()[0];

                if (volume.IsFormatted)
                {
                    var filePath = volume.RootDirectory + "\\" + FileName;

                    if (File.Exists(filePath))
                    {
                        SendFirmware(filePath);
                    }
                    else
                    {
                        Debug.Print(filePath + " was not found!");
                    }
                }
                else
                {
                    Debug.Print("Storage is not formatted");
                }
            }
            finally
            {
                _sending = false;
                _led.Write(false);
            }
        }

        private static void SendFirmware(string filePath)
        {
            var startTime = DateTime.Now;
            var packetCounter = 0;
            var sendCompleted = false;
            Debug.Print("Uploading data...");

            using (var file = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            {
                var buffer = new byte[2 + SrecLineLength];

                for (var i = 0; i < ApplicationSize; i += SrecLineLength)
                {
                    buffer[0] = (byte)(packetCounter >> 8);
                    buffer[1] = (byte)packetCounter;

                    var length = file.Read(buffer, 2, SrecLineLength);
                    if (length <= 0)
                    {
                        Debug.Print("Unexpected end of file!");
                        return;
                    }

                    if (IsLineEmpty(buffer, 2))
                    {
                        buffer[0] = 0xFF;
                        buffer[1] = 0xFF;
                        sendCompleted = true;
                    }

                    if (!_radio.SendTo(TargetAddress, buffer, 1000))
                    {
                        Debug.Print("Failed to send packet nr " + packetCounter);
                        return;
                    }

                    ToggleLed();
                    packetCounter++;

                    if (packetCounter % 100 == 0)
                        Debug.Print("Packets sent: " + packetCounter);

                    Thread.Sleep(10);

                    if (sendCompleted)
                        break;
                }
            }

            Debug.Print("Upload of " + packetCounter + " packets took " + DateTime.Now.Subtract(startTime));
        }

        private static bool IsLineEmpty(byte[] lineBytes, int offset)
        {
            var result = true;
            for (var i = offset; i < lineBytes.Length; i++)
                result &= lineBytes[i] == 0xFF;
            return result;
        }

        private static void InitializeLed()
        {
            _led = new OutputPort(LedPin, true);
        }

        private static void ToggleLed()
        {
            _led.Write(!_led.Read());
        }

        private static void InitializeRadio()
        {
            _radio = new NRF24L01Plus();
            _radio.Initialize(Spi, ChipSelectPin, ChipEnablePin, InterruptPin);
            _radio.Configure(Encoding.UTF8.GetBytes(Address), Channel);
            _radio.Enable();
        }
    }
}
