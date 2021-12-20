using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Globalization;
using Hangfire.Common;
using Hangfire.Dashboard.Management.v2;
using Hangfire.Heartbeat;
using Hangfire.Redis;
using Hangfire.Tags.Redis.CSRedisCore;
using Hangfire.Tags.Sample.Jobs;
using Microsoft.AspNetCore.Localization;

namespace Hangfire.Tags.Sample
{
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
            var redisStorageOptions = new RedisStorageOptions
            {
                Prefix = "hangfire.dev:"
            };
            var storage = new RedisStorage("127.0.0.1:6379,defaultDatabase=1,poolsize=50", redisStorageOptions);
            JobStorage.Current = storage;
            services.AddHangfire(o =>
            {
                // 注册存储
                o.UseStorage(storage);
                // 注册Tags
                o.UseTagsWithRedis(new TagsOptions
                {
                    TagsListStyle = TagsListStyle.Dropdown
                }, redisStorageOptions);
                // 注册健康检查页
                o.UseHeartbeatPage(TimeSpan.FromSeconds(5));
                // 注册管理界面
                o.UseSimpleAssemblyNameTypeSerializer()
                    .UseRecommendedSerializerSettings()
                    .UseManagementPages(typeof(Startup).Assembly);
            });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            // 默认区域性
            var supportedCultures = new[]
            {
                new CultureInfo("zh-CN")
            };
            app.UseRequestLocalization(new RequestLocalizationOptions
            {
                DefaultRequestCulture = new RequestCulture("zh-CN"),
                // Formatting numbers, dates, etc.
                SupportedCultures = supportedCultures,
                // UI strings that we have localized.
                SupportedUICultures = supportedCultures,
                RequestCultureProviders = new List<IRequestCultureProvider>
                {
                    new QueryStringRequestCultureProvider(),
                    new CookieRequestCultureProvider(),
                    new AcceptLanguageHeaderRequestCultureProvider()
                }
            });
            app.UseHangfireDashboard(options: new DashboardOptions
            {
                IgnoreAntiforgeryToken = true,
                DisplayStorageConnectionString = false, // 是否显示数据库连接信息
                IsReadOnlyFunc = context => false,
            });
            app.UseHangfireServer();

            //var recurringJobs = new RecurringJobManager();
            RecurringJob.AddOrUpdate<Tasks>(x => x.SuccessTask(null, null), Cron.Minutely);
            //recurringJobs.AddOrUpdate("Failed Task", Job.FromExpression<Tasks>(x => x.FailedTask(null)), "*/2 * * * *", TimeZoneInfo.Local);

            BackgroundJob.Enqueue<BaseJob>(x => x.Run());
            BackgroundJob.Enqueue<DerivedJob>(x => x.Run());
        }
    }
}
