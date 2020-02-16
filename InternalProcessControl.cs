using System;
using System.Diagnostics;
using System.Threading;
using LiteDB;

namespace GuestControl
{
    public class InternalProcessControl
    {
        public static volatile bool ThreadControl;
        public static Thread _tProcessControl;
        private SharedData mainUser;

        public enum ThreadStatus
        {
            start,
            terminate
        }

        public void startProcessControl()//basically the constructor
        {
            //get all the user info
            _tProcessControl = new Thread(() => constantProcessControl());
            _tProcessControl.Start();
            using(var db = new LiteDatabase(@"C:\Temp\GCData.db"))
            {
                var col = db.GetCollection<SharedData>("users");
                mainUser = col.FindById(MainWindow.User.Id);
            }
        }

        private void constantProcessControl()
        {
            while (ThreadControl)
            {
                foreach (var process in Process.GetProcesses())//loop through every process
                {
                    if(!string.IsNullOrEmpty(process.MainWindowTitle))//filter out core windows processes
                    {
                        try
                        {
                            if (mainUser.ProgramList.Contains(process.ProcessName))
                                process.Kill();
                        }
                        catch (Exception e)
                        {
                            //ignore applications that we don't have access to
                        }
                    }
                }
            }
        }

        public void setThreadStatus(ThreadStatus threadStatus)
        {
            if (threadStatus == ThreadStatus.start)
                ThreadControl = true;//start the external thread
            else if (threadStatus == ThreadStatus.terminate)
                ThreadControl = false;
        }

        //InternalProcessControl IPC = new InternalProcessControl();
        //IPC.startProcessControl();
    }
}
