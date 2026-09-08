namespace FileConvertor.Core.Models
{
    /// <summary>
    /// 文件格式描述
    /// </summary>
    public class FileFormat
    {
        /// <summary>格式名称（显示用）</summary>
        public string Name { get; }

        /// <summary>扩展名（不含点号，小写）</summary>
        public string Extension { get; }

        /// <summary>MIME 类型</summary>
        public string MimeType { get; }

        /// <summary>格式分类</summary>
        public ConversionCategory Category { get; }

        public FileFormat(string name, string extension, string mimeType, ConversionCategory category)
        {
            Name = name;
            Extension = extension.ToLowerInvariant().TrimStart('.');
            MimeType = mimeType;
            Category = category;
        }

        public override string ToString()
        {
            return $"{Name} (.{Extension})";
        }

        public override bool Equals(object obj)
        {
            return obj is FileFormat other && Extension == other.Extension;
        }

        public override int GetHashCode()
        {
            return Extension.GetHashCode();
        }
    }
}
