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
        Dictionary<string, string> Parse(ref string line, StreamReader input);
    }

    class KagLabelParser : IKagParser
    {
        public Dictionary<string, string> Parse(ref string line, StreamReader input)
        {
            string label = line;
            string pagename = "";
            int pipe = line.IndexOf("|");
            string cansave = pipe < 0 ? "false":"true";

            if (pipe == 0)
                throw new Exception(string.Format("KagLabelParser syntax error in: {0}", line));

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
        // states
        string _tagname;
        string _valuename;
        bool _equal;
        bool _address;
        
        private void Reset()
        {
            _valuename = null;
            _equal = false;
            _address = false;
        }

        // symbols for kag tag
        private static char[] _symbols = new char[] { '=', ']', '[', '(', ')', '"', '&', ';', ' ' };

        private void MoveCursor(ref string line, int pos)
        {
            if (pos < line.Length)
                line = line.Substring(pos);
            else if (pos == line.Length)
                line = "";
            else
                throw new Exception(string.Format("KagTagParser Syntax error, wrong cursor offset {1} in: {0}", line, pos));
        }

        private bool IsSymbol(char symbol, char[] symbols)
        {
            foreach (char s in symbols)
            {
                if (s == symbol)
                    return true;
            }
            return false;
        }

        private string ReadToken(string line)
        {
            // if it is empty, do not move cursor, trim later
            if (line[0] == ' ')
                return "";

            // already trimmed, do not move cursor
            int skippos = 0;
            while (skippos < line.Length)
            {
                if (IsSymbol(line[skippos], _symbols))
                    break;

                skippos++;
            }

            return skippos == 0 ? line.Substring(0, 1) : line.Substring(0, skippos);
        }

        private string ReadQuote(ref string line, char lhs)
        {
            // get the pair quote
            char rhs = '\"';
            if (lhs == '(')
                rhs = ')';

            // already start with a quote
            Debug.Assert(line[0] == lhs);

            // already trimmed, move cursor
            int skippos = 1;
            while (skippos < line.Length)
            {
                if (line[skippos] == '\\')
                    skippos++;
                else if (line[skippos] == rhs)
                {
                    string content = line.Substring(1, skippos - 1);
                    MoveCursor(ref line, skippos + 1);
                    return content;
                }

                skippos++;
            }

            return null;
        }

        private void PushValue(Dictionary<string, string> cmd, string content)
        {
            cmd[_valuename] = _address ? string.Format("&\"{0}\"", content) : content;
            Reset();
        }

        private bool IsEnding(string line)
        {
            string nexttoken = ReadToken(line);
            return (nexttoken == "" || nexttoken == "]" || nexttoken == ";");
        }

        private Exception ThrowError(string error, string line)
        {
            return new Exception(string.Format("KagTagParser syntax error, {1} in: {0}", line, error));
        }

        public Dictionary<string, string> Parse(ref string line, StreamReader input)
        {
            Dictionary<string, string> cmd = new Dictionary<string, string>();
            
            _tagname = null;
            Reset();

            bool parsing = true;
            while (parsing)
            {
                // break if we're done here
                if(string.IsNullOrEmpty(line))
                    break;

                string token = ReadToken(line);
                switch (token)
                {
                    case "]":
                        Debug.Assert(!_equal && !_address); // already handled
                        parsing = false; // KagTagInlineParser need this
                        break;

                    case ";":
                        if (_tagname[0] != '@' || _address || _equal)
                            throw ThrowError("isolated comment symbol", line);

                        parsing = false;
                        break;

                    case "": // continues spaces
                        line = ParseWhitespace(line, cmd);
                        break;

                    case "&":
                        line = ParseAddressing(line, cmd, token);
                        break;

                    case "\"":
                    case "(":
                        line = ParseQuote(line, cmd, token);
                        break;

                    case "=":
                        line = ParseEqual(line);
                        break;

                    default:
                        line = ParseSymbol(line, cmd, token);
                        break;
                }
            }

            if(string.IsNullOrEmpty(_tagname))
                throw ThrowError("no tagname", line);

            if (_tagname.StartsWith("@"))
                _tagname = _tagname.Substring(1);

            cmd["tagname"] = _tagname;

            if (line == "")
                line = null;

            return cmd;
        }

        private string ParseSymbol(string line, Dictionary<string, string> cmd, string token)
        {
            if (_address)
                throw ThrowError("addressing with symbol", line);
            else if (_tagname == null)
                _tagname = token;
            else if (!string.IsNullOrEmpty(_valuename))
                PushValue(cmd, token);
            else
                _valuename = token;

            MoveCursor(ref line, token.Length);
            return line;
        }

        private string ParseEqual(string line)
        {
            // read expression, move to next
            if (string.IsNullOrEmpty(_valuename))
                throw ThrowError("no param name before '='", line);

            MoveCursor(ref line, 1);

            if (IsEnding(line))
                throw ThrowError("expecting value after '='", line);

            _equal = true;
            return line;
        }

        private string ParseQuote(string line, Dictionary<string, string> cmd, string token)
        {
            // read quoted expression
            if (!_equal)
                throw ThrowError("isolated quote", line);

            if (_address && token == "(")
                throw ThrowError("addressing with '('", line);

            Debug.Assert(!string.IsNullOrEmpty(_valuename));

            string content = ReadQuote(ref line, token[0]);
            if (content == null)
                throw ThrowError("opened quote", line);

            PushValue(cmd, content);
            return line;
        }

        private string ParseWhitespace(string line, Dictionary<string, string> cmd)
        {
            if (string.IsNullOrEmpty(_tagname))
                throw ThrowError("can't find instruction", line);

            if (!string.IsNullOrEmpty(_valuename))
            {
                Debug.Assert(!_address); // already handled
                PushValue(cmd, "1");
            }

            // trim white space
            return line.TrimStart();
        }

        private string ParseAddressing(string line, Dictionary<string, string> cmd, string token)
        {
            if (!_equal)
                throw ThrowError("isolated addressing", line);

            Debug.Assert(!string.IsNullOrEmpty(_valuename)); // already handled

            _address = true;
            MoveCursor(ref line, 1);

            // syntax sugar for tag=&
            if (IsEnding(line))
            {
                line = line.TrimStart();
                cmd[_valuename] = token;
                Reset();
            }

            return line;
        }
    }

    class KagTagInlineParser : IKagParser
    {
        public Dictionary<string, string> Parse(ref string line, StreamReader input)
        {
            line = line.Substring(1).TrimStart(); // ignore the 1st char and the spaces

            IKagParser parser = new KagTagParser();
            Dictionary<string, string> cmd = parser.Parse(ref line, input);

            if (line[0] != ']') // ignore the last character
                throw new Exception(string.Format("KagTagInlineParser syntax error in: {0}", line));

            line = line.Substring(1);
            return cmd;
        }
    }

    class KagMsgParser : IKagParser
    {
        public Dictionary<string, string> Parse(ref string line, StreamReader input)
        {
            int tag = line.IndexOf('[');
            if (tag == 0)
                throw new Exception(string.Format("KagMsgParser syntax error in: {0}", line));

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
        public Dictionary<string, string> Parse(ref string line, StreamReader input)
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
                object cmd = current.Parse(ref line, input);
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
