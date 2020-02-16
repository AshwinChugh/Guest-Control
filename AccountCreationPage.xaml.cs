using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using LiteDB;
using Effortless.Net.Encryption;

namespace GuestControl
{
    /// <summary>
    /// Interaction logic for AccountCreationPage.xaml
    /// </summary>
    public partial class AccountCreationPage : Window
    {
        private bool RegisterCheckBool;
        public AccountCreationPage()
        {
            InitializeComponent();

            SettingsAlert.Visibility = Visibility.Hidden;

            PWField.PasswordChanged += checkPassword;
            RegisterButton.Click += onRegisterButtonClicked;
            BackButton.Click += onBackButtonClicked;
        }

        private void checkPassword(object sender, EventArgs args)
        {
            if (PWField.Password.ToLower().Contains(" "))
            {
                SettingsAlert.Text = "Invalid Password";
                SettingsAlert.Visibility = Visibility.Visible;
                RegisterCheckBool = false;
            }
            else
            {
                SettingsAlert.Visibility = Visibility.Hidden;
                RegisterCheckBool = true;
            }
        }

        private void onRegisterButtonClicked(object sender, EventArgs args)
        {
            if (RegisterCheckBool)
            {
                using (var db = new LiteDatabase(@"C:\Temp\GCData.db"))
                {
                    //get or create collection
                    var col = db.GetCollection<SharedData>("users");

                    var randomNum = new Random().Next();

                    var user = new SharedData//hash and store the log in credentials
                    {
                        Username = BitConverter.ToString(Hash.Create(HashType.SHA512, UNField.Text, string.Empty)).Replace("-", string.Empty),
                        Password = BitConverter.ToString(Hash.Create(HashType.SHA512, PWField.Password, string.Empty)).Replace("-", string.Empty)
                    };
                    
                    //insert new user - id gets incremented automatically
                    try
                    {
                        col.Insert(user);
                    }
                    catch (LiteException e)
                    {
                        SettingsAlert.Text = "The username is already taken. Please enter a new one.";
                        SettingsAlert.Foreground = new SolidColorBrush(Colors.Red);
                        SettingsAlert.FontSize = 15;
                        SettingsAlert.Visibility = Visibility.Visible;
                        return;
                    }

                    //create a unique index in the Username and Password fields
                    col.EnsureIndex(x => x.Username, true);
                    col.EnsureIndex(x => x.Password, false);
                    col.EnsureIndex(x => x.ProgramList, false);
                    SettingsAlert.Text = "Successfully Registered!";
                    SettingsAlert.FontSize = 15;
                    SettingsAlert.Foreground = new SolidColorBrush(Colors.Green);
                    SettingsAlert.Visibility = Visibility.Visible;
                }
            }
        }

        private void onBackButtonClicked(object sender, EventArgs args)
        {
            //code here
            MainWindow MW = new MainWindow();
            MW.Show();
            this.Close();
        }

    }
}
