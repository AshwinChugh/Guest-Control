using System;
using System.Windows;
using System.Windows.Media;
using LiteDB;
using Effortless.Net.Encryption;


namespace GuestControl
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private bool _validPass;
        private bool _PurposeClose;

        public static SharedData User;
        public MainWindow()
        {
            InitializeComponent();

            _PurposeClose = false;
            Password_Field.PasswordChanged += checkSpacesUF;
            LogInButton.Click += onLogInButtonClick;
            RegisterButton.Click += onRegisterButtonClicked;
            //Closing += MainWindow_Closing;
            //Closed += MainWindow_Closed;//if closing fails, last ditch attempt to end the external thread
            
        }

        //private void MainWindow_Closing(object sender, EventArgs e)
        //{
        //    if(!_PurposeClose)
        //        InternalProcessControl.ThreadControl = false;//close thread when button called
        //}
        //private void MainWindow_Closed(object sender, EventArgs e)
        //{
        //    if (!_PurposeClose)
        //    {
        //        if(InternalProcessControl._tProcessControl != null)
        //        {
        //            if (InternalProcessControl._tProcessControl.IsAlive)
        //                InternalProcessControl._tProcessControl.Abort();//abort thread if thread fails to terminate
        //        }
        //    }
        //}

        private void checkSpacesUF(object sender, EventArgs args)
        {
            if (Password_Field.Password.ToLower().Contains(" "))
            {
                _validPass = false;
                Bad_Pass_Label.Text = "Password can not have spaces";
                Bad_Pass_Label.Visibility = Visibility.Visible;
            }
            else
            {
                _validPass = true;
                Bad_Pass_Label.Visibility = Visibility.Hidden;
            }
        }

        private void onLogInButtonClick(object sender, EventArgs args)
        { 
            using(var db = new LiteDatabase(@"C:\Temp\GCData.db"))
            {
                var col = db.GetCollection<SharedData>("users");
                //col.Delete(Query.All("users")); //--> clears everything(debug purposes)
                try
                {
                    if(_validPass)
                    {
                        var result = col.FindOne(x => SharedData.checkHashes(x.Username, BitConverter.ToString(Hash.Create(HashType.SHA512, Username_Field.Text, string.Empty)).Replace("-", string.Empty)));
                        if (result != null)
                        {
                            if (SharedData.checkHashes(result.Password, BitConverter.ToString(Hash.Create(HashType.SHA512, Password_Field.Password, string.Empty)).Replace("-", string.Empty)))
                            {
                                Bad_Pass_Label.Text = "Account found and validated! Logging in...";
                                User = result;
                                Bad_Pass_Label.FontSize = 15;
                                Bad_Pass_Label.Foreground = new SolidColorBrush(Colors.Green);
                                Bad_Pass_Label.Visibility = Visibility.Visible;
                                //Thread.Sleep(750);

                                //InternalProcessControl IPC = new InternalProcessControl();
                                //IPC.startProcessControl();

                                ControlPanel controlPanel = new ControlPanel();
                                controlPanel.Width = 1200;
                                controlPanel.Height = 800;
                                controlPanel.Show();
                                _PurposeClose = true;
                                Close();


                            }
                            else
                            {
                                Bad_Pass_Label.Text = "Incorrect Password";
                                Bad_Pass_Label.FontSize = 15;
                                Bad_Pass_Label.Visibility = Visibility.Visible;
                            }
                        }
                        else
                        {
                            Bad_Pass_Label.Text = "Account not found";
                            Bad_Pass_Label.FontSize = 15;
                            Bad_Pass_Label.Visibility = Visibility.Visible;
                        }
                    }
                }

                catch(ArgumentNullException e)
                {
                    Bad_Pass_Label.Text = e.Message;
                    //Bad_Pass_Label.Text = "Account not found. Make sure you entered a correct username and password";
                    Bad_Pass_Label.FontSize = 15;
                    Bad_Pass_Label.Visibility = Visibility.Visible;
                    _validPass = false;
                    
                }
                
                
            }
        }

        private void onRegisterButtonClicked(object sender, EventArgs args)
        {
            AccountCreationPage ACP = new AccountCreationPage();
            ACP.Show();
            _PurposeClose = true;//prevent intentional threads from accidentally closing
            Close();
        }
    }
}
