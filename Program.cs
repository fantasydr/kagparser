using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace kagparser
{
    interface IKagParser
    {
        Dictionary<string, string> Parse(ref string line);
    }

    class KagLabelParser : IKagParser
    {
        public Dictionary<string, string> Parse(ref string line)
        {
            line = null;
            return new Dictionary<string, string>();
        }
    }

    class KagTagParser : IKagParser
    {
        public Dictionary<string, string> Parse(ref string line)
        {
            line = null;
            return new Dictionary<string, string>();
        }
    }

    class KagTagInlineParser : IKagParser
    {
        public Dictionary<string, string> Parse(ref string line)
        {
            return new Dictionary<string, string>();
        }
    }

    class KagMsgParser : IKagParser
    {
        public Dictionary<string, string> Parse(ref string line)
        {
            int tag = line.IndexOf('[');
            Debug.Assert(tag != 0);

            string msg = line;
            if (tag >= 0)
            {
                msg = line.Substring(0, tag);
                line = line.Substring(tag);
            }

            Dictionary<string, string> cmd = new Dictionary<string, string>();
            cmd["tagname"] = "_msg";
            cmd["text"] = msg;
            return cmd;
        }
    }

    class KagCommentParser : IKagParser
    {
        public Dictionary<string, string> Parse(ref string line)
        {
            // ignore
            line = null;
            return null;
        }
    }

    class KagAnalyzer
    {
        public List<object> cmds = new List<object>();

        public void Run(StreamReader input)
        {
            string line = null;
            while (!input.EndOfStream)
            {
                IKagParser current = null;

                if(line == null) // whether we need new line
                    line = input.ReadLine();

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
                object cmd = current.Parse(ref line);
                if(cmd != null)
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
