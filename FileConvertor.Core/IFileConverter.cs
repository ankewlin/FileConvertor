using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FileConvertor.Core.Models;

namespace FileConvertor.Core
{
    /// <summary>
    /// 文件转换器接口。所有转换器都实现此接口，以插件化方式注册到 ConverterRegistry。
    /// </summary>
    public interface IFileConverter
    {
        /// <summary>转换器名称</summary>
        string Name { get; }

        /// <summary>所属分类</summary>
        ConversionCategory Category { get; }

        /// <summary>描述</summary>
        string Description { get; }

        /// <summary>支持的输入格式</summary>
        IReadOnlyList<FileFormat> SupportedInputFormats { get; }

        /// <summary>支持的输出格式</summary>
        IReadOnlyList<FileFormat> SupportedOutputFormats { get; }

        /// <summary>
        /// 判断是否支持从输入格式转换为输出格式
        /// </summary>
        /// <param name="inputExtension">输入扩展名（不含点号，忽略大小写）</param>
        /// <param name="outputExtension">输出扩展名（不含点号，忽略大小写）</param>
        bool CanConvert(string inputExtension, string outputExtension);

        /// <summary>
        /// 执行转换
        /// </summary>
        /// <param name="context">转换上下文</param>
        /// <param name="progress">进度报告（0.0 ~ 1.0）</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>转换结果</returns>
        Task<ConversionResult> ConvertAsync(
            ConversionContext context,
            IProgress<double> progress = null,
            CancellationToken cancellationToken = default);
    }
}
