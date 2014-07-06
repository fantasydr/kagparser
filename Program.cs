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
            string label = line;
            string pagename = "";
            int pipe = line.IndexOf("|");
            string cansave = pipe < 0 ? "false":"true";

            if (pipe == 0)
                throw new Exception(string.Format("Syntax error in: {0}", line));

            if(pipe > 0)
            {
                label = line.Substring(0, pipe);
                pipe += 1;
                pagename = pipe >= line.Length ? "" : line.Substring(pipe);
            }

            Dictionary<string, string> cmd = new Dictionary<string, string>();
            cmd["tagname"] = "_label";
            cmd["pagename"] = pagename;
            cmd["cansave"] = cansave;
            cmd["label"] = label;

            line = null;
            return cmd;
        }
    }

    class KagTagParser : IKagParser
    {
        static char[] _symbols = new char[] { '=', ' ' };

        void MoveCursor(ref string line, int pos)
        {
            if (pos < line.Length)
                line = line.Substring(pos);
            else if (pos == line.Length)
                line = "";
            else
                throw new Exception(string.Format("KagTagParser Syntax error, wrong cursor offset {1} in: {0}", line, pos));
        }

        string ReadToken(ref string line)
        {
            // already trimmed, do not move cursor
            int skippos = 0;
            while (skippos < line.Length)
            {
                if (line[skippos] == '=' || 
                    line[skippos] == ' ' ||
                    line[skippos] == ']' ||
                    line[skippos] == '"' ||
                    line[skippos] == '[')
                    break;
                skippos++;
            }

            return skippos == 0 ? line.Substring(0, 1) : line.Substring(0, skippos);
        }

        string ReadQuote(ref string line)
        {
            // already trimmed, move cursor
            int skippos = 1;
            while (skippos < line.Length)
            {
                if (line[skippos] == '"')
                    break;
                skippos++;
            }

            string content = line.Substring(1, skippos - 1);
            MoveCursor(ref line, skippos + 1);

            return content;
        }

        bool TrimLine(ref string line)
        {
            line = line.TrimStart();
            return string.IsNullOrEmpty(line);
        }

        public Dictionary<string, string> Parse(ref string line)
        {
            Dictionary<string, string> cmd = new Dictionary<string, string>();

            string tagname = null;
            string valuename = null;

            bool parsing = true;
            while (parsing)
            {
                if(TrimLine(ref line))
                    break;

                string token = ReadToken(ref line);
                switch (token)
                {
                    case "\"":
                        // read quoted expression
                        if (string.IsNullOrEmpty(valuename))
                            throw new Exception(string.Format("KagTagParser Syntax error, isolated quote in: {0}", line));

                        string content = ReadQuote(ref line);
                        if (string.IsNullOrEmpty(content))
                            throw new Exception(string.Format("KagTagParser Syntax error, opened quote in: {0}", line));

                        cmd[valuename] = content;
                        valuename = null;
                        break;

                    case "=":
                        // read expression, move to next
                        if (string.IsNullOrEmpty(valuename))
                            throw new Exception(string.Format("KagTagParser Syntax error, no param name in: {0}", line));

                        MoveCursor(ref line, 1);
                        break;

                    case "]":
                        parsing = false; // KagTagInlineParser need this
                        break;

                    default:// symbol
                        {
                            if (tagname == null)
                                tagname = token;
                            else if (!string.IsNullOrEmpty(valuename))
                            {
                                cmd[valuename] = token;
                                valuename = null;
                            }
                            else
                                valuename = token;

                            MoveCursor(ref line, token.Length);
                        }
                        break;
                }
            }

            if(string.IsNullOrEmpty(tagname))
                throw new Exception(string.Format("KagTagParser Syntax error, no tagname in: {0}", line));

            if (tagname.StartsWith("@"))
                tagname = tagname.Substring(1);
            cmd["tagname"] = tagname;

            if (line == "")
                line = null;

            return cmd;
        }
    }

    class KagTagInlineParser : IKagParser
    {
        public Dictionary<string, string> Parse(ref string line)
        {
            line = line.Substring(1); // ignore the 1st char

            IKagParser parser = new KagTagParser();
            Dictionary<string, string> cmd = parser.Parse(ref line);

            if (line[0] != ']') // ignore the last character
                throw new Exception(string.Format("KagTagInlineParser Syntax error in: {0}", line));

            line = line.Substring(1);
            return cmd;
        }
    }

    class KagMsgParser : IKagParser
    {
        public Dictionary<string, string> Parse(ref string line)
        {
            int tag = line.IndexOf('[');
            if (tag == 0)
                throw new Exception(string.Format("KagMsgParser Syntax error in: {0}", line));

            string msg = line;
            if (tag >= 0)
            {
                msg = line.Substring(0, tag);
                line = line.Substring(tag);
            }
            else
            {
                line = null;
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

                if (line.Length == 0) // skip empty line
                {
                    line = null;
                    continue;
                }

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
