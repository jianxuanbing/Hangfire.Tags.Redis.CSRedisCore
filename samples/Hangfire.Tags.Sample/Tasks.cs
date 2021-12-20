using System;
using Hangfire.Server;
using Hangfire.Tags.Attributes;

namespace Hangfire.Tags.Sample
{
    [Tag("task")]
    internal class Tasks
    {
        [Tag("success")]
        public void SuccessTask(PerformContext context, IJobCancellationToken token)
        {
            context.AddTags("finished");
        }

        [Tag("fail")]
        [AutomaticRetry(Attempts = 0)] // Disable retry
        public void FailedTask(PerformContext context)
        {
            context.AddTags("throw");
            throw new Exception("Fail please!");
        }
    }
}
