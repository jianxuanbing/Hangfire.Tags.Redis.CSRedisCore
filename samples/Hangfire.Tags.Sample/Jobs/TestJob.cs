using System;
using System.ComponentModel;
using Hangfire.Dashboard.Management.v2.Metadata;
using Hangfire.Dashboard.Management.v2.Support;
using Hangfire.Server;
using Hangfire.Tags.Attributes;

namespace Hangfire.Tags.Sample.Jobs
{
    [ManagementPage(MenuName = "Test", Title = nameof(TestJob))]
    public class TestJob : IJob
    {
        public TestJob()
        {
        }

        public void CreateJob1(PerformContext context, IJobCancellationToken cancellationToken)
        {
            BackgroundJob.Enqueue(() => Job("Home", null));
        }

        [DisplayName("CreateALot")]
        [Description("CreateALot")]
        public void CreateALot(PerformContext context, IJobCancellationToken cancellationToken)
        {
            for (var i = 0; i < 1000; i++)
                BackgroundJob.Enqueue(() => Job("Home", null));
        }

        [DisplayName("Schedule")]
        [Description("Schedule")]
        public void Schedule(PerformContext context, IJobCancellationToken cancellationToken)
        {
            BackgroundJob.Schedule(() => Job("Home", null), TimeSpan.FromMinutes(1));
        }

        [Tag("job", "{0}")]
        private void Job(string name, PerformContext ctx)
        {
            ctx.AddTags("finished");
        }
    }
}
