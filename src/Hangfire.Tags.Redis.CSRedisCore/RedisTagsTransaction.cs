using System;
using Hangfire.Tags.Storage;

namespace Hangfire.Tags.Redis.CSRedisCore
{
    /// <summary>
    /// Redis标签事务
    /// </summary>
    internal class RedisTagsTransaction : ITagsTransaction
    {
        /// <summary>
        /// 设置 Set 过期时间
        /// </summary>
        /// <param name="key">缓存键</param>
        /// <param name="value">值</param>
        /// <param name="expireIn">过期时间</param>
        public void ExpireSetValue(string key, string value, TimeSpan expireIn)
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key));
        }

        /// <summary>
        /// 设置 Set 持久保持
        /// </summary>
        /// <param name="key">缓存键</param>
        /// <param name="value">值</param>
        public void PersistSetValue(string key, string value)
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key));
        }
    }
}
