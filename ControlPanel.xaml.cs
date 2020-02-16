using System;
using System.Linq;
using System.Windows;
using LiteDB;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Media;
using System.Drawing;
using Microsoft.Win32;
using System.Diagnostics;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace GuestControl
{
    public partial class ControlPanel : Window
    {
        //general instance variables
        private bool _backgroundTask;
        private volatile bool CPLThreadRunning;
        private InternalProcessControl IPC;
        System.Windows.Forms.NotifyIcon notifyIcon = new System.Windows.Forms.NotifyIcon();
        Thread CPLThread;
        private ObservableCollection<string> _internalProcessList = new ObservableCollection<string>();
        private List<Process> _PinternalProcessesList = new List<Process>();
        private Dictionary<string, Process> _DPinternalProcessesList = new Dictionary<string, Process>();
        //private static object _syncLock = new object();


        //for the running process list management
        List<string> closedProcessList = new List<string>();


        //anti-force close
        [DllImport("ntdll.dll", SetLastError = true)]
        private static extern void RtlSetProcessIsCritical(UInt32 v1, UInt32 v2, UInt32 v3);
        private static volatile bool s_isProtected = false;
        private static ReaderWriterLockSlim s_isProtectedLock = new ReaderWriterLockSlim();


        public ControlPanel()
        {
            //initialization
            InitializeComponent();
            IPC = new InternalProcessControl();

            //data entry field settings
            fetchProgramData();
            ListModificationStatusLabel.Visibility = Visibility.Collapsed;//hide label on start up

            //event handling
            AddButton.Click += OnAddButtonClicked;
            RemoveButton.Click += OnRemoveButtonClicked;
            ActivateButton.Click += onActivateButtonClick;
            AddButtonRL.Click += AddButtonRL_Click;

            //prevent force kill check button
            PreventForceKillButton.Checked += onPreventForceKillButtonAction;
            PreventForceKillButton.Unchecked += onPreventForceKillButtonAction;
            SessionEndedEventHandler sessionEndedEvent = new SessionEndedEventHandler(ControlPanel_Closed);

            //background task check button
            BackgroundTaskButton.Checked += onBackgroundTaskButtonAction;
            BackgroundTaskButton.Unchecked += onBackgroundTaskButtonAction;

            //on window closing event
            //this.Closing += onUserClose;
            this.Closed += onUserClose;

            notifyIcon.DoubleClick += delegate (object sender, EventArgs args)
            {
                Show();
                WindowState = WindowState.Normal;
            };

            //Running Program List Box Initialization  --> wdym?
            //BindingOperations.EnableCollectionSynchronization(RunningProgramList.ItemsSource, _syncLock);
            RunningProgramList.ItemsSource = _internalProcessList;

            //start the current process thread
            CPLThread = new Thread(() => constantCPLUpdateThread());
            CPLThreadRunning = true;
            CPLThread.Start();
        }

        #region Application Ending events
        private void ControlPanel_Closed(object sender, SessionEndedEventArgs e)//if the user begins a system shutdown/restart, frees memory and ends threads 
        { 
            Unprotect();
            IPC.setThreadStatus(InternalProcessControl.ThreadStatus.terminate);
            CPLThreadRunning = false;
        }

        private void onUserClose(object sender, EventArgs args)
        {
            Unprotect();
            IPC.setThreadStatus(InternalProcessControl.ThreadStatus.terminate);
            CPLThreadRunning = false;
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            //getSettings();
            if (_backgroundTask)
            {
                notifyIcon.Visible = true;
                notifyIcon.Icon = new Icon("eye.ICO");
                Hide();
                e.Cancel = true;
                //WindowState = WindowState.Minimized;
                //base.OnClosing(e);
            }
            else
            {
                Unprotect();
                IPC.setThreadStatus(InternalProcessControl.ThreadStatus.terminate);
            }
            CPLThreadRunning = false;
        }
        #endregion

        #region CheckedButtonInputs
        private void onPreventForceKillButtonAction(object sender, EventArgs args)
        {
            if ((bool)PreventForceKillButton.IsChecked)
            {
                Protect();
            }
 
            if (!((bool)PreventForceKillButton.IsChecked))
            {
                Unprotect();
            }    
        }

        private void onBackgroundTaskButtonAction(object sender, EventArgs args)
        {
            if ((bool)BackgroundTaskButton.IsChecked)
                _backgroundTask = true;
            if (!(bool)BackgroundTaskButton.IsChecked)
                _backgroundTask = false;
        }

        #endregion

        //private void startOnBoot()//run program as soon as system boots up again --> need to do
        //{
        //    RegistryKey rk = Registry.CurrentUser.OpenSubKey
        //    ("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);

        //    if (_systemBoot)
        //        rk.SetValue("Guest Control", Path.GetDirectoryName(System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName));
        //    else
        //        rk.DeleteValue("Guest Control", false);
        //}

        #region UI Button Events
        private void onActivateButtonClick(object sender, EventArgs a)//need to do
        {

            //start the actual Guest Control
            InternalProcessControl IPC = new InternalProcessControl();
            IPC.setThreadStatus(InternalProcessControl.ThreadStatus.start);
            IPC.startProcessControl();

            //Inform User GC is now running
            ActivateTextResult.Text = "Guest Control is now running!";
            ActivateTextResult.Foreground = new SolidColorBrush(Colors.Green);
            ActivateTextResult.Visibility = Visibility.Visible;
        }

        private void AddButtonRL_Click(object sender, RoutedEventArgs e)
        {
            //get the selected program from the Running Programs List
            var selectedProgram = (string)RunningProgramList.SelectedItem;
            //store the program in the database
            using (var db = new LiteDatabase(@"C:\Temp\GCData.db"))
            {
                //get or create collection
                var col = db.GetCollection<SharedData>("users");
                var result = col.FindById(MainWindow.User.Id);

                //insert new user - id gets incremented automatically
                try
                {
                    //var userInfo = col.FindOne(x => SharedData.checkHashes(x.Username, MainWindow.Username).Replace("-", string.Empty));

                    if (result.ProgramList != null && (result.ProgramList.Any()))//make sure the list is not empty
                    {
                        if (result.ProgramList.Count >= 1)//check if the list has 1+ elements
                        {
                            foreach (var program in result.ProgramList)
                            {
                                if (selectedProgram.Equals(program, StringComparison.CurrentCultureIgnoreCase))//if the current program exists within the list
                                {
                                    ListModificationStatusLabel.Text = "This program is already stored in the program list!";
                                    ListModificationStatusLabel.Visibility = Visibility.Visible;
                                    //break;
                                    return;
                                }

                            }
                            result.ProgramList.Add(selectedProgram);//add the program to the list
                            ProgramList.Items.Add(selectedProgram);
                        }
                        else
                        {
                            result.ProgramList.Add(selectedProgram);
                            ProgramList.Items.Add(selectedProgram);
                            //UserDataEntryProgram.IsLoaded
                            ListModificationStatusLabel.Text = "Program has been successfully added to the program list!";
                            ListModificationStatusLabel.Foreground = new SolidColorBrush(Colors.Green);
                            ListModificationStatusLabel.Visibility = Visibility.Visible;
                        }
                    }
                    else
                    {
                        result.ProgramList.Add(selectedProgram);
                        ProgramList.Items.Add(selectedProgram);
                        ListModificationStatusLabel.Text = "Program has been successfully added to the program list!";
                        ListModificationStatusLabel.Foreground = new SolidColorBrush(Colors.Green);
                        ListModificationStatusLabel.Visibility = Visibility.Visible;
                    }


                    col.Update(result);//update database
                }
                catch (LiteException exc)
                {
                    ListModificationStatusLabel.Text = "Something went wrong";
                    ListModificationStatusLabel.Visibility = Visibility.Visible;
                }

                //create a unique index in the Username, Password, and Program List fields
                col.EnsureIndex(x => x.Username, true);
                col.EnsureIndex(x => x.Password, false);
                col.EnsureIndex(x => x.ProgramList, false);
            }
        }

        private void fetchProgramData()
        {
            using (var db = new LiteDatabase(@"C:\Temp\GCData.db"))
            {
                var col = db.GetCollection<SharedData>("users");
                var result = col.FindById(MainWindow.User.Id);
                if(result == null)
                {
                    ProgramList.Items.Add("null");
                }
                else
                {
                    foreach (var item in result.ProgramList)
                    {
                        ProgramList.Items.Add(item);
                    }
                }
                
            }
        }
        private void OnAddButtonClicked(object sender, EventArgs args)
        {
            string targetProgram = "0";
            OpenFileDialog fileDialog = new OpenFileDialog();//allow user to select file
            fileDialog.DereferenceLinks = true;//if they select a shortcut, get the root file location
            bool? resultDlg = fileDialog.ShowDialog();
            if(resultDlg == true)
            {
                FileVersionInfo fileInfo = FileVersionInfo.GetVersionInfo(fileDialog.FileName);
                targetProgram = fileInfo.FileDescription;
            }

            if(targetProgram != "0")//null check basically
            {
                //ProgramList.Items.Add(NewProgramTB.Text);//add to the data table
                using (var db = new LiteDatabase(@"C:\Temp\GCData.db"))
                {
                    //get or create collection
                    var col = db.GetCollection<SharedData>("users");
                    var result = col.FindById(MainWindow.User.Id);

                    //insert new user - id gets incremented automatically
                    try
                    {
                        //var userInfo = col.FindOne(x => SharedData.checkHashes(x.Username, MainWindow.Username).Replace("-", string.Empty));

                        if (result.ProgramList != null && (result.ProgramList.Any()))//make sure the list is not empty
                        {
                            if (result.ProgramList.Count >= 1)//check if the list has 1+ elements
                            {
                                foreach (var program in result.ProgramList)
                                {
                                    if (targetProgram.Equals(program, StringComparison.CurrentCultureIgnoreCase))//if the current program exists within the list
                                    {
                                        ListModificationStatusLabel.Text = "This program is already stored in the program list!";
                                        ListModificationStatusLabel.Visibility = Visibility.Visible;
                                        //break;
                                        return;
                                    }

                                }
                                result.ProgramList.Add(targetProgram);//add the program to the list
                                ProgramList.Items.Add(targetProgram);
                            }
                            else
                            {
                                result.ProgramList.Add(targetProgram);
                                ProgramList.Items.Add(targetProgram);
                                //UserDataEntryProgram.IsLoaded
                                ListModificationStatusLabel.Text = "Program has been successfully added to the program list!";
                                ListModificationStatusLabel.Foreground = new SolidColorBrush(Colors.Green);
                                ListModificationStatusLabel.Visibility = Visibility.Visible;
                            }
                        }
                        else
                        {
                            result.ProgramList.Add(targetProgram);
                            ProgramList.Items.Add(targetProgram);
                            ListModificationStatusLabel.Text = "Program has been successfully added to the program list!";
                            ListModificationStatusLabel.Foreground = new SolidColorBrush(Colors.Green);
                            ListModificationStatusLabel.Visibility = Visibility.Visible;
                        }


                        col.Update(result);//update database
                    }
                    catch (LiteException e)
                    {
                        ListModificationStatusLabel.Text = "Something went wrong";
                        ListModificationStatusLabel.Visibility = Visibility.Visible;
                    }

                    //create a unique index in the Username, Password, and Program List fields
                    col.EnsureIndex(x => x.Username, true);
                    col.EnsureIndex(x => x.Password, false);
                    col.EnsureIndex(x => x.ProgramList, false);
                }
            }
        }

        private void OnRemoveButtonClicked(object sender, EventArgs args)
        {
            using (var db = new LiteDatabase(@"C:\Temp\GCData.db"))
            {
                var col = db.GetCollection<SharedData>("users");

                
                try
                {
                    var user = col.FindById(MainWindow.User.Id);//for some reason, this bypasses some of the restrictions of litedb
                    foreach (var program in user.ProgramList)
                    {
                        if (ProgramList.SelectedItem != null)
                        {
                            if (ProgramList.SelectedItem.Equals(program))
                            {
                                user.ProgramList.Remove(program);//remove from the internal runtime list
                                ProgramList.Items.Remove(ProgramList.SelectedItem);//remove from the UI runtime list
                                col.Update(user);//update the db and remove the item from the list in the database
                                ListModificationStatusLabel.Text = "Program has been successfully removed";
                                ListModificationStatusLabel.Visibility = Visibility.Visible;
                                break;
                            }
                        }
                    }
                }
                catch (LiteException e)
                {
                    ListModificationStatusLabel.Text = "Something went wrong. I was not able to remove the program from the database :(";
                    ListModificationStatusLabel.Visibility = Visibility.Visible;
                }
                

                //create a unique index in the Username, Password, and Program List fields
                col.EnsureIndex(x => x.Username, true);
                col.EnsureIndex(x => x.Password, false);
                col.EnsureIndex(x => x.ProgramList, false);

            }
        }

        #endregion

        #region ACL Settings
        public static bool IsProtected
        {
            get
            {
                try
                {
                    s_isProtectedLock.EnterReadLock();

                    return s_isProtected;
                }
                finally
                {
                    s_isProtectedLock.ExitReadLock();
                }
            }
        }

        public static void Protect()
        {
            try
            {
                s_isProtectedLock.EnterWriteLock();

                if (!s_isProtected)
                {
                    System.Diagnostics.Process.EnterDebugMode();
                    RtlSetProcessIsCritical(1, 0, 0);
                    s_isProtected = true;
                }
            }
            finally
            {
                s_isProtectedLock.ExitWriteLock();
            }
        }

        public static void Unprotect()
        {
            try
            {
                s_isProtectedLock.EnterWriteLock();

                if (s_isProtected)
                {
                    RtlSetProcessIsCritical(0, 0, 0);
                    s_isProtected = false;
                }
            }
            finally
            {
                s_isProtectedLock.ExitWriteLock();
            }
        }

        #endregion

        #region Current Process Thread
        private delegate void updateCPLCallback();//allow for main GUI thread to invoke our external thread
        void constantCPLUpdateThread()//CPL --> Current Process List
        {
            while (CPLThreadRunning)
            {
                ////Process[] processes = Process.GetProcesses();//get the latest running modules
                Dispatcher.Invoke(new updateCPLCallback(this.updateCPL));//invoke back into main GUI thread

                Thread.Sleep(200);//needed to prevent lag
            }
        }
        private void updateCPL()
        {
            //update the process list
            foreach(var proc in Process.GetProcesses())
            {

                //if the process is not already stored in the dictionary, store it now
                //prevents duplicate process from being stored
                if(!_DPinternalProcessesList.ContainsValue(proc) && !_DPinternalProcessesList.ContainsKey(proc.ProcessName) && (!string.IsNullOrEmpty(proc.MainWindowTitle)))
                {
                    _DPinternalProcessesList.Add(proc.ProcessName, proc);
                    proc.EnableRaisingEvents = true;
                    proc.Exited += Proc_Exited;//attach an event handler for every process so when the process ends
                                               //we can remove it from the dictionary
                }
            }
            foreach(var key in _DPinternalProcessesList.Keys)
            {
                if(!_internalProcessList.Contains(key))
                    _internalProcessList.Add(key);//add the process names from the internal<process>list
            }

            //removal of program names from the UI Thread
            if (closedProcessList.Count > 0)//only remove programs if programs have closed
            {
                for (int i = 0; i < _internalProcessList.Count; i++)//loop through the internal process list
                {
                    if (closedProcessList.Contains(_internalProcessList[i]))//check if the list has any process that has been closed and recorded in the closed process list
                    {
                        _internalProcessList.RemoveAt(i);//remove from the list
                        if (closedProcessList.Count == 1)//if the closed process list only has one closed process, we can end execution here because we have removed the only process that was closed
                            break;
                    }
                }
            }
            closedProcessList.Clear();//clear the list now that all the recorded closed processes have been dealt with

        }

        private void Proc_Exited(object sender, EventArgs e)//handle for when a process exits
        {
            Process process = sender as Process;//cast to a process so we can identify the process stored in the internal process list
            for(int i=0; i<_DPinternalProcessesList.Count; i++)
            {
                var targetElement = _DPinternalProcessesList.ElementAt(i);
                if (targetElement.Value.Equals(process))//index the dictionary and check if the dictionary contains the process
                {
                    _DPinternalProcessesList.Remove(targetElement.Key);//remove from the dictionary
                    break;
                }
            }

            //to remove from the UI list we add it to the closedProcessList so the UI list gets editted in the main UI thread itself
            closedProcessList.Add(process.ProcessName);
        }
        #endregion
    }

}
