using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace kagparser
{
    interface IKagParser
    {
        public Dictionary<string, string> Parse(string line);
    }

    class KagLabelParser : IKagParser
    {
        public Dictionary<string, string> Parse(string line)
        {
            throw new NotImplementedException();
        }
    }

    class KagTagParser : IKagParser
    {
        public Dictionary<string, string> Parse(string line)
        {
            throw new NotImplementedException();
        }
    }

    class KagTagInlineParser : IKagParser
    {
        public Dictionary<string, string> Parse(string line)
        {
            throw new NotImplementedException();
        }
    }

    class KagMsgParser : IKagParser
    {
        public Dictionary<string, string> Parse(string line)
        {
            throw new NotImplementedException();
        }
    }

    class KagCommentParser : IKagParser
    {
        public Dictionary<string, string> Parse(string line)
        {
            throw new NotImplementedException();
        }
    }

    class KagAnalyzer
    {
        public List<object> cmds;

        public void Run(StreamReader input)
        {
            while (!input.EndOfStream)
            {
                IKagParser current = null;
                string line = input.ReadLine();
                if(line.Length == 0) // skip empty line
                    continue;

                int peek = line[0];
                switch(peek)
                {
                    // single line
                    case '*':
                        current = new KagLabelParser();
                        break;
                    case '@':
                        current = new KagTagParser();
                        break;
                    case ';':
                        current = new KagCommentParser();
                        break;
                    case '[':
                        current = new KagTagInlineParser();
                        break;
                    default:
                        current = new KagMsgParser();
                        break;
                }
                object cmd = current.Parse(line);
                cmds.Add(cmd);
            }
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            using (StreamReader sr = new StreamReader(@"D:\prelogue.ks"))
            {
                KagAnalyzer analyzer = new KagAnalyzer();

                // parse the input
                try
                {
                    analyzer.Run(sr);
                }
                catch (System.Exception ex)
                {
                    Console.WriteLine("[Error]");
                    Console.WriteLine(ex.Message);
                }

                // print the result as krkr format
                Console.WriteLine("(const) [");
                foreach (object cmd in analyzer.cmds)
                {
                    Console.WriteLine("(const) %[");
                    foreach (KeyValuePair<string, string> kv in (Dictionary<string, string>)cmd)
                    {
                        Console.WriteLine(string.Format("\"{0}\" => \"{1}\",", kv.Key, kv.Value));
                    }
                    Console.WriteLine("],");
                }
                Console.WriteLine("]");
            }
        }
    }
}
