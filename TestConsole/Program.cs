using System;
using Douyu;

namespace TestConsole
{
    class Program
    {
        

        static void Main(string[] args)
        {
            DouyuLiveChat dy = new();
            dy.Connect(216911);
            dy.onLiveMessageReceived += onMessage;
            Console.ReadKey();
         
        }

        static void onMessage(LiveChatMessage msg)
        {
            Console.WriteLine($"[{TimeStamp.GetDateTime(msg.TimeStamp).ToLongTimeString()}]{msg.FromNickName}:{msg.Content}");
        }

    
    }
}
