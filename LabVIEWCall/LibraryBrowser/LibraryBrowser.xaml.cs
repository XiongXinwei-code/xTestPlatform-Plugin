using LabVIEWCallPlugin.LVadapter;
using Syncfusion.UI.Xaml.Grid;
using Syncfusion.UI.Xaml.TreeView;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Services;

namespace LibraryBrowser
{
    /// <summary>
    /// 库项选中事件参数
    /// </summary>
    public class LibraryItemSelectedEventArgs : RoutedEventArgs
    {
        public string FullPath { get; set; } = string.Empty;
        public LibraryNode? SelectedNode { get; set; }

        public LibraryItemSelectedEventArgs(RoutedEvent routedEvent, object source)
            : base(routedEvent, source)
        {
        }
    }

    /// <summary>
    /// 库浏览器控件 - 用于浏览和管理 LabVIEW 库文件
    /// </summary>
    public partial class LibraryBrowserWindow : Window
    {
        private readonly IProjectService? _projectService;
        private readonly IDialogService? _dialogService;
        private readonly ObservableCollection<LibraryNode> _libraryNodes;

        #region 路由事件定义

        /// <summary>
        /// 库项选中路由事件
        /// </summary>
        public static readonly RoutedEvent LibraryItemSelectedEvent =
            EventManager.RegisterRoutedEvent(
                "LibraryItemSelected",
                RoutingStrategy.Bubble,
                typeof(EventHandler<LibraryItemSelectedEventArgs>),
                typeof(LibraryBrowserWindow));

        /// <summary>
        /// 库项选中事件
        /// </summary>
        public event EventHandler<LibraryItemSelectedEventArgs> LibraryItemSelected
        {
            add { AddHandler(LibraryItemSelectedEvent, value); }
            remove { RemoveHandler(LibraryItemSelectedEvent, value); }
        }

        #endregion

        #region 依赖属性 - 暴露选中项

        /// <summary>
        /// 选中的文件路径依赖属性
        /// </summary>
        public static readonly DependencyProperty SelectedFilePathProperty =
            DependencyProperty.Register(
                nameof(SelectedFilePath),
                typeof(string),
                typeof(LibraryBrowserWindow),
                new PropertyMetadata(string.Empty));

        /// <summary>
        /// 选中的文件路径
        /// </summary>
        public string SelectedFilePath
        {
            get => (string)GetValue(SelectedFilePathProperty);
            set => SetValue(SelectedFilePathProperty, value);
        }

        #endregion

        /// <summary>
        /// 构造函数 - 从工程服务获取库路径
        /// </summary>
        /// <param name="projectService">工程服务实例</param>
        public LibraryBrowserWindow(IProjectService projectService, IDialogService dialogService)
        {
            InitializeComponent();
            _projectService = projectService;
            _dialogService = dialogService;
            _libraryNodes = new ObservableCollection<LibraryNode>();
            LibraryTreeGrid.ItemsSource = _libraryNodes;

            // 订阅TreeView选中项改变事件
            LibraryTreeGrid.SelectionChanged += LibraryTreeGrid_SelectionChanged;
            LibraryTreeGrid.MouseDoubleClick += LibraryTreeGrid_MouseDoubleClick;

            // 设置窗口属性
            InitializeWindowProperties();

            LoadLibraries();
        }

        /// <summary>
        /// 初始化窗口属性 - 设置模态对话框属性
        /// </summary>
        private void InitializeWindowProperties()
        {
            // 设置窗口启动位置为父窗口中心（如果有父窗口）或屏幕中心
            WindowStartupLocation = WindowStartupLocation.CenterOwner;

            // 设置窗口样式为对话框
            ShowInTaskbar = false;
            ResizeMode = ResizeMode.CanResize;

            // 设置默认大小
            Width = 400;
            Height = 500;

            // 订阅 Loaded 事件以在窗口加载后激活
            Loaded += (s, e) =>
            {
                
                Activate();
                Focus();

            };
        }

        /// <summary>
        /// 显示库浏览器对话框
        /// </summary>
        /// <param name="owner">父窗口</param>
        /// <param name="projectService">工程服务</param>
        /// <returns>选中的文件路径，如果取消则返回 null</returns>
        public static string? ShowDialog(Window owner, IProjectService projectService, IDialogService dialogService)
        {
            var browser = new LibraryBrowserWindow(projectService, dialogService)
            {
                Owner = owner
            };

            if (browser.ShowDialog() == true)
            {
                return browser.SelectedFilePath;
            }

            return null;
        }

        /// <summary>
        /// TreeView 选中项改变事件处理
        /// </summary>
        private void LibraryTreeGrid_SelectionChanged(object sender, GridSelectionChangedEventArgs e)
        {
            if (LibraryTreeGrid.SelectedItem is LibraryNode selectedNode)
            {
                // 更新依赖属性
                SelectedFilePath = selectedNode.FullPath;

                // 更新路径文本框
                SelectedPathTextBox.Text = selectedNode.FullPath;

                // 触发路由事件,传递选中项信息
                var args = new LibraryItemSelectedEventArgs(LibraryItemSelectedEvent, this)
                {
                    FullPath = selectedNode.FullPath,
                    SelectedNode = selectedNode
                };
                RaiseEvent(args);

                // 可选: 在输出窗口显示日志
                System.Diagnostics.Debug.WriteLine($"[LibraryBrowser] 选中项: {selectedNode.Name}, 路径: {selectedNode.FullPath}");
            }
        }

        /// <summary>
        /// 加载工程中的所有库文件
        /// </summary>
        private void LoadLibraries()
        {
            _libraryNodes.Clear();

            if (_projectService?.CurrentProject == null)
            {
                // 添加提示节点
                _libraryNodes.Add(new LibraryNode
                {
                    Name = "未打开工程",
                    IconColor = new SolidColorBrush(Colors.Orange),
                    NodeType = NodeType.Info
                });
                return;
            }

            var project = _projectService.CurrentProject;
            var projectDir = _projectService.ProjectDirectory;

            if (project.LibraryReferences == null || project.LibraryReferences.Count == 0)
            {
                _libraryNodes.Add(new LibraryNode
                {
                    Name = "未找到库引用",
                    IconColor = new SolidColorBrush(Colors.Gray),
                    NodeType = NodeType.Info
                });
                return;
            }

            // 遍历所有库引用
            foreach (var libRef in project.LibraryReferences)
            {
                if (!libRef.IsEnabled)
                    continue;

                // 构建完整路径
                string libPath = Path.IsPathRooted(libRef.RelativePath)
                    ? libRef.RelativePath
                    : Path.Combine(projectDir ?? string.Empty, libRef.RelativePath);

                if (!File.Exists(libPath))
                {
                    // 库文件不存在
                    _libraryNodes.Add(new LibraryNode
                    {
                        Name = libRef.Name,
                        FullPath = libPath,
                        IsMissing = !File.Exists(libPath),
                        IconImage = GetIconForLibraryType(libRef.LibraryType),
                        IconColor = new SolidColorBrush(Colors.Red),
                        NodeType = NodeType.Error
                    });
                    continue;
                }

                // 创建库根节点
                var libraryNode = new LibraryNode
                {
                    Name = string.IsNullOrEmpty(libRef.Name) ? Path.GetFileName(libPath) : libRef.Name,
                    FullPath = libPath,
                    IconImage = GetIconForLibraryType(libRef.LibraryType), 
                    IconColor = new SolidColorBrush(Colors.Blue),
                    NodeType = NodeType.Library,
                    LibraryType = libRef.LibraryType
                };

                // 如果是 LabVIEW 库，加载其包含的文件
                if (libRef.LibraryType == LibraryType.LabVIEW)
                {
                    LoadLabVIEWLibraryItems(libraryNode, libPath);
                }

                _libraryNodes.Add(libraryNode);
               
            }
        }

        /// <summary>
        /// 加载 LabVIEW 库中的项目
        /// </summary>
        private void LoadLabVIEWLibraryItems(LibraryNode parentNode, string lvLibPath)
        {
            try
            {
                // 调用 GetLibItems 获取库中的所有项目
                var items = LvLibHelper.GetLibItems(lvLibPath);

                if (items == null || items.Count == 0)
                {
                    parentNode.Children.Add(new LibraryNode
                    {
                        Name = "(空库)",
                        IconColor = new SolidColorBrush(Colors.Gray),
                        NodeType = NodeType.Info
                    });
                    return;
                }

                // 获取库文件所在目录作为基准路径
                string libraryDirectory = Path.GetDirectoryName(lvLibPath) ?? string.Empty;

                // 添加每个项目作为子节点，按照相对路径构建文件夹层次
                foreach (var itemPath in items)
                {
                    AddItemWithFolderHierarchy(parentNode, itemPath, libraryDirectory);
                }
            }
            catch (Exception ex)
            {
                // 加载失败，显示错误信息
                parentNode.Children.Add(new LibraryNode
                {
                    Name = $"加载失败: {ex.Message}",
                    IconColor = new SolidColorBrush(Colors.Red),
                    NodeType = NodeType.Error
                });
            }
        }

        /// <summary>
        /// 根据相对路径添加项目，创建必要的文件夹层次结构
        /// </summary>
        /// <param name="parentNode">父节点（库节点或文件夹节点）</param>
        /// <param name="itemPath">项目的完整路径</param>
        /// <param name="basePath">基准路径（库文件所在目录）</param>
        private void AddItemWithFolderHierarchy(LibraryNode parentNode, string itemPath, string basePath)
        {
            try
            {
                // 计算相对路径
                string relativePath = GetRelativePath(basePath, itemPath);

                // 分割路径为各个部分
                string[] pathParts = relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

                // 如果只有文件名（没有子文件夹），直接添加到父节点
                if (pathParts.Length == 1)
                {
                    parentNode.Children.Add(CreateFileNode(itemPath));
                    return;
                }

                // 需要创建文件夹层次结构
                LibraryNode currentParent = parentNode;

                // 遍历路径部分（除了最后一个文件名）
                for (int i = 0; i < pathParts.Length - 1; i++)
                {
                    string folderName = pathParts[i];

                    // 查找是否已存在该文件夹节点
                    var existingFolder = currentParent.Children.FirstOrDefault(
                        c => c.NodeType == NodeType.Folder && c.Name == folderName);

                    if (existingFolder == null)
                    {
                        // 创建新的文件夹节点
                        existingFolder = new LibraryNode
                        {
                            Name = folderName,
                            FullPath = Path.Combine(basePath, string.Join(Path.DirectorySeparatorChar.ToString(), pathParts.Take(i + 1))),
                            IconImage = GetIconForFolder(),
                            IconColor = new SolidColorBrush(Colors.DarkGoldenrod),
                            NodeType = NodeType.Folder
                            
                        };
                        currentParent.Children.Add(existingFolder);
                    }

                    currentParent = existingFolder;
                }

                // 添加文件节点到最终的父文件夹
                currentParent.Children.Add(CreateFileNode(itemPath));
            }
            catch (Exception ex)
            {
                // 如果路径处理失败，直接添加到父节点（降级处理）
                System.Diagnostics.Debug.WriteLine($"[LibraryBrowser] 处理路径失败: {itemPath}, 错误: {ex.Message}");
                parentNode.Children.Add(CreateFileNode(itemPath));
            }
        }

        /// <summary>
        /// 获取文件夹图标
        /// </summary>
        private ImageSource? GetIconForFolder()
        {
            string iconPath = "pack://application:,,,/LibraryBrowser;component/Resources/Icons/folder.png";
            return LoadImageFromPath(iconPath);
        }

        /// <summary>
        /// 创建文件节点
        /// </summary>
        private LibraryNode CreateFileNode(string filePath)
        {
            return new LibraryNode
            {
                Name = Path.GetFileName(filePath),
                FullPath = filePath,
                IsMissing = !File.Exists(filePath),
                IconImage = GetIconForFileExtension(Path.GetExtension(filePath)),
                IconColor = new SolidColorBrush(Colors.Green),
                NodeType = NodeType.File
            };
        }

        /// <summary>
        /// 计算相对路径
        /// </summary>
        /// <param name="basePath">基准路径</param>
        /// <param name="fullPath">完整路径</param>
        /// <returns>相对路径</returns>
        private string GetRelativePath(string basePath, string fullPath)
        {
            // 标准化路径分隔符
            basePath = Path.GetFullPath(basePath);
            fullPath = Path.GetFullPath(fullPath);

            // 使用 Uri 类计算相对路径
            Uri baseUri = new Uri(basePath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar);
            Uri fullUri = new Uri(fullPath);

            Uri relativeUri = baseUri.MakeRelativeUri(fullUri);
            string relativePath = Uri.UnescapeDataString(relativeUri.ToString());

            // 将 URL 风格的斜杠替换为系统路径分隔符
            relativePath = relativePath.Replace('/', Path.DirectorySeparatorChar);

            return relativePath;
        }

        /// <summary>
        /// 根据库类型获取图标
        /// </summary>
        private ImageSource? GetIconForLibraryType(LibraryType libraryType)
        {
            string iconPath = libraryType switch
            {
                LibraryType.LabVIEW => "pack://application:,,,/LibraryBrowser;component/Resources/Icons/lvlib.png",
                LibraryType.DotNet => "pack://application:,,,/LibraryBrowser;component/Resources/Icons/lvlib.png",
                LibraryType.Python => "pack://application:,,,/LibraryBrowser;component/Resources/Icons/lvlib.png",
                _ => "pack://application:,,,/LibraryBrowser;component/Resources/Icons/lvlib.png"
            };

            return LoadImageFromPath(iconPath);
        }

        /// <summary>
        /// 根据文件扩展名获取图标
        /// </summary>
        private ImageSource? GetIconForFileExtension(string extension)
        {
            string iconPath = extension.ToLowerInvariant() switch
            {
                ".vi" => "pack://application:,,,/LibraryBrowser;component/Resources/Icons/VI.png",
                ".dll" => "pack://application:,,,/LibraryBrowser;component/Resources/Icons/VI.png",
                ".py" => "pack://application:,,,/LibraryBrowser;component/Resources/Icons/VI.png",
                _ => "pack://application:,,,/LibraryBrowser;component/Resources/Icons/VI.png"
            };

            return LoadImageFromPath(iconPath);
        }

        /// <summary>
        /// 从路径加载图片，支持 Pack URI 和文件系统路径
        /// </summary>
        /// <param name="path">图片路径</param>
        /// <returns>ImageSource 或 null（加载失败时）</returns>
        private ImageSource? LoadImageFromPath(string path)
        {
            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(path, UriKind.RelativeOrAbsolute);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                bitmap.Freeze(); // 冻结以提高性能
                return bitmap;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LibraryBrowser] 加载图标失败: {path}, 错误: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 刷新按钮点击事件
        /// </summary>
        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            LoadLibraries();
            LibraryTreeGrid.ExpandAllNodes();
        }

        /// <summary>
        /// 确定按钮点击事件
        /// </summary>
        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            // 验证是否选中了有效项
            if (string.IsNullOrEmpty(SelectedFilePath))
            {
                _dialogService?.ShowMessage("请选择一个库项", "提示");
                return;
            }

            // 设置对话框结果为 true
            DialogResult = true;
        }

        private void LibraryTreeGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            // 如果 DialogResult 已经被设置，直接返回，避免重复设置
            if (DialogResult.HasValue)
            {
                return;
            }

            if (LibraryTreeGrid.SelectedItem is LibraryNode selectedNode)
            {
                // 只有在选中的是文件节点时才触发确定操作
                if (selectedNode.NodeType == NodeType.File)
                {
                    DialogResult = true;
                }
            }
        }

        /// <summary>
        /// 取消按钮点击事件
        /// </summary>
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            // 设置对话框结果为 false
            DialogResult = false;
        }
    }

    /// <summary>
    /// 库节点数据模型
    /// </summary>
    public class LibraryNode
    {
        public string Name { get; set; } = string.Empty;
        public string FullPath { get; set; } = string.Empty;
        public Brush IconColor { get; set; } = new SolidColorBrush(Colors.Black);
        public ImageSource? IconImage { get; set; }
        public NodeType NodeType { get; set; }
        public LibraryType LibraryType { get; set; }
        public bool IsMissing { get; set; } = false;
        public ObservableCollection<LibraryNode> Children { get; set; } = new ObservableCollection<LibraryNode>();
    }

    /// <summary>
    /// 节点类型
    /// </summary>
    public enum NodeType
    {
        Library,    // 库根节点
        File,       // 文件节点
        Folder,     // 文件夹节点
        Info,       // 信息节点
        Error       // 错误节点
    }
    /// <summary>
    /// 字符串空值到可见性转换器
    /// </summary>
    public class StringEmptyToVisibilityConverter : IValueConverter
    {
        public static StringEmptyToVisibilityConverter Instance { get; } = new StringEmptyToVisibilityConverter();

        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return string.IsNullOrEmpty(value as string) ? Visibility.Collapsed : Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}