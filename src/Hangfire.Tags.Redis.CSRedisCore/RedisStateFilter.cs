using System.Linq;
using Hangfire.States;
using Hangfire.Storage;

namespace Hangfire.Tags.Redis.CSRedisCore
{
    /// <summary>
    /// Redis状态过滤器
    /// </summary>
    internal class RedisStateFilter : IApplyStateFilter
    {
        /// <summary>
        /// Redis标签服务存储
        /// </summary>
        private readonly RedisTagsServiceStorage _storage;

        /// <summary>
        /// 初始化一个<see cref="RedisStateFilter"/>类型的实例
        /// </summary>
        /// <param name="storage">Redis标签服务存储</param>
        public RedisStateFilter(RedisTagsServiceStorage storage)
        {
            _storage = storage;
        }

        /// <summary>
        /// Called after the specified state was applied
        /// to the job within the given transaction.
        /// </summary>
        public void OnStateApplied(ApplyStateContext context, IWriteOnlyTransaction transaction)
        {
            UpdateTagState(context);
        }

        /// <summary>
        /// Called when the state with specified state was
        /// unapplied from the job within the given transaction.
        /// </summary>
        public void OnStateUnapplied(ApplyStateContext context, IWriteOnlyTransaction transaction)
        {
            UpdateTagState(context);
        }

        private void UpdateTagState(ApplyStateContext context)
        {
            if (context.BackgroundJob == null)
                return;
            var oldState = context.OldStateName?.ToLower();
            var state = context.NewState?.Name.ToLower();

            _storage.GetMonitoringApi(context.Storage).UseConnection(redis =>
            {
                var tags = redis.ZScan(_storage.GetRedisKey($"tags:{context.BackgroundJob.Id}"), 0).Items
                    .Select(e => e.member);
                foreach (var tag in tags)
                {
                    if (!string.IsNullOrEmpty(oldState))
                        redis.SRem(_storage.GetRedisKey($"tags:{tag}:{oldState}"), context.BackgroundJob.Id);
                    if (!string.IsNullOrEmpty(state))
                        redis.SAdd(_storage.GetRedisKey($"tags:{tag}:{state}"), context.BackgroundJob.Id);
                }
                return 0;
            });
        }
    }
}
