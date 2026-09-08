using System.Windows.Input;
using FileConvertor.Core;

namespace FileConvertor.ViewModels
{
    /// <summary>
    /// 主窗口 ViewModel，负责导航管理
    /// </summary>
    public class MainViewModel : ViewModelBase
    {
        public HomeViewModel Home { get; }
        public OperationViewModel Operation { get; }

        private ViewModelBase _currentView;
        /// <summary>当前显示的视图</summary>
        public ViewModelBase CurrentView
        {
            get => _currentView;
            set => SetProperty(ref _currentView, value);
        }

        private string _windowTitle = "FileConvertor - 文件转换工具";
        public string WindowTitle
        {
            get => _windowTitle;
            set => SetProperty(ref _windowTitle, value);
        }

        public MainViewModel()
        {
            Home = new HomeViewModel(this);
            Operation = new OperationViewModel(this);
            CurrentView = Home;
        }

        /// <summary>导航到首页</summary>
        public void NavigateToHome()
        {
            CurrentView = Home;
            WindowTitle = "FileConvertor - 文件转换工具";
        }

        /// <summary>导航到 PDF 操作详情页</summary>
        public void NavigateToOperation(IPdfOperation operation)
        {
            Operation.SetOperation(operation);
            CurrentView = Operation;
            WindowTitle = $"{operation.Name} - FileConvertor";
        }
    }
}
