using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FileConvertor.Core.Enums;
using FileConvertor.Core.Models;

namespace FileConvertor.Core
{
    /// <summary>
    /// PDF 操作接口。支持多输入多输出、复杂参数配置。
    /// </summary>
    public interface IPdfOperation
    {
        /// <summary>操作名称</summary>
        string Name { get; }

        /// <summary>操作描述</summary>
        string Description { get; }

        /// <summary>图标（emoji 字符）</summary>
        string Icon { get; }

        /// <summary>分类</summary>
        PdfOperationCategory Category { get; }

        /// <summary>是否需要多文件输入</summary>
        bool RequiresMultipleInputs { get; }

        /// <summary>是否产生多文件输出</summary>
        bool ProducesMultipleOutputs { get; }

        /// <summary>支持的输入文件扩展名（不含点号，小写）</summary>
        IReadOnlyList<string> SupportedInputExtensions { get; }

        /// <summary>参数定义列表（用于动态生成 UI）</summary>
        IReadOnlyList<OperationParameter> Parameters { get; }

        /// <summary>
        /// 执行操作
        /// </summary>
        Task<PdfOperationResult> ExecuteAsync(
            PdfOperationContext context,
            IProgress<double> progress = null,
            CancellationToken cancellationToken = default);
    }
}
