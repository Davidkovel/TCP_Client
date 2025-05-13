using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace TCP_Client;

class UdpPriceServer
{
    private static readonly Dictionary<string, decimal> _prices =
        new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
        {
            { "Laptop 2", 2222 },
            { "Laptop", 1111 }
        };

    private const int Port = 12345;

    static void Main(string[] args)
    {
        UdpClient udpServer = new UdpClient(Port);
        Console.WriteLine($"Server started. Waiting for connections...");

        try
        {
            while (true)
            {
                IPEndPoint clientEndPoint = new IPEndPoint(IPAddress.Any, 0);
                byte[] requestData = udpServer.Receive(ref clientEndPoint);
                string componentName = Encoding.UTF8.GetString(requestData);

                string response;
                if (_prices.TryGetValue(componentName, out decimal price))
                {
                    response = $"Name: {componentName}: {price}$";
                }
                else
                {
                    response = $"Does not exists '{componentName}'";
                }

                byte[] responseData = Encoding.UTF8.GetBytes(response);
                udpServer.Send(responseData, responseData.Length, clientEndPoint);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR: {ex.Message}");
        }
        finally
        {
            udpServer.Close();
        }
    }
}