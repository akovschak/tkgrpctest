using Grpc.Core;
using Grpc.Net.Client;
using GrpcService1;
using System.Diagnostics;
using System.Net.Http;
using System.Runtime;
using System.Text.Json;

namespace GrpcClient1
{

    namespace Rest
    {
        public class HelloRequest
        {
            public string Name { get; set; }
        }

        public class HelloReply
        {
            public string Message { get; set; }
        }
    }


    internal class Program
    {
        static async Task Main(string[] args)
        {
            int count = 45000;
            int mod = count / 10;
            int iters = 3;

            Console.WriteLine($"Server: {GCSettings.IsServerGC}");

            Console.WriteLine("Grpc vs Rest");

            Stopwatch sw = new Stopwatch();
            sw.Start();

            //using var channel = GrpcChannel.ForAddress("https://localhost:7149");

            //using var channel = GrpcChannel.ForAddress("https://localhost:7149",
            //    new GrpcChannelOptions
            //    {
            //        //HttpHandler = new SocketsHttpHandler
            //        //{
            //        //    EnableMultipleHttp2Connections = true
            //        //}
            //    });



            //single thread, single channel grpc
            List<Task> tasks = new List<Task>();

            {
                var t = Task.Run(async () =>
                {
                    using var channel = GrpcChannel.ForAddress("http://10.0.0.218:5070",
                        new GrpcChannelOptions
                        {
                            Credentials = ChannelCredentials.Insecure
                        });

                    var client = new Greeter.GreeterClient(channel);
                    for (int i = 0; i < count; i++)
                    {
                        var reply = await client.SayHelloAsync(new HelloRequest { Name = $"GreeterClientGrpc{i}" });

                        if (i % mod == 0)
                            Console.WriteLine("Greeting: " + reply.Message);
                    }
                });

                tasks.Add(t);
            }

            Task.WaitAll(tasks.ToArray());

            sw.Stop();
            Console.WriteLine($"Grpc {count} in {sw.ElapsedMilliseconds}ms {sw.ElapsedMilliseconds / 1000}s  each {(sw.ElapsedMilliseconds * 1000) / count}us");


            sw.Reset();
            sw.Start();

            //single channel multiple thread grpc
            List<Task> tasks3 = new List<Task>();

            using var channel = GrpcChannel.ForAddress("http://10.0.0.218:5070",
                        new GrpcChannelOptions
                        {
                            Credentials = ChannelCredentials.Insecure
                        });

            for (int z = 0; z < iters; z++)
            {
                var t = Task.Run(async () =>
                {
                    var client = new Greeter.GreeterClient(channel);
                    
                    for (int i = 0; i < count; i++)
                    {
                        var reply = await client.SayHelloAsync(new HelloRequest { Name = $"GreeterClientGrpc{i}" });

                        if (i % mod == 0)
                            Console.WriteLine("Greeting: " + reply.Message);
                    }
                });

                tasks3.Add(t);
            }

            Task.WaitAll(tasks3.ToArray());

            sw.Stop();
            Console.WriteLine($"Grpc concurrent {count} in {sw.ElapsedMilliseconds}ms {sw.ElapsedMilliseconds / 1000}s  each {(sw.ElapsedMilliseconds * 1000) / count}us");



            //rest with multiple threads
            JsonSerializerOptions jso = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

            sw.Reset();
            sw.Start();

            List<Task> tasks2 = new List<Task>();

            for (int z = 0; z < iters; z++)
            {
                var t = Task.Run(async () =>
                {
                    HttpClient cli = new HttpClient();

                    for (int i = 0; i < count; i++)
                    {
                        Rest.HelloRequest hr = new Rest.HelloRequest { Name = $"GreeterClientRest{i}" };

                        using (var req = new HttpRequestMessage(HttpMethod.Get, "http://10.0.0.218:5080/api/test/hello"))
                        {
                            string js = System.Text.Json.JsonSerializer.Serialize<Rest.HelloRequest>(hr);
                            StringContent content = new StringContent(js,
                                System.Text.Encoding.UTF8, "application/json");

                            req.Content = content;

                            HttpResponseMessage response = await cli.SendAsync(req);

                            //Console.Write(await response.Content.ReadAsStringAsync());

                            var reply2 = System.Text.Json.JsonSerializer.Deserialize<Rest.HelloReply>(response.Content.ReadAsStream(), jso);

                            if (i % mod == 0)
                                Console.WriteLine("Greeting: " + reply2.Message);
                        }
                    }
                });

                tasks2.Add(t);
            }

            Task.WaitAll(tasks2.ToArray());

            sw.Stop();
            Console.WriteLine($"Rest {count} in {sw.ElapsedMilliseconds}ms {sw.ElapsedMilliseconds / 1000}s  each {(sw.ElapsedMilliseconds*1000)/count}us");




        }
    }
}
