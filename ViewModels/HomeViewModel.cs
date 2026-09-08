using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using FileConvertor.Core;

namespace FileConvertor.ViewModels
{
    /// <summary>
    /// 首页 ViewModel：展示所有功能卡片
    /// </summary>
    public class HomeViewModel : ViewModelBase
    {
        public MainViewModel Main { get; }

        /// <summary>所有 PDF 操作</summary>
        public ObservableCollection<IPdfOperation> PdfOperations { get; }

        /// <summary>图像转换操作（用一个包装类）</summary>
        public ObservableCollection<ImageConvertItem> ImageOperations { get; }

        public ICommand SelectOperationCommand { get; }

        public HomeViewModel(MainViewModel main)
        {
            Main = main;
            PdfOperations = new ObservableCollection<IPdfOperation>();
            ImageOperations = new ObservableCollection<ImageConvertItem>();

            SelectOperationCommand = new RelayCommand<IPdfOperation>(OnSelectOperation);

            LoadOperations();
        }

        private void LoadOperations()
        {
            // 加载 PDF 操作
            foreach (var op in PdfOperationRegistry.Instance.GetAll())
            {
                PdfOperations.Add(op);
            }

            // 图像转换（作为单独的卡片）
            ImageOperations.Add(new ImageConvertItem
            {
                Name = "图片格式转换",
                Description = "JPG / PNG 等图片格式互转，支持批量压缩、调整尺寸",
                Icon = "🖼️"
            });
        }

        private void OnSelectOperation(IPdfOperation operation)
        {
            if (operation != null)
            {
                Main.NavigateToOperation(operation);
            }
        }
    }

    /// <summary>
    /// 图像转换卡片项
    /// </summary>
    public class ImageConvertItem
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string Icon { get; set; }
    }
}
