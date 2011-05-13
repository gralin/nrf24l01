using System;
using Microsoft.SPOT;
using Microsoft.SPOT.Hardware;
using System.Threading;

namespace Gralin.NETMF.Nordic
{
    public class NRF24L01Plus
    {
        #region Delegates

        public delegate void OnDataRecievedHandler(byte[] data);

        public delegate void OnInterruptHandler(Status status);

        public delegate void EventHandler();

        #endregion

        private static readonly byte[] EmptyAddress = new byte[5];
        private OutputPort _cePin;
        private bool _initialized;
        private InterruptPort _irqPin;
        private SPI _spiPort;

        /// <summary>
        /// Gets a value indicating whether module is in RX or TX mode.
        /// </summary>
        public bool IsEnabled { get { return _cePin.Read(); } }

        /// <summary>
        ///   Enable RX/TX by setting CE
        /// </summary>
        public void Enable()
        {
            _irqPin.EnableInterrupt();
            _cePin.Write(true);
        }

        /// <summary>
        ///   Disable RX/TX by clearing CE
        /// </summary>
        public void Disable()
        {
            _cePin.Write(false);
            _irqPin.DisableInterrupt();
        }

        /// <summary>
        ///   Open Pins and SPI port for NRF24L01Plus.
        /// </summary>
        public void Initialize(SPI.SPI_module spi, Cpu.Pin chipSelectPin, Cpu.Pin chipEnablePin, Cpu.Pin interruptPin)
        {
            // Chip Select : Active Low
            // Clock : Active High, Data clocked in on rising edge
            _spiPort = new SPI(new SPI.Configuration(chipSelectPin, false, 0, 0, false, false, 2000, spi));

            // Initialize IRQ Port
            _irqPin = new InterruptPort(interruptPin, false, Port.ResistorMode.PullUp,
                                        Port.InterruptMode.InterruptEdgeLow);
            _irqPin.OnInterrupt += HandleInterrupt;

            // Initialize Chip Enable Port
            _cePin = new OutputPort(chipEnablePin, false);

            // Module reset time
            Thread.Sleep(100);

            _initialized = true;
        }

        /// <summary>
        ///   Configure the nRF24L01 module basic settings
        /// </summary>
        public void Configure(byte[] address, byte channel)
        {
            CheckIsInitialized();

            // Set radio channel
            Execute(Commands.W_REGISTER, Registers.RF_CH,
                    new[]
                        {
                            (byte) (channel & 0x7F)
                        });

            // Enable dynamic payload length
            Execute(Commands.W_REGISTER, Registers.FEATURE,
                    new[]
                        {
                            (byte) (1 << Bits.EN_DPL)
                        });

            // Set auto-ack
            Execute(Commands.W_REGISTER, Registers.EN_AA,
                    new[]
                        {
                            (byte) (1 << Bits.ENAA_P0 |
                                    1 << Bits.ENAA_P1)
                        });

            // Set dynamic payload length for pipes
            Execute(Commands.W_REGISTER, Registers.DYNPD,
                    new[]
                        {
                            (byte) (1 << Bits.DPL_P0 |
                                    1 << Bits.DPL_P1)
                        });

            // Flush RX FIFO
            Execute(Commands.FLUSH_RX, 0x00, new byte[0]);

            // Flush TX FIFO
            Execute(Commands.FLUSH_TX, 0x00, new byte[0]);

            // Clear IRQ Masks
            Execute(Commands.W_REGISTER, Registers.STATUS,
                    new[]
                        {
                            (byte) (1 << Bits.MASK_RX_DR |
                                    1 << Bits.MASK_TX_DS |
                                    1 << Bits.MAX_RT)
                        });

            // Set module address
            Execute(Commands.W_REGISTER, Registers.RX_ADDR_P1, address);

            // Setup, CRC enabled, Power Up, PRX
            SetReceiveMode();
        }

        /// <summary>
        ///   Executes a command in NRF24L01 (for details see module datasheet)
        /// </summary>
        /// <param name = "command">Command</param>
        /// <param name = "addres">Addres/Register to write to</param>
        /// <param name = "data">Data to write</param>
        /// <returns>Byte array with sizeof(Data)+1 rows. row 0 is status register from NRF24L01Plus</returns>
        public byte[] Execute(byte command, byte addres, byte[] data)
        {
            CheckIsInitialized();

            var wasEnabled = IsEnabled;

            // This command requires module to be in power down od standby mode
            if (command == Commands.W_REGISTER)
            {
                Disable();   
            }

            // Create SPI Buffers with Size of Data + 1 (For Command)
            var writeBuffer = new byte[data.Length + 1];
            var readBuffer = new byte[data.Length + 1];

            // Add command and adres to SPI buffer
            writeBuffer[0] = (byte) (command | addres);

            // Add data to SPI buffer
            Array.Copy(data, 0, writeBuffer, 1, data.Length);

            // Do SPI Read/Write
            _spiPort.WriteRead(writeBuffer, readBuffer);


            // Enable module back if it was disabled
            if (command == Commands.W_REGISTER && wasEnabled)
            {
                Enable();
            }

            // Return ReadBuffer
            return readBuffer;
        }

        /// <summary>
        /// Gets module basic status information
        /// </summary>
        public Status GetStatus()
        {
            CheckIsInitialized();

            var readBuffer = new byte[1];
            _spiPort.WriteRead(new[] { Commands.NOP }, readBuffer);

            return new Status(readBuffer[0]);
        }

        /// <summary>
        ///   Send <param name = "bytes">bytes</param> to given <param name = "address">address</param>
        /// </summary>
        public void SendTo(byte[] address, byte[] bytes)
        {
            // Chip enable low
            Disable();

            // Setup PTX (Primary TX)
            SetTransmitMode();

            // Write transmit adres to TX_ADDR register. 
            Execute(Commands.W_REGISTER, Registers.TX_ADDR, address);

            // Write transmit adres to RX_ADDRESS_P0 (Pipe0) (For Auto ACK)
            Execute(Commands.W_REGISTER, Registers.RX_ADDR_P0, address);

            // Send payload
            Execute(Commands.W_TX_PAYLOAD, 0x00, bytes);

            // Pulse for CE -> starts the transmission.
            Enable();
        }

        private void CheckIsInitialized()
        {
            if (!_initialized)
            {
                throw new InvalidOperationException("Initialize method needs to be called before configuring the module");
            }
        }

        private void HandleInterrupt(uint data1, uint data2, DateTime dateTime)
        {
            if (!_initialized)
            {
                return;
            }

            // Disable RX/TX
            Disable();

            // Set PRX
            SetReceiveMode();

            var status = GetStatus();
            var payloads = new byte[3][];
            byte payloadCount = 0;
            var payloadCorrupted = false;

            // Trigger event
            OnInterrupt(status);

            if (status.DataReady)
            {
                while (!status.RxEmpty)
                {
                    // Read payload size
                    var payloadLength = Execute(Commands.R_RX_PL_WID, 0x00, new byte[1]);

                    // this indicates corrupted data
                    if (payloadLength[1] > 32)
                    {
                        payloadCorrupted = true;

                        // Flush anything that remains in buffer
                        Execute(Commands.FLUSH_RX, 0x00, new byte[0]);
                    }
                    else
                    {
                        // Read payload data
                        payloads[payloadCount] = Execute(Commands.R_RX_PAYLOAD, 0x00, new byte[payloadLength[1]]);
                        payloadCount++;   
                    }

                    // Clear RX_DR bit 
                    var result = Execute(Commands.W_REGISTER, Registers.STATUS, new[] { (byte)(1 << Bits.RX_DR) });
                    status.Update(result[0]);
                }
            }

            if (status.ResendLimitReached)
            {
                // Flush TX FIFO 
                Execute(Commands.FLUSH_TX, 0x00, new byte[0]);

                // Clear MAX_RT bit in status register
                Execute(Commands.W_REGISTER, Registers.STATUS, new[] {(byte) (1 << Bits.MAX_RT)});
            }

            if (status.TxFull)
            {
                // Flush TX FIFO 
                Execute(Commands.FLUSH_TX, 0x00, new byte[0]);
            }

            if (status.DataSent)
            {
                // Clear TX_DS bit in status register
                Execute(Commands.W_REGISTER, Registers.STATUS, new[] {(byte) (1 << Bits.TX_DS)});
            }

            // Enable RX
            Enable();

            if (payloadCorrupted)
            {
                Debug.Print("Corrupted data received");
            }
            else if (payloadCount > 0)
            {
                for (var i = 0; i < payloadCount; i++)
                {
                    var payload = payloads[i];
                    var payloadWithoutCommand = new byte[payload.Length - 1];
                    Array.Copy(payload, 1, payloadWithoutCommand, 0, payload.Length - 1);
                    OnDataReceived(payloadWithoutCommand);   
                }
            } 
            else if (status.DataSent)
            {
                OnTransmitSuccess();
            }
            else
            {
                OnTransmitFailed();
            }
        }

        private void SetTransmitMode()
        {
            Execute(Commands.W_REGISTER, Registers.CONFIG,
                    new[]
                        {
                            (byte) (1 << Bits.PWR_UP |
                                    1 << Bits.CRCO)
                        });
        }

        private void SetReceiveMode()
        {
            // Clear the ack address
            Execute(Commands.W_REGISTER, Registers.RX_ADDR_P0, EmptyAddress);

            Execute(Commands.W_REGISTER, Registers.CONFIG,
                    new[]
                        {
                            (byte) (1 << Bits.PWR_UP |
                                    1 << Bits.CRCO |
                                    1 << Bits.PRIM_RX)
                        });
        }

        /// <summary>
        ///   Event on nRF24L01 IRQ. Called before OnRecieve and OnTransmitSuccess. 
        ///   You can set StopDefaultInterrupt to true to stop the default interrupt 
        ///   from processing. You should ensure that the IRQ mask is correctly reset.
        /// </summary>
        public event OnInterruptHandler OnInterrupt = delegate { };

        /// <summary>
        ///   Occurs when data packet has been received
        /// </summary>
        public event OnDataRecievedHandler OnDataReceived = delegate { };

        /// <summary>
        ///   Occurs when ack has been received for send packet
        /// </summary>
        public event EventHandler OnTransmitSuccess = delegate { };

        /// <summary>
        ///   Occurs when no ack has been received for send packet
        /// </summary>
        public event EventHandler OnTransmitFailed = delegate { };
    }
}