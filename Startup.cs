using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using StackExchange.Redis;

namespace SessionExample
{
    public class Startup
    {
        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
            var redisSessionService = Environment.GetEnvironmentVariable("REDIS_SESSION_SERVICE_NAME");
            var host = Environment.GetEnvironmentVariable($"{redisSessionService}_SERVICE_HOST");
            var port = Environment.GetEnvironmentVariable($"{redisSessionService}_SERVICE_PORT");
            var conn = $"{host}:{port}";
            var redis = ConnectionMultiplexer.Connect(conn);
            
            services.AddDataProtection()
                .PersistKeysToRedis(redis, "DataProtection-Keys");
            services.AddDistributedRedisCache(option =>
            {
                option.Configuration = conn;
                option.InstanceName = "master";
            });
            services.AddSession();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory)
        {
            app.UseSession();

            loggerFactory.AddConsole();

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            //This sample is described in https://docs.microsoft.com/en-us/aspnet/core/fundamentals/app-state
            app.Map("/session", subApp =>
            {
                subApp.Run(async context =>
                {
                    // uncomment the following line and delete session coookie to generate an error due to session access after response has begun
                    // await context.Response.WriteAsync("some content");
                    RequestEntryCollection collection = GetOrCreateEntries(context);
                    collection.RecordRequest(context.Request.PathBase + context.Request.Path);
                    SaveEntries(context, collection);
                    if (context.Session.GetString("StartTime") == null)
                    {
                        context.Session.SetString("StartTime", DateTime.Now.ToString());
                    }

                    await context.Response.WriteAsync("<html><body>");
                    await context.Response.WriteAsync($"Counting: You have made {collection.TotalCount()} requests to this application.<br><a href=\"/\">Return</a>");
                    await context.Response.WriteAsync("</body></html>");
                });
            });

            app.Run(async context =>
            {
                RequestEntryCollection collection = GetOrCreateEntries(context);

                if (collection.TotalCount() == 0)
                {
                    await context.Response.WriteAsync("<html><body>");
                    await context.Response.WriteAsync("Your session has not been established.<br>");
                    await context.Response.WriteAsync(DateTime.Now.ToString() + "<br>");
                    await context.Response.WriteAsync("<a href=\"/session\">Establish session</a>.<br>");
                }
                else
                {
                    collection.RecordRequest(context.Request.PathBase + context.Request.Path);
                    SaveEntries(context, collection);

                    // Note: it's best to consistently perform all session access before writing anything to response
                    await context.Response.WriteAsync("<html><body>");
                    await context.Response.WriteAsync("Session Established At: " + context.Session.GetString("StartTime") + "<br>");
                    foreach (var entry in collection.Entries)
                    {
                        await context.Response.WriteAsync("Request: " + entry.Path + " was requested " + entry.Count + " times.<br />");
                    }

                    await context.Response.WriteAsync("Your session was located, you've visited the site this many times: " + collection.TotalCount() + "<br />");
                }
                await context.Response.WriteAsync("<a href=\"/untracked\">Visit untracked part of application</a>.<br>");
                await context.Response.WriteAsync("</body></html>");
            });

            
        }

        private RequestEntryCollection GetOrCreateEntries(HttpContext context)
        {
            RequestEntryCollection collection = null;
            byte[] requestEntriesBytes = context.Session.Get("RequestEntries");

            if (requestEntriesBytes != null && requestEntriesBytes.Length > 0)
            {
                string json = System.Text.Encoding.UTF8.GetString(requestEntriesBytes);
                return JsonConvert.DeserializeObject<RequestEntryCollection>(json);
            }
            if (collection == null)
            {
                collection = new RequestEntryCollection();
            }
            return collection;
        }

        private void SaveEntries(HttpContext context, RequestEntryCollection collection)
        {
            string json = JsonConvert.SerializeObject(collection);
            byte[] serializedResult = System.Text.Encoding.UTF8.GetBytes(json);

            context.Session.Set("RequestEntries", serializedResult);
        }
    }

    public class RequestEntry
    {
        public string Path { get; set; }
        public int Count { get; set; }
    }

    public class RequestEntryCollection
    {
        public List<RequestEntry> Entries { get; set; } = new List<RequestEntry>();

        public void RecordRequest(string requestPath)
        {
            var existingEntry = Entries.FirstOrDefault(e => e.Path == requestPath);
            if (existingEntry != null) { existingEntry.Count++; return; }

            var newEntry = new RequestEntry()
            {
                Path = requestPath,
                Count = 1
            };
            Entries.Add(newEntry);
        }

        public int TotalCount()
        {
            return Entries.Sum(e => e.Count);
        }
    }
}
