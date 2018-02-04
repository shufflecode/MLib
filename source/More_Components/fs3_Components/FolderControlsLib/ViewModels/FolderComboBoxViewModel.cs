namespace FolderControlsLib.ViewModels
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.IO;
    using System.Linq;
    using System.Windows.Input;
    using FolderControlsLib.Interfaces;
    using FolderControlsLib.ViewModels.Base;
    using FileSystemModels;
    using FileSystemModels.Events;
    using FileSystemModels.Models.FSItems.Base;
    using FileSystemModels.Interfaces;

    /// <summary>
    /// Class implements a viewmodel that can be used for a
    /// combobox that can be used to browse to different folder locations.
    /// </summary>
    internal class FolderComboBoxViewModel : Base.ViewModelBase, IFolderComboBoxViewModel
    {
        #region fields
        private readonly ObservableCollection<IFolderItemViewModel> mCurrentItems;

        private string _CurrentFolder = string.Empty;
        private IFolderItemViewModel _SelectedItem = null;

        private ICommand _SelectionChanged = null;
        private string mSelectedRecentLocation = string.Empty;

        private object mLockObject = new object();
        #endregion fields

        #region constructor
        /// <summary>
        /// Class constructor
        /// </summary>
        public FolderComboBoxViewModel()
        {
            this.mCurrentItems = new ObservableCollection<IFolderItemViewModel>();
        }
        #endregion constructor

        #region Events
        /// <summary>
        /// Event is fired when the path in the text portion of the combobox is changed.
        /// </summary>
        public event EventHandler<FolderChangedEventArgs> RequestChangeOfDirectory;
        #endregion

        #region properties
        /// <summary>
        /// Expose a collection of file system items (folders and hard disks and ...) that
        /// can be selected and navigated to in this viewmodel.
        /// </summary>
        public IEnumerable<IFolderItemViewModel> CurrentItems
        {
            get
            {
                return this.mCurrentItems;
            }
        }

        /// <summary>
        /// Gets/sets the currently selected file system viewmodel item.
        /// </summary>
        public IFolderItemViewModel SelectedItem
        {
            get
            {
                return _SelectedItem;
            }

            protected set
            {
                if (_SelectedItem != value)
                {
                    System.Console.WriteLine("SelectedItem changed to '{0}' -> '{1}'", _SelectedItem, value);
                    _SelectedItem = value;
                    RaisePropertyChanged(() => SelectedItem);
                }
            }
        }

        /// <summary>
        /// Get/sets viewmodel data pointing at the path
        /// of the currently selected folder.
        /// </summary>
        public string CurrentFolder
        {
            get
            {
                return this._CurrentFolder;
            }

            protected set
            {
                if (_CurrentFolder != value)
                {
                    System.Console.WriteLine("CurrentFolder changed to '{0}' -> '{1}'", _CurrentFolder, value);
                    _CurrentFolder = value;
                    RaisePropertyChanged(() => CurrentFolder);
                    RaisePropertyChanged(() => CurrentFolderToolTip);
                }
            }
        }

        /// <summary>
        /// Gets a string that can be displayed as a tooltip for the
        /// viewmodel data pointing at the path of the currently selected folder.
        /// </summary>
        public string CurrentFolderToolTip
        {
            get
            {
                if (string.IsNullOrEmpty(this._CurrentFolder) == false)
                    return string.Format("{0}\n{1}", this._CurrentFolder,
                                                     FileSystemModels.Local.Strings.SelectLocationCommand_TT);
                else
                    return FileSystemModels.Local.Strings.SelectLocationCommand_TT;
            }
        }

        #region commands
        /// <summary>
        /// Gets a command that should be invoked when the combobox view tells
        /// the viewmodel that the current path selection has changed
        /// (via selection changed event or keyup events).
        /// 
        /// The parameter p can be an array of objects
        /// containing objects of the FSItemVM type or
        /// p can also be string.
        /// 
        /// Each parameter item that adheres to the above types results in
        /// a OnCurrentPathChanged event being fired with the folder path
        /// as parameter.
        /// </summary>
        public ICommand SelectionChanged
        {
            get
            {
                if (this._SelectionChanged == null)
                    this._SelectionChanged = new RelayCommand<object>((p) => this.SelectionChanged_Executed(p));

                return this._SelectionChanged;
            }
        }
        #endregion commands
        #endregion properties

        #region methods
        /// <summary>
        /// Resets the currently selected/displayed folder to
        /// the indicated folder.
        /// </summary>
        public void SetCurrentFolder(IPathModel folder)
        {
            CurrentFolder = folder.Path;
        }

        /// <summary>
        /// Can be invoked to refresh the currently visible set of data.
        /// </summary>
        public void PopulateView()
        {
            lock (this.mLockObject)
            {
                ////CurrentItems.Clear();
                string bak = this.CurrentFolder;

                this.mCurrentItems.Clear();
                this.CurrentFolder = bak;

                // add drives
                string pathroot = string.Empty;

                if (string.IsNullOrEmpty(this.CurrentFolder) == false)
                {
                    try
                    {
                        pathroot = System.IO.Path.GetPathRoot(this.CurrentFolder);
                    }
                    catch
                    {
                        pathroot = string.Empty;
                    }
                }

                foreach (string s in Directory.GetLogicalDrives())
                {
                    IFolderItemViewModel info = FolderControlsLib.Factory.CreateLogicalDrive(s);
                    this.mCurrentItems.Add(info);

                    // add items under current folder if we currently create the root folder of the current path
                    if (string.IsNullOrEmpty(pathroot) == false && string.Compare(pathroot, s, true) == 0)
                    {
                        string[] dirs = this.CurrentFolder.Split(new char[] { System.IO.Path.DirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);
                        for (int i = 1; i < dirs.Length; i++)
                        {
                            string curdir = string.Join(string.Empty + System.IO.Path.DirectorySeparatorChar, dirs, 0, i + 1);

                            info = new FolderItemViewModel(curdir, FSItemType.Folder, dirs[i], i * 10);

                            this.mCurrentItems.Add(info);
                        }

                        // currently selected path was expanded in last for loop -> select the last expanded element 
                        if (this.SelectedItem == null)
                        {
                            this.SelectedItem = mCurrentItems.Last();

                            if (this.RequestChangeOfDirectory != null)
                                this.RequestChangeOfDirectory(this, new FolderChangedEventArgs(this.SelectedItem.GetModel));
                        }
                    }
                }

                // Force a selection on to the control when there is no selected item, yet
                if (this.mCurrentItems != null && this.SelectedItem == null)
                {
                    if (this.mCurrentItems.Count > 0)
                    {
                        this.CurrentFolder = this.mCurrentItems[0].FullPath;
                        this.SelectedItem = this.mCurrentItems[0];

                        if (this.RequestChangeOfDirectory != null)
                            this.RequestChangeOfDirectory(this, new FolderChangedEventArgs(this.SelectedItem.GetModel));
                    }
                }
            }
        }

        /// <summary>
        /// Method executes when the SelectionChanged command is invoked.
        /// The parameter <paramref name="p"/> can be an array of objects
        /// containing objects of the <seealso cref="IFolderItemViewModel"/> type
        /// or p can also be string.
        /// 
        /// Each parameter item that adheres to the above types results in
        /// a OnCurrentPathChanged event being fired with the folder path
        /// as parameter.
        /// </summary>
        /// <param name="p"></param>
        private void SelectionChanged_Executed(object p)
        {
            if (p == null)
                return;

            // Check if the given parameter is an array of objects and process it...
            object[] paramObjects = p as object[];
            if (paramObjects != null)
            {
                SetSelection(paramObjects);
                return;
            }

            // Check if the given parameter is a string, fire a corresponding event if so...
            SetSelection(p as string);
        }

        /// <summary>
        /// Fires a folder changed event to indicate that the current folder
        /// selection has changed to the indicated path.
        /// </summary>
        /// <param name="paramString"></param>
        private void SetSelection(string paramString)
        {
            if (paramString == null)
                return;

            var path = PathFactory.Create(paramString, FSItemType.Folder);

            if (path.DirectoryPathExists() == true)
            {
                if (this.RequestChangeOfDirectory != null)
                    this.RequestChangeOfDirectory(this, new FolderChangedEventArgs(path));
            }
        }

        private void SetSelection(object[] paramObjects)
        {
            if (paramObjects == null)
                return;

            for (int i = 0; i < paramObjects.Length; i++)
            {
                var item = paramObjects[i] as IFolderItemViewModel;

                if (item != null)
                {
                    if (item.DirectoryPathExists() == true)
                    {
                        if (this.RequestChangeOfDirectory != null)
                            this.RequestChangeOfDirectory(this, new FolderChangedEventArgs(item.GetModel));
                    }
                }
            }
        }
        #endregion methods
    }
}