using Hangfire.Redis;
using Hangfire.Tags.Dashboard;

namespace Hangfire.Tags.Redis.CSRedisCore
{
    /// <summary>
    /// Hangfire全局配置(<see cref="IGlobalConfiguration"/>) 扩展
    /// </summary>
    public static class GlobalConfigurationExtensions
    {
        /// <summary>
        /// 配置Hangfire使用标签
        /// </summary>
        /// <param name="configuration">全局配置</param>
        /// <param name="options">标签选项配置</param>
        /// <param name="redisOptions">Redis选项配置</param>
        /// <param name="jobStorage">作业存储</param>
        public static IGlobalConfiguration UseTagsWithRedis(this IGlobalConfiguration configuration, TagsOptions options = null, RedisStorageOptions redisOptions = null, JobStorage jobStorage = null)
        {
            options = options ?? new TagsOptions();
            redisOptions = redisOptions ?? new RedisStorageOptions();
            var storage = new RedisTagsServiceStorage(redisOptions);
            (jobStorage ?? JobStorage.Current).Register(options, storage);
            var config = configuration.UseTags(options).UseFilter(new RedisStateFilter(storage));
            return config;
        }
    }
}
