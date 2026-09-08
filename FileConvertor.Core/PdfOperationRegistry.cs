using System;
using System.Collections.Generic;
using System.Linq;
using FileConvertor.Core.Enums;

namespace FileConvertor.Core
{
    /// <summary>
    /// PDF 操作注册中心。单例。
    /// </summary>
    public class PdfOperationRegistry
    {
        private static readonly Lazy<PdfOperationRegistry> _instance =
            new Lazy<PdfOperationRegistry>(() => new PdfOperationRegistry());

        public static PdfOperationRegistry Instance => _instance.Value;

        private readonly List<IPdfOperation> _operations = new List<IPdfOperation>();

        private PdfOperationRegistry() { }

        public void Register(IPdfOperation operation)
        {
            if (operation == null) throw new ArgumentNullException(nameof(operation));
            _operations.Add(operation);
        }

        public IReadOnlyList<IPdfOperation> GetAll() => _operations.AsReadOnly();

        public IEnumerable<IPdfOperation> GetByCategory(PdfOperationCategory category)
            => _operations.Where(o => o.Category == category);

        public IPdfOperation FindByName(string name)
            => _operations.FirstOrDefault(o => o.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }
}
