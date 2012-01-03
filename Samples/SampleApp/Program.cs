using System.Threading;

namespace Gralin.NETMF.Nordic
{
    public class Program
    {
        public static void Main()
        {
            var app = new SampleApp();
            app.Run();
            Thread.Sleep(Timeout.Infinite);
        }
    }
}