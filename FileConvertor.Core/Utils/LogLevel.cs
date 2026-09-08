namespace FileConvertor.Core.Utils
{
    /// <summary>
    /// 日志等级
    /// </summary>
    public enum LogLevel
    {
        /// <summary>调试信息（仅 Debug 配置下输出）</summary>
        Debug,
        /// <summary>一般信息（操作开始、完成等）</summary>
        Info,
        /// <summary>警告（非致命异常、可恢复错误）</summary>
        Warning,
        /// <summary>错误（操作失败、异常）</summary>
        Error
    }
}
