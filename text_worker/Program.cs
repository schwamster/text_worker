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
using System.Text;

namespace text_worker
{
    public class Program
    {
        public static int Main(string[] args)
        {

            var mainThreadMayNeverDiePolicy = Policy
              .Handle<Exception>()
              .WaitAndRetryForever(
                retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                (exception, timespan, context) =>
                {
                    Console.WriteLine($"Error while processing a item in the queue. Resuming => {exception}");
                });


            ConfigurationBuilder configurationBuilder = new ConfigurationBuilder();
            configurationBuilder
                .AddJsonFile("appsettings.json", false)
                .AddEnvironmentVariables();

            var configuration = configurationBuilder.Build();

            string queue = configuration["Queue"];
            try
            {
                var redis = OpenRedisConnection(configuration["RedisHost"]).GetDatabase();

                var definition = new { name = "", size = 0, user = "", client = "", content = "", id = "" };
                Console.WriteLine($"Starting to read from Queue: {queue}");
                while (true)
                {
                    //TODO: messages will be lost here, if action fails. Better put it in a "processing" queue or something...
                    //e.g. http://big-elephants.com/2013-09/building-a-message-queue-using-redis-in-go/
                    //or consider changing to a completly different technology (rabbitmq...)
                    mainThreadMayNeverDiePolicy.Execute(() =>
                    {
                        string json = redis.ListLeftPopAsync(queue).Result;
                        if (json != null)
                        {
                            var document = JsonConvert.DeserializeAnonymousType(json, definition);
                            Console.WriteLine($"Processing document '{document.name}' uploaded by '{document.user}/{document.client}'");
                            var result = ExtractText(document.name, document.content, configuration["OcrServiceHost"]).Result;
                            Console.WriteLine($"Result from ocr: {result}");
                            AddExtractedTextToDocument(document.id, result, configuration["DocStoreHost"]);
                        }
                        else
                        {
                            Thread.Sleep(500);
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Fatal => text-worker crashed => {ex.ToString()}");
                return 1;
            }
        }

        private static async void AddExtractedTextToDocument(string id, string extractedText, string docStoreHost)
        {

            var patch = new[] { new { value = extractedText, path = "/extractedText", op = "add", from = "string" } };
            
            var content = new StringContent(JsonConvert.SerializeObject(patch), Encoding.UTF8, "application/json");
            using (var client = new HttpClient())
            {
                using (var message = await client.PatchAsync($"http://{docStoreHost}/api/Document/{id}", content))
                {
                    var input = await message.Content.ReadAsStringAsync();
                    Console.WriteLine($"Document patched -> {input}");
                }
            }

        }
        private static async Task<string> ExtractText(string name, string content, string ocrServiceHost)
        {
            //make call to ocr_service
            byte[] document = System.Convert.FromBase64String(content);
            using (var client = new HttpClient())
            {
                using (var msg = new MultipartFormDataContent("Upload----" + DateTime.Now.ToString(CultureInfo.InvariantCulture)))
                {
                    msg.Add(new StreamContent(new MemoryStream(document)), "files", name);
                    var url = $"http://{ocrServiceHost}/api/ocr";
                    Console.WriteLine($"OcrService Endpoint at: {url}");

                    using (var message = await client.PostAsync(url, msg))
                    {
                        var input = await message.Content.ReadAsStringAsync();
                        Console.WriteLine($"Document sent to {url}");
                        return input;
                    }
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

    public static class HttpClientExtensions
    {
        public static async Task<HttpResponseMessage> PatchAsync(this HttpClient client, string requestUri, HttpContent iContent)
        {
            var method = new HttpMethod("PATCH");
            var request = new HttpRequestMessage(method, requestUri)
            {
                Content = iContent
            };

            HttpResponseMessage response = new HttpResponseMessage();
            try
            {
                response = await client.SendAsync(request);
            }
            catch (TaskCanceledException e)
            {
                Console.WriteLine("ERROR: " + e.ToString());
            }

            return response;
        }
    }
}
