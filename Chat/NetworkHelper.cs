using System.Net;
using System.Net.Sockets;

namespace Chat;

public class NetworkHelper
{
    public string GetLocalIp()
    {
        foreach (var ip in Dns.GetHostEntry(Dns.GetHostName()).AddressList)
        {
            if (ip.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip))
                return ip.ToString();
        }
        return "127.0.0.1";
    }
}