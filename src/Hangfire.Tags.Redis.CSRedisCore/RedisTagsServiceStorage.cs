using System;
using System.Collections.Generic;
using System.Linq;
using CSRedis;
using Hangfire.Common;
using Hangfire.Redis;
using Hangfire.Storage;
using Hangfire.Storage.Monitoring;
using Hangfire.Tags.Dashboard.Monitoring;
using Hangfire.Tags.Storage;

namespace Hangfire.Tags.Redis.CSRedisCore
{
    /// <summary>
    /// Redis标签服务存储
    /// </summary>
    public class RedisTagsServiceStorage : ObsoleteBaseStorage, ITagsServiceStorage
    {
        /// <summary>
        /// Redis存储选项配置
        /// </summary>
        private readonly RedisStorageOptions _options;

        /// <summary>
        /// 初始化一个<see cref="RedisTagsServiceStorage"/>类型的实例
        /// </summary>
        public RedisTagsServiceStorage() : this(new RedisStorageOptions())
        {
        }

        /// <summary>
        /// 初始化一个<see cref="RedisTagsServiceStorage"/>类型的实例
        /// </summary>
        /// <param name="options">Redis存储选项配置</param>
        public RedisTagsServiceStorage(RedisStorageOptions options)
        {
            _options = options;
        }

        /// <summary>
        /// 获取监控API
        /// </summary>
        /// <param name="jobStorage">作业存储</param>
        internal RedisTagsMonitoringApi GetMonitoringApi(JobStorage jobStorage) => new RedisTagsMonitoringApi(jobStorage.GetMonitoringApi());

        /// <summary>
        /// 获取Redis缓存键
        /// </summary>
        /// <param name="key">缓存键</param>
        internal string GetRedisKey(string key)
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key));
            return _options.Prefix + key;
        }

        /// <summary>
        /// 获取标签事务
        /// </summary>
        /// <param name="transaction">只写事务</param>
        public override ITagsTransaction GetTransaction(IWriteOnlyTransaction transaction) => new RedisTagsTransaction();

        /// <summary>
        /// 搜索加权标签
        /// </summary>
        /// <param name="jobStorage">作业存储</param>
        /// <param name="tag">标签</param>
        /// <param name="setKey">Set键名</param>
        public override IEnumerable<TagDto> SearchWeightedTags(JobStorage jobStorage, string tag = null, string setKey = "tags")
        {
            var monitoringApi = GetMonitoringApi(jobStorage);
            return monitoringApi.UseConnection(redis =>
            {
                var key = GetRedisKey(setKey);
                var matchingTags = new HashSet<string>();
                foreach (var item in redis.ZScan(key, 0, string.IsNullOrEmpty(tag) ? null : $"*{tag}*").Items)
                    matchingTags.Add(item.member);
                return matchingTags.Select(m => new TagDto
                {
                    Amount = 1,
                    Percentage = 1,
                    Tag = m
                });
            });
        }

        /// <summary>
        /// 搜索相关标签
        /// </summary>
        /// <param name="jobStorage">作业存储</param>
        /// <param name="tag">标签</param>
        /// <param name="setKey">Set键名</param>
        public override IEnumerable<string> SearchRelatedTags(JobStorage jobStorage, string tag, string setKey = "tags")
        {
            var monitoringApi = GetMonitoringApi(jobStorage);
            return monitoringApi.UseConnection(redis =>
            {
                var entries = redis.ZScan(GetRedisKey($"{setKey}:{tag}"), 0).Items;
                var jobIds = entries.Select(e => e.member).ToList();
                return jobIds.SelectMany(j =>
                {
                    var key = GetRedisKey($"{setKey}:{j}");
                    var tags = redis.ZScan(key, 0).Items;
                    return tags.Select(t => t.member);
                }).Where(t => t != tag).Distinct();
            });
        }

        /// <summary>
        /// 获取作业计数
        /// </summary>
        /// <param name="jobStorage">作业存储</param>
        /// <param name="tags">标签数组</param>
        /// <param name="stateName">状态名称</param>
        public override int GetJobCount(JobStorage jobStorage, string[] tags, string stateName = null)
        {
            var monitoringApi = GetMonitoringApi(jobStorage);
            return monitoringApi.UseConnection(redis => GetJobCount(redis, tags, stateName));
        }

        /// <summary>
        /// 获取作业计数
        /// </summary>
        /// <param name="redis">Redis客户端</param>
        /// <param name="tags">标签数组</param>
        /// <param name="stateName">状态名称</param>
        private int GetJobCount(CSRedisClient redis, string[] tags, string stateName)
        {
            var redisKeys = tags.Select(t =>
            {
                var key = string.IsNullOrEmpty(stateName) ? $"{t}" : $"{t}:{stateName.ToLower()}";
                return GetRedisKey(key);
            }).ToArray();

            if (redisKeys.Length <= 1)
            {
                return (int)(string.IsNullOrEmpty(stateName)
                    ? redis.ZCard(redisKeys.First())
                    : redis.SCard(redisKeys.First()));
            }

            var tempKey = GetRedisKey($"tags:jobcount-{stateName.ToLower()}");
            var retVal = string.IsNullOrEmpty(stateName)
                ? redis.ZUnionStore(tempKey, null, RedisAggregate.Sum, redisKeys)
                : redis.SUnionStore(tempKey, redisKeys);
            redis.Del(tempKey);
            return (int)retVal;
        }

        /// <summary>
        /// 获取作业状态技术
        /// </summary>
        /// <param name="jobStorage">作业存储</param>
        /// <param name="tags">标签数组</param>
        /// <param name="maxTags">最大标签数</param>
        public override IDictionary<string, int> GetJobStateCount(JobStorage jobStorage, string[] tags, int maxTags = 50)
        {
            var monitoringApi = GetMonitoringApi(jobStorage);
            return monitoringApi.UseConnection(redis =>
            {
                var retVal = new Dictionary<string, int>();
                var allStates = GlobalStateHandlers.Handlers
                    .Select(h => h.StateName)
                    .Union(new[] { "Failed", "Processing" });

                foreach (var state in allStates)
                {
                    var redisKeys = tags.Select(t => GetRedisKey($"{t}:{state.ToLower()}"));
                    var tempKey = GetRedisKey($"tags:jobstatecount-{state.ToLower()}");
                    var amount = redis.ZUnionStore(tempKey, null, RedisAggregate.Sum, redisKeys.ToArray());
                    redis.Del(tempKey);
                    if (amount != 0)
                        retVal.Add(state, (int)amount);
                }

                return retVal;
            });
        }

        /// <summary>
        /// 获取匹配的作业列表
        /// </summary>
        /// <param name="jobStorage">作业存储</param>
        /// <param name="tags">标签数组</param>
        /// <param name="from">分页开始索引</param>
        /// <param name="count">每页数量</param>
        /// <param name="stateName">状态名</param>
        public override JobList<MatchingJobDto> GetMatchingJobs(JobStorage jobStorage, string[] tags, int @from, int count, string stateName = null)
        {
            var monitoringApi = GetMonitoringApi(jobStorage);
            return monitoringApi.UseConnection(redis => GetJobs(redis, from, count, tags, stateName,
                (redisJob, job, stateData) =>
                    new MatchingJobDto
                    {
                        Job = job,
                        State = redisJob.StateName,
                        CreatedAt = redisJob.CreatedAt,
                        ResultAt = GetNullableStateDate(stateData, redisJob.StateName),
                        EnqueueAt = GetNullableStateDate(stateData, "Enqueue")
                    }));
        }

        /// <summary>
        /// 获取可空的状态日期
        /// </summary>
        /// <param name="stateData">状态数据</param>
        /// <param name="stateName">状态名</param>
        private static DateTime? GetNullableStateDate(SafeDictionary<string, string> stateData, string stateName)
        {
            var stateDateName = stateName == "Processing" ? "StartedAt" : $"{stateName}At";
            var dateTime = stateData?[stateDateName];
            return !string.IsNullOrEmpty(dateTime) ? JobHelper.DeserializeNullableDateTime(dateTime) : null;
        }

        /// <summary>
        /// 获取作业列表
        /// </summary>
        /// <typeparam name="TDto">数据传输对象类型</typeparam>
        /// <param name="redis">Redis客户端</param>
        /// <param name="from">分页开始索引</param>
        /// <param name="count">每页数量</param>
        /// <param name="tags">标签数组</param>
        /// <param name="stateName">状态明</param>
        /// <param name="selector">选择器</param>
        private JobList<TDto> GetJobs<TDto>(CSRedisClient redis, int from, int count, string[] tags, string stateName,
            Func<RedisJob, Job, SafeDictionary<string, string>, TDto> selector)
        {
            var redisKeys = tags.Select(t =>
            {
                var key = string.IsNullOrEmpty(stateName) ? $"{t}" : $"{t}:{stateName.ToLower()}";
                return GetRedisKey(key);
            }).ToArray();

            List<string> jobIds;
            if (redisKeys.Length <= 1)
            {
                jobIds = string.IsNullOrEmpty(stateName)
                    ? redis.ZScan(redisKeys.First(), 0).Items.Select(e => e.member).Skip(from).Take(count).ToList()
                    : redis.SScan(redisKeys.First(), 0).Items.Skip(from).Take(count).ToList();
            }
            else
            {
                var tempKey = GetRedisKey($"tags:job-{stateName.ToLower()}");
                if (string.IsNullOrEmpty(stateName))
                {
                    redis.ZUnionStore(tempKey, null, RedisAggregate.Sum, redisKeys);
                    jobIds = redis.ZScan(tempKey, 0).Items.Select(e => e.member).Skip(from).Take(count).ToList();
                }
                else
                {
                    redis.SUnionStore(tempKey, redisKeys);
                    jobIds = redis.SScan(tempKey, 0).Items.Skip(from).Take(count).ToList();
                }

                redis.Del(tempKey);
            }

            var redisJobs = jobIds.Select(id =>
            {
                var values = new SafeDictionary<string, string>(
                    redis.HGetAll(GetRedisKey($"job:{id}")), StringComparer.OrdinalIgnoreCase);

                var stateValues = new SafeDictionary<string, string>(
                    redis.HGetAll(GetRedisKey($"job:{id}:state")), StringComparer.OrdinalIgnoreCase);

                return new RedisJob
                {
                    Id = Guid.Parse(id),
                    CreatedAt = JobHelper.DeserializeDateTime(values["CreatedAt"]),
                    StateData = stateValues,
                    StateName = values["State"],
                    Arguments = values["Arguments"],
                    InvocationData =
                        new InvocationData(values["Type"], values["Method"], values["ParameterTypes"],
                            values["Arguments"]).SerializePayload(true)
                };
            }).ToList();

            return DeserializeJobs(redisJobs, selector);
        }

        /// <summary>
        /// 反序列化作业
        /// </summary>
        /// <param name="invocationData">调用数据</param>
        /// <param name="arguments">参数</param>
        private static Job DeserializeJob(string invocationData, string arguments)
        {
            var data = InvocationData.DeserializePayload(invocationData);
            if (!string.IsNullOrEmpty(arguments))
                data.Arguments = arguments;
            try
            {
                return data.DeserializeJob();
            }
            catch (JobLoadException)
            {
                return null;
            }
        }

        /// <summary>
        /// 反序列化作业列表
        /// </summary>
        /// <typeparam name="TDto">数据传输对象类型</typeparam>
        /// <param name="jobs">作业列表</param>
        /// <param name="selector">选择器</param>
        private static JobList<TDto> DeserializeJobs<TDto>(ICollection<RedisJob> jobs, Func<RedisJob, Job, SafeDictionary<string, string>, TDto> selector)
        {
            var result = new List<KeyValuePair<string, TDto>>(jobs.Count);
            foreach (var job in jobs)
            {
                var dto = default(TDto);
                if (job.InvocationData != null)
                {
                    var deserializedData = job.StateData;
                    var stateData = deserializedData != null
                        ? new SafeDictionary<string, string>(deserializedData, StringComparer.OrdinalIgnoreCase)
                        : null;
                    dto = selector(job, DeserializeJob(job.InvocationData, job.Arguments), stateData);
                }

                result.Add(new KeyValuePair<string, TDto>(job.Id.ToString(), dto));
            }
            return new JobList<TDto>(result);
        }

        /// <summary>
        /// 安全字典
        /// <para>
        /// 解决无效key问题：https://github.com/HangfireIO/Hangfire/issues/871
        /// </para>
        /// </summary>
        /// <typeparam name="TKey">键类型</typeparam>
        /// <typeparam name="TValue">值类型</typeparam>
        private class SafeDictionary<TKey, TValue> : Dictionary<TKey, TValue>
        {
            /// <summary>
            /// 初始化一个<see cref="SafeDictionary{TKey,TValue}"/>类型的实例
            /// </summary>
            public SafeDictionary(IDictionary<TKey, TValue> dictionary, IEqualityComparer<TKey> comparer) 
                : base(dictionary, comparer)
            {
            }

            /// <summary>
            /// 获取或设置指定键值对
            /// </summary>
            /// <param name="key">键名</param>
            public new TValue this[TKey key]
            {
                get => ContainsKey(key) ? base[key] : default;
                set => base[key] = value;
            }
        }
    }
}
