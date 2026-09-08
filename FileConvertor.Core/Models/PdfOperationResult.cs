using System.Collections.Generic;

namespace FileConvertor.Core.Models
{
    /// <summary>
    /// PDF 操作结果
    /// </summary>
    public class PdfOperationResult
    {
        /// <summary>是否成功</summary>
        public bool Success { get; }

        /// <summary>输出文件列表</summary>
        public List<string> OutputFiles { get; } = new List<string>();

        /// <summary>错误信息</summary>
        public string ErrorMessage { get; }

        private PdfOperationResult(bool success, string error)
        {
            Success = success;
            ErrorMessage = error;
        }

        public static PdfOperationResult Ok(IEnumerable<string> outputFiles)
        {
            var result = new PdfOperationResult(true, null);
            result.OutputFiles.AddRange(outputFiles);
            return result;
        }

        public static PdfOperationResult Ok(string outputFile)
        {
            var result = new PdfOperationResult(true, null);
            result.OutputFiles.Add(outputFile);
            return result;
        }

        public static PdfOperationResult Fail(string errorMessage)
        {
            return new PdfOperationResult(false, errorMessage);
        }
    }
}
