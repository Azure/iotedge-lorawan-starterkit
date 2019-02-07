using System;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;

namespace CayenneDecoderModule
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CreateWebHostBuilder(args).Build().Run();
        }

        public static IWebHostBuilder CreateWebHostBuilder(string[] args) =>
            WebHost.CreateDefaultBuilder(args)
                .UseStartup<Startup>()
                .UseKestrel(x => x.Limits.KeepAliveTimeout = TimeSpan.FromDays(10));

    }
}
