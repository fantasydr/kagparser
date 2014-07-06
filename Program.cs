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
            if (args.Length < 1)
            {
                Console.WriteLine("1st param is the input ks file.");
                return;
            }

            KagParser parser = new KagParser();

            // parse the input
            try
            {
                using (StreamReader sr = new StreamReader(args[0]))
                {
                    parser.Run(sr);
                }
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
