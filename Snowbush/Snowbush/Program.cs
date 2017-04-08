using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Autofac;
using Serilog;
using Snowbush.CommandLine;

namespace Snowbush
{
    class Program
    {
        static void Main(string[] args)
        {
            using (var container = BuildContainer())
            {
                var arguments = container.Resolve<ProgramCommandLineArguments>();
                var routineManager = container.Resolve<RoutineManager>();
                Directory.SetCurrentDirectory(Path.Combine(Directory.GetCurrentDirectory(), arguments.WorkPath));
                if (arguments.RoutineName != null)
                {
                    var routineType = routineManager.Resolve(arguments.RoutineName);
                    var routine = container.ResolveKeyed<IRoutine>(routineType);
                    routine.PerformAsync().GetAwaiter().GetResult();
                }
                else
                {
                    PrintHelp(routineManager);
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

            builder.Register<ILogger>(context => new LoggerConfiguration().WriteTo.LiterateConsole().CreateLogger())
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

        public string CookiesFileName { get; set; } = "cookies.json";

        public IList<(string Key, string Value)> RoutineArguments { get; set; }

        public void ParseFrom(IEnumerable<string> arguments)
        {
            var parsingRoutineArguments = false;
            foreach (var sarg in arguments)
            {
                var arg = CommandLineParser.ParseArgument(sarg);
                if (parsingRoutineArguments)
                {
                    RoutineArguments.Add(arg);
                    continue;
                }
                switch (arg.Key?.ToUpperInvariant())
                {
                    case "COOKIESFILE":
                        CookiesFileName = arg.Value;
                        break;
                    case "WORKPATH":
                        WorkPath = arg.Value;
                        break;
                    case null:
                        RoutineName = arg.Value;
                        RoutineArguments = new List<(string Key, string Value)>();
                        parsingRoutineArguments = true;
                        break;
                }
            }
        }
    }
}