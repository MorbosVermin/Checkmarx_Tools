using Com.WaitWha.Checkmarx.REST;
using Com.WaitWha.Checkmarx.SOAP;
using Com.WaitWha.Checkmarx.Utils;
using log4net;
using log4net.Appender;
using log4net.Core;
using log4net.Layout;
using log4net.Repository.Hierarchy;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security;
using System.Text;
using System.Threading.Tasks;

namespace Com.WaitWha.Checkmarx.CxCLI
{
    class Program
    {

        static ILog Log = LogManager.GetLogger(typeof(Program));

        enum Commands
        {
            scan,
            list,
            register,
            unregister
        }

        class Configuration : Dictionary<string, dynamic>
        {

            public bool IsDebug
            {
                get
                {
                    return (this.Keys.Contains("v") ? this["v"] : false);
                }
            }

            public string LogFile
            {
                get { return (this.Keys.Contains("log") ? this["log"] : null); }
            }

            public string Command { get; private set; }

            Configuration(string[] args) : base()
            {
                for(int i = 0; i < args.Length; i++)
                {
                    if(args[i].StartsWith("-") || args[i].StartsWith("/"))
                    {
                        string key = args[i].Substring(1);
                        dynamic value = true;
                        if(!args[(i + 1)].StartsWith("-") && !args[(i + 1)].StartsWith("/"))
                        {
                            value = args[(i + 1)];
                            i++;
                        }

                        if (!this.Keys.Contains(key))
                            this.Add(key, value);
                        else
                            this[key] = value;

                    }
                    else
                    {
                        Command = Enum.GetName(typeof(Commands), args[i]);
                    }
                }
            }

            /// <summary>
            /// Returns the value of the given key. However, if the key does not exists, this will
            /// throw an ArgumentException. 
            /// </summary>
            /// <param name="key">Key</param>
            /// <returns>Value</returns>
            /// <exception cref="ArgumentException">Thrown when the given key does not exist.</exception>
            public dynamic GetValueWithCheck(string key)
            {
                if (this.Keys.Contains(key))
                    return this[key];

                throw new ArgumentException(String.Format("The option '-{0}' is required for command {1}.", key, Command));
            }

            public static Configuration GetInstance(string[] args)
            {
                return new Configuration(args);
            }
        }

        static void Help(string error = "")
        {
            if (error.Length > 0)
                Console.Error.WriteLine(error + Environment.NewLine);

            Console.WriteLine("Syntax: {0} [command] -CxServer <uri> -CxUser <username> -CxPass <password> [options]",
                Process.GetCurrentProcess().MainModule.FileName);

            Console.WriteLine("Available Commands: ");
            foreach(Commands c in Enum.GetValues(typeof(Commands)))
            {
                Console.WriteLine("  {0}", c.ToString());
            }

            Console.WriteLine("Required Options: ");
            Console.WriteLine("  -CxServer <uri>       The URI of the Checkmarx server (e.g. https://checkmarx.server)");
            Console.WriteLine("  -CxUser <username>    The username to use when connecting to Cx. Be sure to prepend with the domain if using Windows/Domain authentication.");
            Console.WriteLine("  -CxPass <password>    The password to use when connecting to Cx.");

            Console.WriteLine("Other Options: ");
            Console.WriteLine("  -v                    Debug/Vervbose mode.");
            Console.WriteLine("  -log <file>           Path to the log file to log to.");

            Environment.Exit(-1);
        }

        static void SetupLogging(Level level, string logPath = "")
        {
            Hierarchy hierarchy = (Hierarchy)LogManager.GetRepository();

            if (logPath.Length > 0)
            {
                PatternLayout patternLayout = new PatternLayout();
                patternLayout.ConversionPattern = "%date [%thread] %-5level %logger - %message%newline";
                patternLayout.ActivateOptions();

                RollingFileAppender fileAppender = new RollingFileAppender();
                fileAppender.Layout = patternLayout;
                fileAppender.File = logPath;
                fileAppender.AppendToFile = true;
                fileAppender.RollingStyle = RollingFileAppender.RollingMode.Date;
                fileAppender.PreserveLogFileNameExtension = true;
                fileAppender.MaxSizeRollBackups = 7;
                hierarchy.Root.AddAppender(fileAppender);
            }

            PatternLayout consolePatternLayout = new PatternLayout();
            consolePatternLayout.ConversionPattern = "%-5level %message%newline";
            consolePatternLayout.ActivateOptions();

            ConsoleAppender conAppender = new ConsoleAppender();
            conAppender.Layout = consolePatternLayout;
            conAppender.Target = "Console.Out";
            hierarchy.Root.AddAppender(conAppender);

            hierarchy.Root.Level = level;
            hierarchy.Configured = true;
        }

        static void Main(string[] args)
        {
            if (args.Length == 0)
                Help();

            Configuration config = Configuration.GetInstance(args);
            SetupLogging((config.IsDebug) ? Level.Debug : Level.Info, config.LogFile);

            //Validation
            if(config.Command == null)
            {
                Help();
            }

            string[] requiredKeys = { "CxUser", "CxPass", "CxServer" };
            foreach(string key in requiredKeys)
            {
                if (!config.Keys.Contains(key))
                    Help(String.Format("The option '-{0}' is required.", key));

            }

            string username = config.GetValueWithCheck("CxUser");
            SecureString password = StringUtils.GetSecureString(config.GetValueWithCheck("CxPass"));
            config["CxPass"] = null;
            string cxServer = config.GetValueWithCheck("CxServer");

            if (config.Command.Equals("scan"))
            {

            }
            else if(config.Command.Equals("list"))
            {
                bool projects = config.Keys.Contains("Projects");
                using (CxWebService service = new CxWebService())
                {
                    if (service.Login(username, password))
                    {
                        if (projects)
                        {
                            foreach (var project in service.GetProjectsToDisplay())
                            {
                                Console.WriteLine("[{0}] {1}", project.projectID, project.ProjectName);
                            }
                        }
                        else
                        {
                            foreach(var scan in service.GetProjectScannedDisplayData())
                            {
                                Console.WriteLine("[{0}] {1} was scanned on {6}: {2} high, {3} medium, {4} low, {5} info",
                                    scan.ProjectID,
                                    scan.ProjectName,
                                    scan.HighVulnerabilities,
                                    scan.MediumVulnerabilities,
                                    scan.LowVulnerabilities,
                                    scan.InfoVulnerabilities,
                                    DateTime.FromFileTimeUtc(scan.LastScanDate));
                            }
                        }
                    }
                    else
                        Help("Error: User/Pass was invalid. Please try again.");

                }
            }
            else if(config.Command.Equals("register"))
            {
                string name = config.GetValueWithCheck("Name");
                string uri = config.GetValueWithCheck("Url");
                int minLoc = config.GetValueWithCheck("MinLOC");
                int maxLoc = config.GetValueWithCheck("MaxLOC");
                bool blocked = config.GetValueWithCheck("Blocked");

                CxRestService service = new CxRestService(new Uri(cxServer));
                bool loggedIn = service.Login(username, password).GetAwaiter().GetResult();
                if (!loggedIn)
                    Help("Error: User/Pass was invalid. Please try again.");

                Log.Info(String.Format("Registering scan engine: {0}", config["Name"]));
                int engineId = service.RegisterEngine(name, uri, minLoc, maxLoc, blocked).GetAwaiter().GetResult();
                Log.Info(String.Format("Registration completed: {0}", engineId));

            }
            else if(config.Command.Equals("unregister"))
            {
                int engineId = config.GetValueWithCheck("EngineId");
                bool blockOnly = false;
                if (config.Keys.Contains("BlockOnly"))
                    blockOnly = true;

                CxRestService service = new CxRestService(new Uri(cxServer));
                bool loggedIn = service.Login(username, password).GetAwaiter().GetResult();
                if (!loggedIn)
                    Help("Error: User/Pass was invalid. Please try again.");

                Log.Info(String.Format("Unregistering scan engine: {0}", engineId));
                bool allDone = false;
                if(blockOnly)
                {
                    allDone = service.UpdateEngine(engineId, "", "", -1, -1, blockOnly).GetAwaiter().GetResult();
                }
                else
                {
                    allDone = service.UnregisterEngine(engineId).GetAwaiter().GetResult();
                }

                if (allDone)
                    Log.Info(String.Format("Successfully updated/unregistered scan engine: {0}", engineId));
                else
                    Log.Warn(String.Format("Unable to update/unregister scan engine: {0}", engineId));

            }

        }
    }
}
