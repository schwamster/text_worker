using Newtonsoft.Json;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using System.Net.Http;
using System.Globalization;
using System.IO;
using Polly;

namespace text_worker
{
    public class Program
    {
        public static int Main(string[] args)
        {
            ConfigurationBuilder configurationBuilder = new ConfigurationBuilder();
            configurationBuilder
                .AddJsonFile("appsettings.json", false)
                .AddEnvironmentVariables();

            var configuration = configurationBuilder.Build();

            string queue = configuration["Queue"];
            try
            {
                var redis = OpenRedisConnection(configuration["RedisHost"]).GetDatabase();

                var definition = new { name = "", size = 0, user = "", client = "", content = "" };
                Console.WriteLine($"Starting to read from Queue: {queue}");
                while (true)
                {
                    string json = redis.ListLeftPopAsync(queue).Result;
                    if (json != null)
                    {
                        var document = JsonConvert.DeserializeAnonymousType(json, definition);
                        Console.WriteLine($"Processing document '{document.name}' uploaded by '{document.user}/{document.client}'");
                        var result = ExtractText(document.name, document.content, configuration["OcrServiceHost"]).Result;
                        Console.WriteLine($"Result from ocr: {result}");
                    }
                    else
                    {
                        Thread.Sleep(500);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.ToString());
                return 1;
            }
        }

        private static async Task<string> ExtractText(string name, string content, string ocrServiceHost)
        {
            var policy = Policy
              .Handle<AggregateException>()
              .WaitAndRetryForever(
                retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                (exception, timespan, context) =>
                {
                    Console.WriteLine($"Could not connect to OcrService at {ocrServiceHost} will retry forever => {exception}");       
                });

            //TODO: messages will be lost here
            //make call to ocr_service
            byte[] document = System.Convert.FromBase64String(content);
            using (var client = new HttpClient())
            {
                using (var msg = new MultipartFormDataContent("Upload----" + DateTime.Now.ToString(CultureInfo.InvariantCulture)))
                {
                    msg.Add(new StreamContent(new MemoryStream(document)), "files", name);
                    var url = $"http://{ocrServiceHost}/api/ocr";
                    Console.WriteLine($"OcrService Endpoint at: {url}");

                    return await policy.Execute(async () =>
                     {
                         try
                         {
                             using (var message = await client.PostAsync(url, msg))
                             {
                                 var input = await message.Content.ReadAsStringAsync();
                                 Console.WriteLine($"Document sent to {url}");
                                 return input;
                             }
                         }
                         catch (Exception ex)
                         {
                   
                             Console.WriteLine(ex);
                             return null;
                         }
                     });
                }
            }
        }



        private static ConnectionMultiplexer OpenRedisConnection(string hostname)
        {
            // Use IP address to workaround hhttps://github.com/StackExchange/StackExchange.Redis/issues/410
            Console.WriteLine($"Looking for redis at {hostname}");
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
