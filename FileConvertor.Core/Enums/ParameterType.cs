namespace FileConvertor.Core.Enums
{
    /// <summary>
    /// 操作参数类型
    /// </summary>
    public enum ParameterType
    {
        /// <summary>文本输入</summary>
        Text,

        /// <summary>数字输入</summary>
        Number,

        /// <summary>下拉选择</summary>
        Select,

        /// <summary>复选框</summary>
        Checkbox,

        /// <summary>页码范围（如 "1,3,5-10"）</summary>
        PageRange,

        /// <summary>滑块（数值范围）</summary>
        Slider,

        /// <summary>密码输入</summary>
        Password
    }
}
