namespace FileConvertor.Core
{
    /// <summary>
    /// 转换类别
    /// </summary>
    public enum ConversionCategory
    {
        /// <summary>图像转换</summary>
        Image,

        /// <summary>文档转换</summary>
        Document,

        /// <summary>音频转换</summary>
        Audio,

        /// <summary>视频转换</summary>
        Video,

        /// <summary>压缩包</summary>
        Archive,

        /// <summary>数据格式（JSON/XML/YAML等）</summary>
        DataFormat,

        /// <summary>二维码/条形码</summary>
        QrCode,

        /// <summary>文本编码</summary>
        TextEncoding,

        /// <summary>其他</summary>
        Other
    }
}
