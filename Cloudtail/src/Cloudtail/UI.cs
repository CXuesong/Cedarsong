using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Cloudtail
{
    static class Prompts
    {
        public const string InvalidInput = "Invalid input.";
        public const string NumberOverflow = "Number overflow.";
        public const string Over = "Over";
        public const string PressAnyKeyToContinue = "Press any key...";
        public const string PleaseWait = "Please wait...";
    }

    /// <summary>
    /// 负责处理基本的用户界面操作。
    /// </summary>
    public static class UI
    {

        private struct Point
        {
            public int X;

            public int Y;
            public Point(int x, int y)
            {
                this.X = x;
                this.Y = y;
            }
        }

        private class LockCursorToken : IDisposable
        {
            public void Dispose()
            {
                UI.CursorLocked = false;
            }
        }


        private static bool m_CursorLocked;
        public static bool CursorLocked
        {
            get { return m_CursorLocked; }
            set { m_CursorLocked = value; }
        }

        private static LockCursorToken _LockCursorToken = new LockCursorToken();
        /// <summary>
        /// 配合 using 语句使用，用于在 using 块中锁定控制台的指针位置。
        /// </summary>
        public static IDisposable LockCursor()
        {
            CursorLocked = true;
            return _LockCursorToken;
        }

        /// <summary>
        /// 接受是/否输入。
        /// </summary>
        /// <param name="prompt">接受输入的提示信息。</param>
        public static bool Confirm(string prompt, bool defaultValue = false)
        {
            return (Input(prompt, defaultValue ? "Y" : "N", "Y", "是", "N", "否") == "Y");
        }

        public static string InputMultiline(string prompt, string EofMark = ".")
        {
            Console.WriteLine(prompt);
            var builder = new StringBuilder();
            while (true)
            {
                var s = Console.ReadLine();
                if (s == EofMark)
                    return builder.ToString();
                builder.AppendLine(s);
            }
        }

        /// <summary>
        /// 接受数据输入。
        /// </summary>
        /// <param name="prompt">接受输入的提示信息。</param>
        public static string Input(string prompt)
        {
            return Input(prompt, null, new string[] { });
        }

        /// <summary>
        /// 接受数据输入。
        /// </summary>
        /// <param name="prompt">接受输入的提示信息。</param>
        /// <param name="defaultValue">如果输入为空，返回的默认值。</param>
        public static string Input(string prompt, string defaultValue)
        {
            return Input(prompt, defaultValue, new string[] { });
        }

        /// <summary>
        /// 接受选项输入。
        /// </summary>
        /// <param name="prompt">接受输入的提示信息。</param>
        /// <param name="defaultValue">如果输入为空，返回的默认值。</param>
        /// <param name="selection">可用选项。格式为：option1, description1, option2, description2, ...</param>
        public static string Input(string prompt, string defaultValue, params string[] selection)
        {
        INPUT:
            Console.Write(prompt);
            if (selection.Length > 0)
            {
                Console.Write('[');
                for (int i = 0; i <= selection.Length - 1; i += 2)
                {
                    Console.Write(selection[i + 1]);
                    Console.Write('(');
                    Console.Write(selection[i].ToUpperInvariant());
                    Console.Write(')');
                    if (i + 2 < selection.Length)
                    {
                        Console.Write('/');
                    }
                }
                Console.Write(']');
            }
            if (defaultValue != null)
            {
                Console.Write('<');
                Console.Write(defaultValue);
                Console.Write('>');
            }
            Console.Write('：');
            Console.CursorVisible = true;
            var inp = Console.ReadLine();
            Console.CursorVisible = false;
            if (string.IsNullOrEmpty(inp))
            {
                return defaultValue;
            }
            else if (selection.Length > 0)
            {
                //检查输入
                for (var i = 0; i <= selection.Length - 1; i += 2)
                {
                    if (string.Compare(inp, selection[i], StringComparison.OrdinalIgnoreCase) == 0)
                    {
                        return selection[i];
                    }
                }
                //输入无效
                Console.WriteLine(Prompts.InvalidInput);
                goto INPUT;
            }
            else
            {
                return inp;
            }
        }

        /// <summary>
        /// 接受指定类型的输入。
        /// </summary>
        /// <param name="prompt">接受输入的提示信息。</param>
        /// <param name="defaultValueHint">默认值的描述字符串。</param>
        public static T? Input<T>(string prompt, string defaultValueHint = null) where T : struct
        {
        INPUT:
            Console.Write(prompt);
            if (defaultValueHint != null)
            {
                Console.Write('<');
                Console.Write(defaultValueHint);
                Console.Write('>');
            }
            Console.Write('：');
            var inp = Console.ReadLine();
            if (string.IsNullOrEmpty(inp))
                return null;
            try
            {
                return (T)Convert.ChangeType(inp, typeof(T));
            }
            catch (FormatException)
            {
                Console.WriteLine(Prompts.InvalidInput);
            }
            catch (OverflowException)
            {
                Console.Write(Prompts.NumberOverflow);
            }
            goto INPUT;
        }

        /// <summary>
        /// 接受指定类型的输入。
        /// </summary>
        /// <param name="prompt">接受输入的提示信息。</param>
        /// <param name="defaultValue">如果输入为空，返回的默认值。</param>
        public static T Input<T>(string prompt, T? defaultValue) where T : struct
        {
            return Input<T>(prompt, defaultValue.ToString()).GetValueOrDefault(defaultValue.GetValueOrDefault());
        }

        /// <summary>
        /// 接受列表输入。
        /// </summary>
        public static void Input<T>(string prompt, IList<T> list) where T : struct
        {
            do
            {
                dynamic NewItem = Input(prompt, Prompts.Over);
                if (NewItem == null)
                {
                    return;
                }
                else
                {
                    list.Add(NewItem.Value);
                }
            } while (true);
        }

        /// <summary>
        /// 输出空行。
        /// </summary>
        public static void Print()
        {
            if (m_CursorLocked)
            {
                PushCursor();
                var builder = new StringBuilder();
                builder.Append(' ', Console.WindowWidth);
                Console.Write(builder.ToString());
                PopCursor();
            }
            else
            {
                Console.WriteLine();
            }
        }

        /// <summary>
        /// 输出字符串。
        /// </summary>
        public static void Print(object v)
        {
            if (m_CursorLocked) PushCursor();
            Console.WriteLine(v);
            if (m_CursorLocked) PopCursor();
        }

        /// <summary>
        /// 输出文本直到窗口的右边缘。
        /// </summary>
        public static void PrintToMargin(string v)
        {
            var width = Console.BufferWidth - 2;
            foreach (var c in v)
            {
                if (c == '\r' || c == '\n') break;
                if (Console.CursorLeft >= width) break;
                Console.Write(c);
            }
            Console.WriteLine();
        }

        /// <summary>
        /// 输出字符串。
        /// </summary>
        public static void Write(string v)
        {
            if (m_CursorLocked) PushCursor();
            Console.Write(v);
            if (m_CursorLocked) PopCursor();
        }

        /// <summary>
        /// 输出字符串。
        /// </summary>
        public static void Write(string format, params object[] args)
        {
            if (m_CursorLocked) PushCursor();
            Console.Write(format, args);
            if (m_CursorLocked) PopCursor();
        }

        /// <summary>
        /// 输出字符串。
        /// </summary>
        public static void Print(string v)
        {
            if (m_CursorLocked) PushCursor();
            Console.WriteLine(v);
            if (m_CursorLocked) PopCursor();
        }

        /// <summary>
        /// 输出字符串。
        /// </summary>
        public static void Print(string format, params object[] args)
        {
            if (m_CursorLocked) PushCursor();
            Console.WriteLine(format, args);
            if (m_CursorLocked) PopCursor();
        }

        /// <summary>
        /// 输出列表。
        /// </summary>
        public static void Print(IEnumerable v, string seperator = ",")
        {
            bool isFirst = true;
            foreach (var eachItem in v)
            {
                if (isFirst)
                    isFirst = false;
                else
                    Console.Write(seperator);
                Console.Write(eachItem);
            }
            Console.WriteLine();
        }

        /// <summary>
        /// 输出错误信息字符串。
        /// </summary>
        public static void PrintError(object v)
        {
            Console.Error.WriteLine(v);
        }

        /// <summary>
        /// 输出错误信息字符串。
        /// </summary>
        public static void PrintError(Exception v)
        {
            PrintError(v, null);
        }

        /// <summary>
        /// 输出错误信息字符串。
        /// </summary>
        public static void PrintError(Exception v, string indent)
        {
            Console.Error.WriteLine("{0}{1}:{2}", indent, v.GetType(), v.Message);
            if (!(v is AggregateException)) return;
            foreach (var EachException in ((AggregateException)v).InnerExceptions)
            {
                PrintError(EachException, indent + ">");
            }
        }

        /// <summary>
        /// 输出错误信息字符串。
        /// </summary>
        public static void PrintError(string format, params object[] args)
        {
            Console.Error.WriteLine(format, args);
        }

        private static Stack<Point> PositionStack = new Stack<Point>();
        public static void PushCursor()
        {
            PositionStack.Push(new Point(Console.CursorLeft, Console.CursorTop));
        }
        public static void PeekCursor()
        {
            var _with1 = PositionStack.Peek();
            Console.SetCursorPosition(_with1.X, _with1.Y);
        }
        public static void PopCursor()
        {
            PeekCursor();
            PositionStack.Pop();
        }

        /// <summary>
        /// 输入文件路径。
        /// </summary>
        public static string InputFile(string defaultValue = null, string prompt = null)
        {
            do
            {
                var pathStr = UI.Input(prompt ?? "输入文件", defaultValue).Replace("\"", "");
                Uri path;
                if (Uri.TryCreate(pathStr, UriKind.RelativeOrAbsolute, out path))
                {
                    if (path.IsAbsoluteUri) return path.LocalPath;
                    path = new Uri(new Uri(Directory.GetCurrentDirectory() + "\\"), path.ToString());
                    UI.Print(path.LocalPath);
                    return path.LocalPath;
                }
                else
                {
                    PrintError("无效的路径。");
                }
            } while (true);
        }

        /// <summary>
        /// 输入文件路径，用于打开。
        /// </summary>
        public static string FileOpen(string defaultValue = null, string prompt = null)
        {
            do
            {
                string Path = InputFile(defaultValue, "打开文件");
                if (File.Exists(Path))
                {
                    return Path;
                }
                else
                {
                    PrintError("找不到文件：{0}。", Path);
                }
            } while (true);
        }

        /// <summary>
        /// 输入文件路径，用于保存。
        /// </summary>
        public static string FileSave(string defaultValue = null, string prompt = null)
        {
            return InputFile(defaultValue, "保存文件");
        }

        public static void Pause()
        {
            Console.Write(Prompts.PressAnyKeyToContinue);
            Console.ReadKey();
        }

        public static void PromptWait(Action action)
        {
            using (LockCursor())
            {
                Print(Prompts.PleaseWait);
                action();
                Print();
            }
        }

        public static T PromptWait<T>(Func<T> action)
        {
            using (LockCursor())
            {
                Print(Prompts.PleaseWait);
                var result = action();
                Print();
                return result;
            }
        }

        public static string InputPassword(string prompt = null)
        {
            Console.Write((prompt ?? "键入密码") + "：");
            var pass = "";
            using (LockCursor())
            {
                while (true)
                {
                    UI.Print("[已键入{0}字符]", pass.Length);
                    var key = Console.ReadKey(true);
                    switch (key.Key)
                    {
                        case ConsoleKey.Enter:
                            UI.Print();
                            UI.Print("[已键入]");
                            UI.CursorLocked = false;
                            UI.Print();
                            return pass;
                        case ConsoleKey.Backspace:
                            if (pass.Length > 0) pass = pass.Substring(0, pass.Length - 1);
                            break;
                        case ConsoleKey.Escape:
                            pass = "";
                            break;
                        default:
                            if (key.KeyChar != '\0') pass += key.KeyChar;
                            break;
                    }
                }
            }
        }
    }
}
