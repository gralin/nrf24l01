using System;
using System.Threading;
using GHIElectronics.NETMF.System;
using Microsoft.SPOT;
using Microsoft.SPOT.Hardware;

namespace Gralin.NETMF
{
    public class Program
    {
        // values adjusted for FEZ Domino board
        const Cpu.Pin LedPin = (Cpu.Pin)4;  // FEZ_Pin.Digital.LED
        const Cpu.Pin LdrPin = (Cpu.Pin)0;  // FEZ_Pin.Digital.LDR

        private static Led _led;
        private static InterruptPort _ldrButton;

        public static void Main()
        {
            switch (SystemUpdate.GetMode())
            {
                case SystemUpdate.SystemUpdateMode.NonFormatted:
                    SystemUpdate.EnableBootloader();
                    break;

                case SystemUpdate.SystemUpdateMode.Bootloader:
                    throw new InvalidOperationException("We must be in application mode!");
            }

            SystemUpdate.AlwaysRunBootloader(true);

            _led = new Led(LedPin);

            Debug.Print("App v1.0");
            _led.BlinkOn(1500);

            //Debug.Print("App v2.0");
            //_led.BlinkOn(500);

            _ldrButton = new InterruptPort(LdrPin, true, Port.ResistorMode.PullUp, Port.InterruptMode.InterruptEdgeLow);
            _ldrButton.OnInterrupt += (d1, d2, t) => SystemUpdate.AccessBootloader();

            Thread.Sleep(Timeout.Infinite);
        }
    }
}
