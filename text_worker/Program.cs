using Newtonsoft.Json;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace text_worker
{
    public class Program
    {
        public static int Main(string[] args)
        {
            try
            {
                var redis = OpenRedisConnection("redis").GetDatabase();

                var definition = new { name = "", size = 0, user="", client="", content="" };

                while (true)
                {
                    string json = redis.ListLeftPopAsync("documents:inprocess:0").Result;
                    if (json != null)
                    {
                        var document = JsonConvert.DeserializeAnonymousType(json, definition);
                        Console.WriteLine($"Processing document '{document.name}' uploaded by '{document.user}/{document.client}'");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.ToString());
                return 1;
            }
        }

        private static ConnectionMultiplexer OpenRedisConnection(string hostname)
        {
            // Use IP address to workaround hhttps://github.com/StackExchange/StackExchange.Redis/issues/410
            var ipAddress = GetIp(hostname);
            Console.WriteLine($"Found redis at {ipAddress}");

            while (true)
            {
                try
                {
                    Console.Error.WriteLine("Connected to redis");
                    return ConnectionMultiplexer.Connect(ipAddress);
                }
                catch (RedisConnectionException)
                {
                    Console.Error.WriteLine("Waiting for redis");
                    Thread.Sleep(1000);
                }
            }
        }

        private static string GetIp(string hostname)
            => Dns.GetHostEntryAsync(hostname)
                .Result
                .AddressList
                .First(a => a.AddressFamily == AddressFamily.InterNetwork)
                .ToString();

    }
}
