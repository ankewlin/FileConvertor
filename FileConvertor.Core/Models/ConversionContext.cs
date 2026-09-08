using System.Collections.Generic;

namespace FileConvertor.Core.Models
{
    /// <summary>
    /// 转换上下文：包含输入输出信息及参数
    /// </summary>
    public class ConversionContext
    {
        /// <summary>输入文件完整路径</summary>
        public string InputFilePath { get; set; }

        /// <summary>输出文件完整路径</summary>
        public string OutputFilePath { get; set; }

        /// <summary>目标扩展名（不含点号，小写）</summary>
        public string TargetExtension { get; set; }

        /// <summary>扩展参数字典</summary>
        public Dictionary<string, object> Parameters { get; } = new Dictionary<string, object>();

        /// <summary>获取参数值，不存在则返回默认值</summary>
        public T GetParameter<T>(string key, T defaultValue = default(T))
        {
            if (Parameters.TryGetValue(key, out var value) && value is T typed)
                return typed;
            return defaultValue;
        }
    }
}
