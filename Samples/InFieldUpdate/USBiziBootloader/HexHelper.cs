
namespace Gralin.NETMF.USBizi
{
    public static class HexHelper
    {
        const string Hex = "0123456789ABCDEF";

        public static string ToHex(int b)
        {
            return new string(new[] { Hex[b >> 28], Hex[(b & 0x0F000000) >> 24], 
                                      Hex[(b & 0x00F00000) >> 20], Hex[(b & 0x000F0000) >> 16],
                                      Hex[(b & 0x0000F000) >> 12], Hex[(b & 0x00000F00) >> 8],
                                      Hex[(b & 0x000000F0) >> 4], Hex[b & 0x0000000F] });
        }

        public static string ToHex(byte b)
        {
            return new string(new[] { Hex[b >> 4], Hex[b & 0x0F] });
        }
    }
}
