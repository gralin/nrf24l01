using System.Threading;
using Microsoft.SPOT.Hardware;

namespace Gralin.NETMF.USBizi
{
    public class Led
    {
        private static OutputPort _led;
        private static Timer _ledTimer;

        public Led(Cpu.Pin pin)
        {
            _led = new OutputPort(pin, false);
        }

        public void BlinkOn(int period)
        {
            if (_ledTimer == null)
            {
                _ledTimer = new Timer(s => _led.Write(!_led.Read()), null, 0, period);
            }
            else
            {
                _ledTimer.Change(0, period);
            }
        }

        public void BlinkOff()
        {
            if (_ledTimer != null)
                _ledTimer.Dispose();
        }

        public void On()
        {
            _led.Write(true);
        }

        public void Off()
        {
            _led.Write(false);
        }
    }
}