using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace KagParser
{
    class Program
    {
        static void Main(string[] args)
        {
            using (StreamReader sr = new StreamReader(args[0]))
            {
                KagParser parser = new KagParser();

                // parse the input
                try
                {
                    parser.Run(sr);
                }
                catch (System.Exception ex)
                {
                    Console.WriteLine("[Error]");
                    Console.WriteLine(ex.Message);
                }

                parser.OutputKRKR();
            }
        }
    }
}
