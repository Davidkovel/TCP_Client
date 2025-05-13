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

    private static readonly RateLimiter _rateLimiter =
        new RateLimiter(maxRequests: 10, timeWindow: TimeSpan.FromHours(1));

    private static readonly ClientManager _clientManager =
        new ClientManager(maxClients: 5, clientTimeout: TimeSpan.FromMinutes(10));

    static void Main(string[] args)
    {
        UdpClient udpServer = new UdpClient(Port);
        Console.WriteLine($"Server started. Waiting for connections...");


        Thread cleanupThread = new Thread(_clientManager.CleanupInactiveClients);
        cleanupThread.IsBackground = true;
        cleanupThread.Start();

        string response;
        try
        {
            while (true)
            {
                IPEndPoint clientEndPoint = new IPEndPoint(IPAddress.Any, 0);
                byte[] requestData = udpServer.Receive(ref clientEndPoint);
                string componentName = Encoding.UTF8.GetString(requestData);

                if (!_clientManager.TryAddOrUpdateClient(clientEndPoint))
                {
                    var busyResponse = "Server is busy. Try again later.";
                    byte[] busyResponseData = Encoding.UTF8.GetBytes(busyResponse);
                    udpServer.Send(busyResponseData, busyResponseData.Length, clientEndPoint);
                    continue;
                }

                if (!_rateLimiter.IsRequestAllowed(clientEndPoint))
                {
                    string rateLimitResponse =
                        $"Rate limit exceeded (max {_rateLimiter.MaxRequests} requests per hour). Try again later.";
                    byte[] rateLimitResponseData = Encoding.UTF8.GetBytes(rateLimitResponse);
                    udpServer.Send(rateLimitResponseData, rateLimitResponseData.Length, clientEndPoint);
                    continue;
                }

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

public class RateLimiter
{
    private readonly Dictionary<IPEndPoint, ClientRequestInfo> _clientRequests =
        new Dictionary<IPEndPoint, ClientRequestInfo>();

    private readonly object _lock = new object();

    public int MaxRequests { get; }
    public TimeSpan TimeWindow { get; }

    public RateLimiter(int maxRequests, TimeSpan timeWindow)
    {
        MaxRequests = maxRequests;
        TimeWindow = timeWindow;
    }

    public bool IsRequestAllowed(IPEndPoint clientEndPoint)
    {
        lock (_lock)
        {
            if (!_clientRequests.TryGetValue(clientEndPoint, out ClientRequestInfo clientInfo))
            {
                clientInfo = new ClientRequestInfo(DateTime.Now);
                _clientRequests[clientEndPoint] = clientInfo;
            }

            if (DateTime.Now - clientInfo.FirstRequestTime > TimeWindow)
            {
                clientInfo.RequestCount = 0;
                clientInfo.FirstRequestTime = DateTime.Now;
            }

            if (clientInfo.RequestCount >= MaxRequests)
            {
                return false;
            }

            clientInfo.RequestCount++;
            return true;
        }
    }

    private class ClientRequestInfo
    {
        public int RequestCount { get; set; }
        public DateTime FirstRequestTime { get; set; }

        public ClientRequestInfo(DateTime firstRequestTime)
        {
            FirstRequestTime = firstRequestTime;
            RequestCount = 0;
        }
    }
}

public class ClientManager
{
    private readonly Dictionary<IPEndPoint, DateTime> _activeClients = new Dictionary<IPEndPoint, DateTime>();
    private readonly object _lock = new object();

    public int MaxClients { get; }
    public TimeSpan ClientTimeout { get; }

    public ClientManager(int maxClients, TimeSpan clientTimeout)
    {
        MaxClients = maxClients;
        ClientTimeout = clientTimeout;
    }

    public bool TryAddOrUpdateClient(IPEndPoint clientEndPoint)
    {
        lock (_lock)
        {
            if (_activeClients.ContainsKey(clientEndPoint))
            {
                _activeClients[clientEndPoint] = DateTime.Now;
                return true;
            }

            if (_activeClients.Count >= MaxClients)
            {
                return false;
            }

            _activeClients.Add(clientEndPoint, DateTime.Now);
            return true;
        }
    }

    public void CleanupInactiveClients()
    {
        while (true)
        {
            lock (_lock)
            {
                List<IPEndPoint> clientsToRemove = new List<IPEndPoint>();

                foreach (var client in _activeClients)
                {
                    if (DateTime.Now - client.Value > ClientTimeout)
                    {
                        clientsToRemove.Add(client.Key);
                    }
                }

                foreach (var client in clientsToRemove)
                {
                    _activeClients.Remove(client);
                    Console.WriteLine($"Disconnected inactive client: {client}");
                }
            }

            Thread.Sleep(60000);
        }
    }
}