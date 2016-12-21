using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using WikiClientLibrary;

namespace Cloudtail
{
    public class Logger
    {
        public static readonly Logger Cloudtail = new Logger("Cloudtail");

        private readonly TraceSource source;

        public Logger(string name)
        {
            source = new TraceSource(name);
        }

        public TraceListenerCollection Listeners => source.Listeners;

        public SourceSwitch Switch => source.Switch;

        /// <summary>
        /// 向日志输出一条诊断信息。
        /// </summary>
        /// <param name="obj">发出诊断信息的源对象。</param>
        /// <param name="format">诊断信息的格式化字符串。</param>
        /// <param name="args">格式化字符串的参数。</param>
        public void Trace(object obj, string format, params object[] args)
        {
            if (source.Switch.ShouldTrace(TraceEventType.Verbose))
                source.TraceEvent(TraceEventType.Verbose, 0, $"{ToString(obj)} : {string.Format(format, args)}");
        }

        /// <summary>
        /// 向日志输出一条警告信息。
        /// </summary>
        /// <param name="obj">发出诊断信息的源对象。</param>
        /// <param name="format">诊断信息的格式化字符串。</param>
        /// <param name="args">格式化字符串的参数。</param>
        public void Warn(object obj, string format, params object[] args)
        {
            if (source.Switch.ShouldTrace(TraceEventType.Warning))
                source.TraceEvent(TraceEventType.Warning, 0, $"{ToString(obj)} : {string.Format(format, args)}");
        }

        /// <summary>
        /// 向日志输出一条信息。
        /// </summary>
        /// <param name="obj">发出诊断信息的源对象。</param>
        /// <param name="format">诊断信息的格式化字符串。</param>
        /// <param name="args">格式化字符串的参数。</param>
        public void Info(object obj, string format, params object[] args)
        {
            if (source.Switch.ShouldTrace(TraceEventType.Information))
                source.TraceEvent(TraceEventType.Information, 0,
                $"{ToString(obj)} : {string.Format(format, args)}");
        }

        /// <summary>
        /// 向日志输出一条错误信息。
        /// </summary>
        /// <param name="obj">发出诊断信息的源对象。</param>
        /// <param name="format">诊断信息的格式化字符串。</param>
        /// <param name="args">格式化字符串的参数。</param>
        public void Error(object obj, string format, params object[] args)
        {
            if (source.Switch.ShouldTrace(TraceEventType.Error))
                source.TraceEvent(TraceEventType.Error, 0,
                $"{ToString(obj)} : {string.Format(format, args)}");
        }

        /// <summary>
        /// 向日志输出一条异常信息。
        /// </summary>
        /// <param name="obj">发出诊断信息的源对象。</param>
        /// <param name="ex">要输出的异常信息。</param>
        public void Exception(object obj, Exception ex, [CallerMemberName] string memberName = null)
        {
            if (source.Switch.ShouldTrace(TraceEventType.Error))
                source.TraceEvent(TraceEventType.Error, 0, $"{ToString(obj)}.{memberName} !> {ex}");
        }

        private string ToString(object obj)
        {
            if (obj == null) return "-";
            var content = obj as string;
            if (content != null) return content;
            if (obj is Page) return obj.ToString();
            return obj.GetType().Name + "#" + obj.GetHashCode();
        }
    }
}
