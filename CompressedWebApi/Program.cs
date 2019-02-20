using System;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using CompressedWebApi.Handlers;
using Microsoft.Owin.Hosting;

namespace CompressedWebApi
{
    public class Program
    {
        static void Main()
        {
            var baseAddress = "http://localhost:9000/";

            using (WebApp.Start<Startup>(url: baseAddress))
            {
                var client = HttpClientFactory.Create(new HttpClientHandler { AutomaticDecompression = DecompressionMethods.GZip }, new RequestCompressionHandler());
                //var client = HttpClientFactory.Create(new HttpClientHandler { AutomaticDecompression = DecompressionMethods.GZip });

                var requestUri = baseAddress + "api/values";

                //var response = client.GetAsync(requestUri).Result;
                var response = client.PostAsJsonAsync<string>(requestUri, "Hello world").Result;

                Console.WriteLine(response);
                Console.WriteLine(response.Content.ReadAsStringAsync().Result);

                if (Debugger.IsAttached)
                {
                    Console.WriteLine();

                    Console.WriteLine("Press a key to continue...");
                    Console.ReadKey();
                }
            }
        }
    }
}