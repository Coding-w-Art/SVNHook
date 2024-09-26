using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace SVNHook
{
    class Program
    {
        static string[] forbiddenExtension = { ".dds" };

        static void Main(string[] args)
        {
            var argMap = ProcessArguments(args);
            Console.Error.WriteLine();

            if (!argMap.TryGetValue("REPOS", out string repo))
            {
                Console.WriteLine("illegal argument, REPOS not found");
                return;
            }

            if (!argMap.TryGetValue("TXN", out string txn))
            {
                Console.WriteLine("illegal argument, TXN not found");
                return;
            }

            CheckLog(repo, txn, out bool super);
            if (!super)
            {
                CheckChange(repo, txn);
            }
        }

        static void CheckLog(string repo, string txn, out bool super)
        {
            int totalLength = 6;

            var logResult = ExecCommand("svnlook", null, $"log {repo} -t {txn}").Trim();

            if (logResult.Length < totalLength)
            {
                LogError($"描述长度不足，请填写至少 {totalLength} 个字，当前长度 {logResult.Length}");
            }

            var changeResult = ExecCommand("svnlook", null, $"changed {repo} -t {txn}").Trim();
            if (changeResult.IndexOf("Documents/", StringComparison.Ordinal) < 0)
            {
                MatchCollection commonMatchCollection = Regex.Matches(logResult, @"\[\d{7}\].{6,}|\[BUG:\d{7}\]");
                if (commonMatchCollection.Count == 0)
                {
                    LogError($"日志:\n{logResult}\n\n日志格式不正确！正确为：\n需求：[需求ID(7位数字)]修改内容描述 (6个字符以上)\n或\n缺陷：修改内容描述(6个字符以上)，并在在提交窗口的右上角填写缺陷ID");
                }

                Match m = Regex.Match(logResult, @"\d{7}");
                if (m.Success && m.Value == "0000000")
                {
                    super = true;
                    return;
                }
            }
            super = false;

            //string commonPattern = @"^(\[\d{7}\].{6,}|\[BUG:\d{7}\])";
            //string tapdPattern = @"^(\u9700\u6c42ID|\u4efb\u52a1ID):\d{7}$";
            //bool isTag = false;

            //var changeResult = ExecCommand("svnlook", null, $"changed {repo} -t {txn}").Trim();
            //if (changeResult.IndexOf("tags/") >= 0 || changeResult.IndexOf("branches/") >= 0)
            //{
            //    commonPattern = @"^\[.{2,}\]\[.{7}\].{6,}";
            //    tapdPattern = @"^\[.{2}.*\]\[.{7}.*\].{6}.*";
            //    isTag = true;
            //}
            //else
            //{
            //    commonPattern = @"^\[.{2,}\].{6,}";
            //    tapdPattern = @"^.*:\d{7}";
            //}

            //MatchCollection commonMatchCollection = Regex.Matches(logResult, commonPattern);
            //if (commonMatchCollection.Count == 0)
            //{
            //    LogError($"日志:\n{logResult}\n\n日志格式不正确！正确为：\n需求：[需求ID(7位数字)]修改内容描述 (6个字符以上)\n缺陷：修改内容描述(6个字符以上)，并在在提交窗口的右上角填写缺陷ID");

            //if (isTag)
            //{
            //    LogError("日志格式不正确！正确为：[模块(2字符以上)]修改内容描述(6字符以上)");canting
            //}
            //else
            //{
            //    LogError("日志格式不正确！正确为：[模块(2字符以上)]修改内容描述(6字符以上)");
            //}
            //}

            //MatchCollection tapdMatchCollection = Regex.Matches(logResult, tapdPattern);
            //if (tapdMatchCollection.Count == 0)
            //{
            //    LogError("日志格式不正确！请填写正确的需求ID，或在提交窗口的右上角填写缺陷ID）");
            //}
        }

        static void CheckChange(string repo, string txn)
        {
            var changeResult = ExecCommand("svnlook", null, $"changed {repo} -t {txn}").Trim();

            foreach (var item in forbiddenExtension)
            {
                if (changeResult.IndexOf(item) > 0)
                {
                    LogError($"{item} 文件类型不允许上传！");
                    return;
                }
            }

            if (changeResult.IndexOf("Client/Assets/", StringComparison.Ordinal) >= 0)
            {
                string[] lines = changeResult.Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);

                List<string> sourceList = new List<string>();
                List<string> metaList = new List<string>();

                foreach (var line in lines)
                {
                    string newLine = line.Trim();
                    newLine = newLine.EndsWith("/") ? newLine.TrimEnd('/') : newLine;
                    Console.Error.WriteLine("------------------------------------------------");
                    Console.Error.WriteLine(newLine);

                    Match match = Regex.Match(newLine, @"(?![A-Z]+[ ]+)[^ \n]+");
                    if (match.Success && match.Value.StartsWith("Art/"))
                    {
                        continue;
                    }

                    if (newLine.IndexOf("Client/Assets/", StringComparison.Ordinal) < 0)
                        continue;

                    MatchCollection matchCollection1 = Regex.Matches(changeResult, @"[U]");
                    if (matchCollection1.Count > 0)
                    {
                        continue;
                    }

                    MatchCollection matchCollection2 = Regex.Matches(changeResult, @"[ADR]");

                    if (matchCollection2.Count > 0)
                    {
                        if (newLine.EndsWith(".meta"))
                        {
                            metaList.Add(newLine);
                        }
                        else
                        {
                            sourceList.Add(newLine);
                        }
                    }
                }

                int srcLength = sourceList.Count;
                for (int i = 0; i < srcLength; i++)
                {
                    string metaFileName = sourceList[i] + ".meta";
                    if (!metaList.Contains(metaFileName))
                    {
                        LogError("meta文件需与源文件一起添加/删除: " + sourceList[i]);
                        return;
                    }
                }

                int metaLength = metaList.Count;
                for (int i = 0; i < metaLength; i++)
                {
                    string sourceFileName = System.IO.Path.ChangeExtension(metaList[i], null);
                    if (!sourceList.Contains(sourceFileName))
                    {
                        LogError("源文件需与meta文件与一起添加/删除: " + metaList[i]);
                        return;
                    }
                }
            }
        }

        static void LogError(string content)
        {
            Console.Error.WriteLine("************************************************");
            Console.Error.WriteLine(content);
            Console.Error.WriteLine("************************************************");
            Environment.Exit(1);
        }

        private static Dictionary<string, string> ProcessArguments(string[] args)
        {
            Dictionary<string, string> argDic = new Dictionary<string, string>();
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i].StartsWith("-"))
                {
                    string key = args[i].Substring(1);
                    string value = string.Empty;
                    if (i + 1 < args.Length && args[i + 1].StartsWith("-") == false)
                    {
                        value = args[i + 1];
                        i++;
                    }
                    if (argDic.ContainsKey(key))
                    {
                        break;
                    }
                    argDic.Add(key, value);
                }
            }

            return argDic;
        }

        private static string ExecCommand(string app, string workingDirectory = null, string args = null)
        {
            using (System.Diagnostics.Process p = new System.Diagnostics.Process())
            {
                p.StartInfo.FileName = app;
                p.StartInfo.UseShellExecute = false;
                p.StartInfo.RedirectStandardInput = true;
                p.StartInfo.RedirectStandardOutput = true;
                p.StartInfo.RedirectStandardError = true;
                p.StartInfo.CreateNoWindow = true;
                p.StartInfo.WorkingDirectory = workingDirectory;
                p.StartInfo.Arguments = args;
                p.Start();

                return p.StandardOutput.ReadToEnd();
            }
        }
    }
}
