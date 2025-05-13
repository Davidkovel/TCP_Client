using System;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Client;

class UdpPriceClient
{
    private const string ServerIp = "127.0.0.1";
    private const int Port = 12345;

    static void Main(string[] args)
    {
        UdpClient udpClient = new UdpClient();
        IPEndPoint serverEndPoint = new IPEndPoint(IPAddress.Parse(ServerIp), Port);

        try
        {
            while (true)
            {
                Console.Write("Enter the name: ");
                string input = Console.ReadLine();

                if (input.Equals("exit", StringComparison.OrdinalIgnoreCase))
                    break;

                byte[] requestData = Encoding.UTF8.GetBytes(input);
                udpClient.Send(requestData, requestData.Length, serverEndPoint);

                byte[] responseData = udpClient.Receive(ref serverEndPoint);
                string response = Encoding.UTF8.GetString(responseData);

                Console.WriteLine(response);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR: {ex.Message}");
        }
        finally
        {
            udpClient.Close();
            Console.WriteLine("Client disconnected");
        }
    }
}