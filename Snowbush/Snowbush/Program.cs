using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Autofac;
using Serilog;
using Serilog.Events;
using Snowbush.CommandLine;

namespace Snowbush
{
    static class Program
    {
        private static TextWriter logWriter;

        static void Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            using (var container = BuildContainer())
            {
                var arguments = container.Resolve<ProgramCommandLineArguments>();
                var routineManager = container.Resolve<RoutineManager>();
                Directory.SetCurrentDirectory(Path.Combine(Directory.GetCurrentDirectory(), arguments.WorkPath));
                using (logWriter = File.CreateText("Snowbush-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".log"))
                {
                    if (arguments.RoutineName != null)
                    {
                        var routineType = routineManager.Resolve(arguments.RoutineName);
                        var routine = container.ResolveKeyed<IRoutine>(routineType);
                        routine.PerformAsync(arguments.RoutineArguments).GetAwaiter().GetResult();
                    }
                    else
                    {
                        PrintHelp(routineManager);
                    }
                }
            }
        }

        static IContainer BuildContainer()
        {
            var builder = new ContainerBuilder();

            builder.Register(context =>
                {
                    var arguments = new ProgramCommandLineArguments();
                    arguments.ParseFrom(Environment.GetCommandLineArgs().Skip(1));
                    return arguments;
                })
                .SingleInstance();

            builder.Register<ILogger>(context => new LoggerConfiguration().MinimumLevel.Information()
                    .WriteTo.LiterateConsole(LogEventLevel.Debug,
                        "{Timestamp:HH:mm:ss} [{Level}] {SourceContext} {Message}{NewLine}{Exception}")
                    .WriteTo.TextWriter(new SyncTextWriter(logWriter), LogEventLevel.Information,
                        "{Timestamp:HH:mm:ss} [{Level}] {SourceContext} {Message}{NewLine}{Exception}")
                    .CreateLogger())
                .SingleInstance();

            builder.Register(context => new SiteProvider(context.Resolve<ProgramCommandLineArguments>().CookiesFileName,
                    context.Resolve<ILogger>()))
                .SingleInstance();

            var routineManager = new RoutineManager();
            foreach (var type in typeof(Program).GetTypeInfo()
                .Assembly.ExportedTypes
                .Where(t => typeof(IRoutine).IsAssignableFrom(t) && !t.GetTypeInfo().IsAbstract))
            {
                routineManager.Register(type);
                builder.RegisterType(type).Keyed<IRoutine>(type);
            }
            builder.RegisterInstance(routineManager);
            return builder.Build();
        }

        static void PrintHelp(RoutineManager rm)
        {
            var assembly = typeof(Program).GetTypeInfo().Assembly;
            Console.WriteLine("{0} {1}",
                assembly.GetCustomAttribute<AssemblyTitleAttribute>().Title,
                assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion);
            Console.WriteLine();
            Console.WriteLine("Usage:");
            Console.WriteLine("dotnet run RoutineName RoutineParam1 ... RoutineParam_n");
            Console.WriteLine();
            PrintAvailableRoutines(rm);
        }

        static void PrintAvailableRoutines(RoutineManager rm)
        {
            Console.WriteLine("Available routines:");
            Console.WriteLine("Routine name  (Type name)");
            Console.WriteLine("------------------------------");
            foreach (var t in rm.GetRegisteredRoutineTypes()
                .Select(t => (Name:rm.GetRoutineName(t), Type:t))
                .OrderBy(t => t.Name))
            {
                Console.ForegroundColor = ConsoleColor.White;
                Console.Write(t.Name);
                Console.ResetColor();
                Console.WriteLine("  ({0})", t.Type.FullName);
            }
        }
    }

    public class ProgramCommandLineArguments
    {
        public string RoutineName { get; set; }

        public string WorkPath { get; set; } = "work";

        public string CookiesFileName { get; set; } = "cookies.dat";

        public CommandArguments RoutineArguments { get; set; }

        public void ParseFrom(IEnumerable<string> arguments)
        {
            var parsingRoutineArguments = false;
            var routineArgs = new List<CommandArgument>();
            foreach (var sarg in arguments)
            {
                var arg = CommandLineParser.ParseArgument(sarg);
                if (parsingRoutineArguments)
                {
                    routineArgs.Add(arg);
                    continue;
                }
                switch (arg.Name?.ToUpperInvariant())
                {
                    case "COOKIESFILE":
                        CookiesFileName = arg.Value;
                        break;
                    case "WORKPATH":
                        WorkPath = arg.Value;
                        break;
                    case null:
                        RoutineName = arg.Value;
                        parsingRoutineArguments = true;
                        break;
                }
            }
            RoutineArguments = new CommandArguments(routineArgs);
        }
    }
}