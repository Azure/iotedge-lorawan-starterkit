namespace CayenneDecoderModule
{
    using System;
    using System.Text;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Diagnostics;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;

    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            _ = services.AddMvc().SetCompatibilityVersion(CompatibilityVersion.Version_2_1);
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            if (env.IsDevelopment())
            {
                _ = app.UseDeveloperExceptionPage();
            }
            else
            {
                //app.UseHsts();
            }

            _ = app.UseExceptionHandler(errorApp =>
            {
                errorApp.Run(async context =>
                {
                    context.Response.StatusCode = 400; // We are using HTTP Status Code 400 - Bad Request.
                    context.Response.ContentType = "text/plain";

                    var error = context.Features.Get<IExceptionHandlerFeature>();
                    if (error != null)
                    {
                        var ex = error.Error;
                        string exMessage;
                        if (ex.InnerException != null)
                            exMessage = $"Decoder error: {ex.InnerException.Message}";

                        else
                            exMessage = ex.Message;

                        Console.WriteLine($"Exception at: {System.DateTime.UtcNow}: {exMessage}");
                        await context.Response.WriteAsync(exMessage, Encoding.UTF8);
                    }
                });
            });

            //app.UseHttpsRedirection();
            _ = app.UseMvc();
        }
    }
}
