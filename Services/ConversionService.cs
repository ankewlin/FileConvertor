using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FileConvertor.Core;
using FileConvertor.Core.Models;

namespace FileConvertor.Services
{
    /// <summary>
    /// 批量转换服务：负责协调多个文件的转换、进度聚合、取消等
    /// </summary>
    public class ConversionService
    {
        /// <summary>
        /// 批量转换结果
        /// </summary>
        public class BatchResult
        {
            public int TotalCount { get; set; }
            public int SuccessCount { get; set; }
            public int FailedCount { get; set; }
            public List<string> FailedFiles { get; } = new List<string>();
            public List<string> OutputFiles { get; } = new List<string>();
            public string OutputDirectory { get; set; }
        }

        /// <summary>
        /// 单个文件转换进度事件
        /// </summary>
        public event EventHandler<string> FileStarted;

        /// <summary>
        /// 批量转换
        /// </summary>
        /// <param name="inputFiles">输入文件列表</param>
        /// <param name="targetExtension">目标扩展名</param>
        /// <param name="outputDirectory">输出目录（为 null 则与源文件同目录）</param>
        /// <param name="parameters">转换参数</param>
        /// <param name="progress">整体进度 0.0~1.0</param>
        /// <param name="cancellationToken">取消令牌</param>
        public async Task<BatchResult> ConvertBatchAsync(
            IList<string> inputFiles,
            string targetExtension,
            string outputDirectory,
            Dictionary<string, object> parameters,
            IProgress<double> progress,
            CancellationToken cancellationToken)
        {
            var result = new BatchResult
            {
                TotalCount = inputFiles.Count,
                OutputDirectory = outputDirectory
            };

            if (inputFiles.Count == 0)
                return result;

            targetExtension = targetExtension.TrimStart('.').ToLowerInvariant();

            for (int i = 0; i < inputFiles.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                string inputFile = inputFiles[i];
                string fileName = Path.GetFileNameWithoutExtension(inputFile);
                string ext = Path.GetExtension(inputFile).TrimStart('.');

                // 确定输出路径
                string outputDir = string.IsNullOrEmpty(outputDirectory)
                    ? Path.GetDirectoryName(inputFile)
                    : outputDirectory;
                string outputFile = Path.Combine(outputDir, $"{fileName}.{targetExtension}");

                // 如果输出和输入路径相同，加后缀避免覆盖
                if (string.Equals(Path.GetFullPath(outputFile), Path.GetFullPath(inputFile),
                    StringComparison.OrdinalIgnoreCase))
                {
                    outputFile = Path.Combine(outputDir, $"{fileName}_converted.{targetExtension}");
                }

                FileStarted?.Invoke(this, Path.GetFileName(inputFile));

                // 查找转换器
                var converter = ConverterRegistry.Instance.FindConverter(ext, targetExtension);
                if (converter == null)
                {
                    result.FailedCount++;
                    result.FailedFiles.Add($"{Path.GetFileName(inputFile)}: 不支持此格式转换");
                    continue;
                }

                // 单文件进度（占总进度的 1/总数）
                double fileStart = (double)i / inputFiles.Count;
                double fileEnd = (double)(i + 1) / inputFiles.Count;
                var fileProgress = new Progress<double>(p =>
                {
                    double overall = fileStart + p * (fileEnd - fileStart);
                    progress?.Report(overall);
                });

                var context = new ConversionContext
                {
                    InputFilePath = inputFile,
                    OutputFilePath = outputFile,
                    TargetExtension = targetExtension
                };
                if (parameters != null)
                {
                    foreach (var kvp in parameters)
                        context.Parameters[kvp.Key] = kvp.Value;
                }

                var fileResult = await converter.ConvertAsync(context, fileProgress, cancellationToken);

                if (fileResult.Success)
                {
                    result.SuccessCount++;
                    result.OutputFiles.Add(fileResult.OutputFilePath);
                }
                else
                {
                    result.FailedCount++;
                    result.FailedFiles.Add($"{Path.GetFileName(inputFile)}: {fileResult.ErrorMessage}");
                }
            }

            progress?.Report(1.0);
            return result;
        }
    }
}
