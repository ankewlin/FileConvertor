using System;
using System.Collections.Generic;
using System.Linq;
using FileConvertor.Core.Models;

namespace FileConvertor.Core
{
    /// <summary>
    /// 转换器注册中心。单例，管理所有已注册的转换器。
    /// </summary>
    public class ConverterRegistry
    {
        private static readonly Lazy<ConverterRegistry> _instance =
            new Lazy<ConverterRegistry>(() => new ConverterRegistry());

        public static ConverterRegistry Instance => _instance.Value;

        private readonly List<IFileConverter> _converters = new List<IFileConverter>();

        private ConverterRegistry() { }

        /// <summary>注册转换器</summary>
        public void Register(IFileConverter converter)
        {
            if (converter == null) throw new ArgumentNullException(nameof(converter));
            _converters.Add(converter);
        }

        /// <summary>获取所有已注册转换器</summary>
        public IReadOnlyList<IFileConverter> GetAll() => _converters.AsReadOnly();

        /// <summary>按分类获取转换器</summary>
        public IEnumerable<IFileConverter> GetByCategory(ConversionCategory category)
            => _converters.Where(c => c.Category == category);

        /// <summary>
        /// 查找能处理指定输入格式的转换器
        /// </summary>
        public IFileConverter FindConverter(string inputExtension, string outputExtension)
        {
            string extIn = inputExtension.TrimStart('.').ToLowerInvariant();
            string extOut = outputExtension.TrimStart('.').ToLowerInvariant();
            return _converters.FirstOrDefault(c => c.CanConvert(extIn, extOut));
        }

        /// <summary>
        /// 获取指定输入扩展名支持的所有输出格式
        /// </summary>
        public IEnumerable<FileFormat> GetAvailableOutputFormats(string inputExtension)
        {
            string ext = inputExtension.TrimStart('.').ToLowerInvariant();
            var result = new List<FileFormat>();
            foreach (var converter in _converters)
            {
                foreach (var inputFmt in converter.SupportedInputFormats)
                {
                    if (inputFmt.Extension.Equals(ext, StringComparison.OrdinalIgnoreCase))
                    {
                        foreach (var outputFmt in converter.SupportedOutputFormats)
                        {
                            if (!outputFmt.Extension.Equals(ext, StringComparison.OrdinalIgnoreCase)
                                && !result.Contains(outputFmt))
                            {
                                result.Add(outputFmt);
                            }
                        }
                        break;
                    }
                }
            }
            return result;
        }

        /// <summary>
        /// 获取所有支持的输入扩展名
        /// </summary>
        public IEnumerable<string> GetAllInputExtensions()
        {
            return _converters
                .SelectMany(c => c.SupportedInputFormats)
                .Select(f => f.Extension)
                .Distinct()
                .OrderBy(e => e);
        }
    }
}
