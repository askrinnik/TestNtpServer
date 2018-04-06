using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;

namespace TestNtp
{
  internal static class Program
  {
    private static void Main(string[] args)
    {
      IPAddress ipAddress;
      var arg0 = args[0];
      if (!char.IsDigit(arg0[0]))
      {
        var serverName = args[0];
        //var serverName = "askrqa1.metratech.com";
        var addresses = GetIpAddresses(serverName);
        ipAddress = addresses.First();
      }
      else
        ipAddress = IPAddress.Parse(arg0);

      Console.WriteLine("IP address: " + ipAddress);
      var time = GetNetworkUtcTime(ipAddress);
      Console.WriteLine("UTC Time  : " + time);
      Console.WriteLine("Local Time: " + time.ToLocalTime());
    }

    private static DateTime GetNetworkUtcTime(IPAddress ipAddress)
    {
      //default Windows time server

      // NTP message size - 16 bytes of the digest (RFC 2030)
      var ntpData = new byte[48];

      //Setting the Leap Indicator, Version Number and Mode values
      ntpData[0] = 0x1B; //LI = 0 (no warning), VN = 3 (IPv4 only), Mode = 3 (Client Mode)
      ntpData[1] = 0x01; //Stratum level of the time source 1: Primary server
      ntpData[2] = 10;   //Poll interval (8-bit signed integer) 10 seconds
      ntpData[3] = 0xFA; //Clock precision (8-bit signed integer)

      //The UDP port number assigned to NTP is 123
      var ipEndPoint = new IPEndPoint(ipAddress, 123);
      //NTP uses UDP

      using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
      {
        socket.Connect(ipEndPoint);

        //Stops code hang if NTP is blocked
        socket.ReceiveTimeout = 3000;

        socket.Send(ntpData);
        socket.Receive(ntpData);
        socket.Close();
      }

      //Offset to get to the "Transmit Timestamp" field (time at which the reply 
      //departed the server for the client, in 64-bit timestamp format."
      const byte serverReplyTime = 40;

      //Get the seconds part
      var intPart = BitConverter.ToUInt32(ntpData, serverReplyTime);

      //Get the seconds fraction
      var fractPart = BitConverter.ToUInt32(ntpData, serverReplyTime + 4);

      //Convert From big-endian to little-endian
      intPart = SwapEndianness(intPart);
      fractPart = SwapEndianness(fractPart);

      var milliseconds = (ulong)intPart * 1000 + (ulong)fractPart * 1000 / 0x100000000L;

      //**UTC** time
      var networkDateTime = new DateTime(1900, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddMilliseconds((long)milliseconds);

      return networkDateTime;
    }

    private static IEnumerable<IPAddress> GetIpAddresses(string dnsName)
    {
      return Dns.GetHostEntry(dnsName).AddressList;
    }

    // stackoverflow.com/a/3294698/162671
    private static uint SwapEndianness(uint x)
    {
      return ((x & 0x000000ff) << 24) +
             ((x & 0x0000ff00) << 8) +
             ((x & 0x00ff0000) >> 8) +
             ((x & 0xff000000) >> 24);
    }
  }

}
