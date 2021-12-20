using System;
using System.Reflection;
using CSRedis;
using Hangfire.Storage;

namespace Hangfire.Tags.Redis.CSRedisCore
{
    /// <summary>
    /// Redis标签监控API
    /// </summary>
    internal class RedisTagsMonitoringApi
    {
        /// <summary>
        /// 监控API
        /// </summary>
        private readonly IMonitoringApi _monitoringApi;

        /// <summary>
        /// 类型
        /// </summary>
        private static Type _type;

        /// <summary>
        /// 使用连接方法
        /// </summary>
        private static MethodInfo _useConnection;

        public RedisTagsMonitoringApi(IMonitoringApi monitoringApi)
        {
            if (monitoringApi.GetType().Name != "RedisMonitoringApi")
                throw new ArgumentException("The monitor API is not implemented using Redis.", nameof(monitoringApi));
            _monitoringApi = monitoringApi;
            if (_type != monitoringApi.GetType())
            {
                _useConnection = null;
                _type = monitoringApi.GetType();
            }

            if (_useConnection == null)
                _useConnection = monitoringApi.GetType().GetTypeInfo().GetMethod(nameof(UseConnection), BindingFlags.NonPublic | BindingFlags.Instance);

            if (_useConnection == null)
                throw new ArgumentException("The function UseConnection cannot be found.");
        }

        /// <summary>
        /// 使用连接
        /// </summary>
        /// <typeparam name="T">类型</typeparam>
        /// <param name="action">操作</param>
        public T UseConnection<T>(Func<CSRedisClient, T> action)
        {
            var method = _useConnection.MakeGenericMethod(typeof(T));
            return (T)method.Invoke(_monitoringApi, new object[] { action });
        }
    }
}
