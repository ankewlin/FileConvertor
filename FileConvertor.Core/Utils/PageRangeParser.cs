using System;
using System.Collections.Generic;
using System.Linq;

namespace FileConvertor.Core.Utils
{
    /// <summary>
    /// 页面范围解析工具
    /// 支持格式: "1", "1,3,5", "1-5", "1-3,7,10-15"
    /// </summary>
    public static class PageRangeParser
    {
        /// <summary>
        /// 解析页面范围字符串，返回排序后的页码列表（从 1 开始）
        /// </summary>
        public static List<int> Parse(string rangeStr, int totalPages)
        {
            var pages = new HashSet<int>();

            if (string.IsNullOrWhiteSpace(rangeStr))
                return pages.ToList();

            string[] parts = rangeStr.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string part in parts)
            {
                string trimmed = part.Trim();
                if (string.IsNullOrEmpty(trimmed)) continue;

                if (trimmed.Contains("-"))
                {
                    // 范围：如 "3-10"
                    string[] range = trimmed.Split('-');
                    if (range.Length == 2
                        && int.TryParse(range[0].Trim(), out int start)
                        && int.TryParse(range[1].Trim(), out int end))
                    {
                        if (start > end)
                        {
                            int tmp = start;
                            start = end;
                            end = tmp;
                        }
                        start = Math.Max(1, start);
                        end = Math.Min(totalPages, end);
                        for (int i = start; i <= end; i++)
                            pages.Add(i);
                    }
                }
                else
                {
                    // 单页
                    if (int.TryParse(trimmed, out int page))
                    {
                        if (page >= 1 && page <= totalPages)
                            pages.Add(page);
                    }
                }
            }

            return pages.OrderBy(p => p).ToList();
        }

        /// <summary>
        /// 验证页范围字符串是否合法
        /// </summary>
        public static bool IsValid(string rangeStr)
        {
            if (string.IsNullOrWhiteSpace(rangeStr))
                return false;

            string[] parts = rangeStr.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string part in parts)
            {
                string trimmed = part.Trim();
                if (string.IsNullOrEmpty(trimmed)) continue;

                if (trimmed.Contains("-"))
                {
                    string[] range = trimmed.Split('-');
                    if (range.Length != 2) return false;
                    if (!int.TryParse(range[0].Trim(), out _)) return false;
                    if (!int.TryParse(range[1].Trim(), out _)) return false;
                }
                else
                {
                    if (!int.TryParse(trimmed, out _)) return false;
                }
            }
            return true;
        }
    }
}
