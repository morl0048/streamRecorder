using System;

namespace ConsoleApp1
{
    class Program
    {
        static void Main(string[] args)
        {
            bool stop = false;

            string streamLink = "Enter your rtsp stream here";

            StreamRecorder sr = new StreamRecorder(streamLink, true, true);

            while (!stop)
            {
                if (Console.ReadKey(true).Key == ConsoleKey.Escape)
                    stop = true;
            }
        }
    }
}
