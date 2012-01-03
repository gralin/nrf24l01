using System.Collections;
using System.Text;
using System.Threading;
using GHIElectronics.NETMF.System;

namespace Gralin.NETMF.USBizi
{
    public class Bootloader
    {
        public delegate void EventHandler();

        public const int ApplicationAddress = 0x00058000;
        public const int SrecLineLength = 16;
        public const uint ApplicationSize = 96 * 1024;
        public const string LinePrefix = "S315";

        readonly Queue _processQueue;
        readonly Thread _processThread;
        bool _processThreadTerminate;

        public bool Started { get; private set; }
        public bool Finished { get; private set; }
        public int CurrentAddress { get; private set; }

        public int CurrentProgress
        {
            get
            {
                return (int) (((CurrentAddress - ApplicationAddress) / (float)ApplicationSize) * 100);
            }
        }

        public int ReceivedPacketCount
        {
            get { return (CurrentAddress - ApplicationAddress) / SrecLineLength; }
        }

        public Bootloader()
        {
            CurrentAddress = ApplicationAddress;
            _processQueue = new Queue();
            _processThread = new Thread(ProcessData);
            _processThread.Start();
        }

        public void Process(byte[] data)
        {
            _processQueue.Enqueue(data);
        }

        public void Stop()
        {
            _processThreadTerminate = true;
            //_processThread.Join(1000);
        }

        private void ProcessData()
        {
            while (!_processThreadTerminate)
            {
                while (_processQueue.Count > 0)
                {
                    if (_processThreadTerminate)
                        return;

                    var data = (byte[])_processQueue.Dequeue();

                    if (data.Length != 2 + SrecLineLength)
                        continue;

                    var packetNumber = (data[0] << 8) + data[1];

                    switch (packetNumber)
                    {
                        case 0:
                            StartUpdate();
                            WriteSingleLine(data);
                            break;
                        case 0xFFFF:
                            ClearRemainingLines();
                            EndUpdate();
                            break;
                        default:
                            WriteSingleLine(data);
                            if (CurrentProgress == 100)
                                EndUpdate();
                            break;
                    }
                }

                Thread.Sleep(50);
            }
        }

        private void StartUpdate()
        {
            if (!Started)
                SystemUpdate.ApplicationUpdate.Start();
            
            Started = true;
            Finished = false;
            CurrentAddress = ApplicationAddress;
            OnStart();
        }

        private void WriteSingleLine(byte[] data)
        {
            WriteData(CurrentAddress, data, 2);
            CurrentAddress += SrecLineLength;
        }

        private void ClearRemainingLines()
        {
            var emptyLine = new byte[SrecLineLength];

            for (var i = 0; i < SrecLineLength; i++)
                emptyLine[i] = 0xFF;

            while (CurrentProgress < 100)
            {
                WriteData(CurrentAddress, emptyLine, 0);
                CurrentAddress += SrecLineLength;
            }
        }

        private void EndUpdate()
        {
            SystemUpdate.ApplicationUpdate.End();
            Started = false;
            Finished = true;
            OnFinish();
        }

        private static void WriteData(int address, byte[] bytes, int offset)
        {
            var dataStr = LinePrefix + HexHelper.ToHex(address);

            byte cs = 0x15;
            for (var i = 0; i < 4; i++)
                cs += (byte)(address >> (8 * i));

            for (var i = 0; i < SrecLineLength; i++)
            {
                cs += bytes[offset + i];
                dataStr += HexHelper.ToHex(bytes[offset + i]);
            }

            dataStr += HexHelper.ToHex((byte)~cs);
            var data = Encoding.UTF8.GetBytes(dataStr);
            SystemUpdate.ApplicationUpdate.Write(data, 0, data.Length);
        }

        public event EventHandler OnStart = delegate { };

        public event EventHandler OnFinish = delegate { };
    }
}
