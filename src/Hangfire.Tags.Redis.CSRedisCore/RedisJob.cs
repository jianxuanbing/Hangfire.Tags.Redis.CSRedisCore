using System;
using System.Collections.Generic;

namespace Hangfire.Tags.Redis.CSRedisCore
{
    /// <summary>
    /// Redis作业
    /// </summary>
    internal class RedisJob
    {
        /// <summary>
        /// 标识
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// 调用数据
        /// </summary>
        public string InvocationData { get; set; }

        /// <summary>
        /// 参数
        /// </summary>
        public string Arguments { get; set; }

        /// <summary>
        /// 创建时间
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// 过期时间
        /// </summary>
        public DateTime? ExpireAt { get; set; }

        /// <summary>
        /// 拉取时间
        /// </summary>
        public DateTime? FetchedAt { get; set; }

        /// <summary>
        /// 状态名称
        /// </summary>
        public string StateName { get; set; }

        /// <summary>
        /// 状态原因
        /// </summary>
        public string StateReason { get; set; }

        /// <summary>
        /// 状态数据
        /// </summary>
        public Dictionary<string, string> StateData { get; set; }
    }
}
