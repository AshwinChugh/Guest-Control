using System.Collections.Generic;
using LiteDB;

namespace GuestControl
{
    public class SharedData
    {
        [BsonId]
        public int Id { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        //public bool Admin { get; set; }

        public List<string> ProgramList { get; set; } = new List<string>();

        public static bool checkHashes(string originalHash1, string newHash2)
        {
            if (newHash2.Length == originalHash1.Length)
            {
                int i = 0;
                while ((i < newHash2.Length) && (newHash2[i] == originalHash1[i]))
                {
                    i++;
                    if (i == newHash2.Length)
                    {
                        return true;
                    }
                }
            }
            return false;
        }
    }
}
