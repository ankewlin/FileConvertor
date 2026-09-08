using FileConvertor.Core.Enums;

namespace FileConvertor.Core.Models
{
    /// <summary>
    /// 操作参数描述。用于动态生成 UI 控件。
    /// </summary>
    public class OperationParameter
    {
        /// <summary>参数键</summary>
        public string Key { get; set; }

        /// <summary>显示标签</summary>
        public string Label { get; set; }

        /// <summary>参数类型</summary>
        public ParameterType Type { get; set; }

        /// <summary>默认值</summary>
        public object DefaultValue { get; set; }

        /// <summary>Select 类型的选项（字符串数组）</summary>
        public object[] Options { get; set; }

        /// <summary>最小值（Slider/Number 用）</summary>
        public double MinValue { get; set; }

        /// <summary>最大值（Slider/Number 用）</summary>
        public double MaxValue { get; set; }

        /// <summary>是否必填</summary>
        public bool IsRequired { get; set; }

        /// <summary>占位提示文字</summary>
        public string Placeholder { get; set; }

        /// <summary>可见性依赖的参数键（为 null 则始终可见）</summary>
        public string VisibleWhenKey { get; set; }

        /// <summary>可见性依赖参数的目标值（等于此值时显示）</summary>
        public object VisibleWhenValue { get; set; }

        public OperationParameter() { }

        public OperationParameter(string key, string label, ParameterType type, object defaultValue = null)
        {
            Key = key;
            Label = label;
            Type = type;
            DefaultValue = defaultValue;
        }
    }
}
