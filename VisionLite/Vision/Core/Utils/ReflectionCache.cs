using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using VisionLite.Vision.Core.Attributes;

namespace VisionLite.Vision.Core.Utils
{
    /// <summary>
    /// 反射结果缓存，避免重复反射开销
    /// 用于高效获取处理器的实时参数列表
    /// </summary>
    public static class ReflectionCache
    {
        // 使用线程安全的字典缓存反射结果
        private static readonly ConcurrentDictionary<Type, string[]> _realtimeParamsCache 
            = new ConcurrentDictionary<Type, string[]>();
        
        /// <summary>
        /// 获取处理器的实时参数列表（带缓存）
        /// </summary>
        /// <param name="processorType">处理器类型</param>
        /// <returns>实时参数名称数组</returns>
        public static string[] GetRealtimeParameters(Type processorType)
        {
            if (processorType == null)
                return new string[0];
                
            return _realtimeParamsCache.GetOrAdd(processorType, type =>
            {
                try
                {
                    var realtimeParams = new List<string>();
                    
                    // 获取所有公共属性
                    var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
                    
                    foreach (var property in properties)
                    {
                        // 检查是否有ParameterAttribute标记
                        var paramAttr = property.GetCustomAttribute<ParameterAttribute>();
                        if (paramAttr != null && paramAttr.IsRealtime)
                        {
                            realtimeParams.Add(property.Name);
                        }
                    }
                    
                    // 按字母顺序排序以便调试和日志
                    realtimeParams.Sort();
                    
                    return realtimeParams.ToArray();
                }
                catch (Exception ex)
                {
                    // 记录错误但不抛出异常，返回空数组作为降级方案
                    System.Diagnostics.Debug.WriteLine($"反射获取实时参数失败 [{type.Name}]: {ex.Message}");
                    return new string[0];
                }
            });
        }
        
        /// <summary>
        /// 检查指定参数是否为实时参数
        /// </summary>
        /// <param name="processorType">处理器类型</param>
        /// <param name="parameterName">参数名称</param>
        /// <returns>是否为实时参数</returns>
        public static bool IsRealtimeParameter(Type processorType, string parameterName)
        {
            if (string.IsNullOrEmpty(parameterName))
                return false;
                
            var realtimeParams = GetRealtimeParameters(processorType);
            return realtimeParams.Contains(parameterName);
        }
        
        /// <summary>
        /// 获取缓存统计信息（用于调试和监控）
        /// </summary>
        /// <returns>缓存的处理器类型数量</returns>
        public static int GetCacheSize()
        {
            return _realtimeParamsCache.Count;
        }
        
        /// <summary>
        /// 获取指定处理器的缓存信息（用于调试）
        /// </summary>
        /// <param name="processorType">处理器类型</param>
        /// <returns>调试信息字符串</returns>
        public static string GetCacheInfo(Type processorType)
        {
            if (processorType == null)
                return "处理器类型为空";
                
            var realtimeParams = GetRealtimeParameters(processorType);
            return $"{processorType.Name}: [{string.Join(", ", realtimeParams)}] ({realtimeParams.Length}个实时参数)";
        }
        
        /// <summary>
        /// 清除缓存（用于测试或重新加载）
        /// 注意：生产环境中通常不需要调用此方法
        /// </summary>
        public static void ClearCache()
        {
            _realtimeParamsCache.Clear();
            System.Diagnostics.Debug.WriteLine("ReflectionCache: 缓存已清除");
        }
        
        /// <summary>
        /// 预加载指定处理器的反射信息
        /// 可在应用启动时调用以提升首次使用性能
        /// </summary>
        /// <param name="processorTypes">要预加载的处理器类型列表</param>
        public static void PreloadCache(params Type[] processorTypes)
        {
            if (processorTypes == null) return;
            
            foreach (var type in processorTypes)
            {
                if (type != null)
                {
                    GetRealtimeParameters(type);
                }
            }
            
            System.Diagnostics.Debug.WriteLine($"ReflectionCache: 已预加载 {processorTypes.Length} 个处理器类型");
        }
    }
}