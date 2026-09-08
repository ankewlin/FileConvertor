using System.Collections.Generic;

namespace FileConvertor.Core.Models
{
    /// <summary>
    /// PDF 操作上下文
    /// </summary>
    public class PdfOperationContext
    {
        /// <summary>输入文件路径列表</summary>
        public List<string> InputFiles { get; set; } = new List<string>();

        /// <summary>输出目录</summary>
        public string OutputDirectory { get; set; }

        /// <summary>输出文件名（仅单输出时有效）</summary>
        public string OutputFileName { get; set; }

        /// <summary>参数字典</summary>
        public Dictionary<string, object> Parameters { get; } = new Dictionary<string, object>();

        /// <summary>获取参数值</summary>
        public T GetParameter<T>(string key, T defaultValue = default(T))
        {
            if (Parameters.TryGetValue(key, out var value) && value is T typed)
                return typed;
            return defaultValue;
        }

        /// <summary>获取字符串参数</summary>
        public string GetString(string key, string defaultValue = "")
        {
            if (Parameters.TryGetValue(key, out var value) && value != null)
                return value.ToString();
            return defaultValue;
        }

        /// <summary>获取整数参数</summary>
        public int GetInt(string key, int defaultValue = 0)
        {
            if (Parameters.TryGetValue(key, out var value))
            {
                if (value is int i) return i;
                if (value is double d) return (int)d;
                if (int.TryParse(value.ToString(), out var result)) return result;
            }
            return defaultValue;
        }
    }
}
