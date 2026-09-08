namespace FileConvertor.Core.Models
{
    /// <summary>
    /// 转换结果
    /// </summary>
    public class ConversionResult
    {
        /// <summary>是否成功</summary>
        public bool Success { get; }

        /// <summary>输出文件路径（成功时）</summary>
        public string OutputFilePath { get; }

        /// <summary>错误信息（失败时）</summary>
        public string ErrorMessage { get; }

        private ConversionResult(bool success, string outputPath, string error)
        {
            Success = success;
            OutputFilePath = outputPath;
            ErrorMessage = error;
        }

        public static ConversionResult Ok(string outputPath)
            => new ConversionResult(true, outputPath, null);

        public static ConversionResult Fail(string errorMessage)
            => new ConversionResult(false, null, errorMessage);
    }
}
