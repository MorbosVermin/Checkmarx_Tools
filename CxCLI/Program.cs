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
using System.IO;
using System.Linq;
using System.Security;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

namespace Com.WaitWha.Checkmarx.CxCLI
{
    class Program
    {

        static ILog Log = LogManager.GetLogger(typeof(Program));

        enum Commands
        {
            scan,
            list,
            report,
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
                get { return (this.Keys.Contains("log") ? this["log"] : ""); }
            }

            public bool UseSSL
            {
                get { return (this.Keys.Contains("UseSSL") ? this["UseSSL"] : false); }
            }

            public Commands Command { get; private set; }

            Configuration(string[] args) : base()
            {
                
                for(int i = 0; i < args.Length; i++)
                {
                    if(args[i].StartsWith("-") || args[i].StartsWith("/"))
                    {
                        string key = args[i].Substring(1);
                        dynamic value = true;
                        if((i + 1) < args.Length && !args[(i + 1)].StartsWith("-") && !args[(i + 1)].StartsWith("/"))
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
                        if (args[i].Equals("scan", StringComparison.CurrentCultureIgnoreCase))
                            Command = Commands.scan;

                        else if (args[i].Equals("list", StringComparison.CurrentCultureIgnoreCase))
                            Command = Commands.list;

                        else if (args[i].Equals("register", StringComparison.CurrentCultureIgnoreCase))
                            Command = Commands.register;

                        else if (args[i].Equals("unregister", StringComparison.CurrentCultureIgnoreCase))
                            Command = Commands.unregister;

                        else if (args[i].Equals("report", StringComparison.CurrentCultureIgnoreCase))
                            Command = Commands.report;

                        else
                            Help();

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

            string fileName = Path.GetFileName(Process.GetCurrentProcess().MainModule.FileName);
            Console.WriteLine("Syntax: {0} [command] -CxServer <uri> -CxUser <username> -CxPass <password> [options]", fileName);

            Console.WriteLine("Commands: ");
            foreach(Commands c in Enum.GetValues(typeof(Commands)))
            {
                Console.WriteLine("  {0}", c.ToString());
            }

            Console.WriteLine();
            Console.WriteLine("Required Options: ");
            Console.WriteLine("  -CxServer <uri>       The URI of the Checkmarx server (e.g. https://checkmarx.server).");
            Console.WriteLine("  -CxUser <username>    The username to use when connecting to Cx. Be sure to prepend the domain if using Windows/Domain authentication.");
            Console.WriteLine("  -CxPass <password>    The password to use when connecting to Cx.");
            Console.WriteLine();
            Console.WriteLine("Other Options: ");
            Console.WriteLine("  -v                    Debug/Verbose mode.");
            Console.WriteLine("  -log <file>           Path to the log file to log to.");
            Console.WriteLine("  -UseSSL               TLS/SSL support for the connection to Checkmarx.");
            Console.WriteLine();
            Console.WriteLine("Listing things...");
            Console.WriteLine("list <-Projects|-Scans|-Configurations|-Presets|-Users>");
            Console.WriteLine();
            Console.WriteLine("Reporting...");
            Console.WriteLine("report -ScanId <int> [-Format <pdf|csv|rtf|xml>] [-Out <filename>]");
            Console.WriteLine();
            Console.WriteLine("Scanning...");
            Console.WriteLine("scan <-LocationPath <path>|-Zip <path>> <-ProjectId <int> | [-Team <string>] -ProjectName <string> -PresetId <int> -ConfigurationId <int>> [[-IsIncremental] [-IsPrivate] [-CronString <string>] [-UtcEpocStartTime <long>] [-UtcEpocEndTime <long>]]");
            Console.WriteLine();
            Console.WriteLine("Registering/Unregistering Scan Engines...");
            Console.WriteLine("register -Name <string> -Url <http://checkmarx.server> [-MinLOC <int> -MaxLOC <int> -IsBlocked]");
            Console.WriteLine("unregister -EngineId <int> [-BlockOnly]");
            Console.WriteLine();
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

            if (config.Command == Commands.scan)
            {
                string zipFile = (config.Keys.Contains("Zip") ? config.GetValueWithCheck("Zip") : "");
                string locationPath = (zipFile.Length == 0) ? config.GetValueWithCheck("LocationPath") : "";
                bool isIncremental = (config.Keys.Contains("IsIncremental") ? config.GetValueWithCheck("IsIncremental") : false);
                bool isPrivate = (config.Keys.Contains("IsPrivate") ? config.GetValueWithCheck("IsPrivate") : false);
                string cronString = (config.Keys.Contains("CronString") ? config.GetValueWithCheck("CronString") : "");
                int projectId = (config.Keys.Contains("ProjectId") ? Int32.Parse(config.GetValueWithCheck("ProjectId")) : -1);              
                string projectName = (projectId == -1) ? config.GetValueWithCheck("ProjectName") : "";
                long presetId = (projectName.Length > 0) ? Int32.Parse(config.GetValueWithCheck("PresetId")) : -1;
                string teamName = (projectName.Length > 0) ? config.GetValueWithCheck("Team") : "CxServer";
                if (!teamName.StartsWith("CxServer"))
                    Help(@"Error: Team name must start with 'CxServer' (e.g. CxServer\SP\Humana SE\Pharmacy).");

                long configurationId = (projectName.Length > 0) ? long.Parse(config.GetValueWithCheck("ConfigurationId")) : 0;
                long utcEpochStartTime = (config.Keys.Contains("UtcEpochStartTime") ? long.Parse(config.GetValueWithCheck("UtcEpochStartTime")) : 0);
                long utcEpochEndTime = (config.Keys.Contains("UtcEpochEndTime") ? long.Parse(config.GetValueWithCheck("UtcEpochEndTime")) : 0);

                using(CxWebService service = new CxWebService(config.UseSSL))
                {
                    if(service.Login(username, password))
                    {
                        CxSDKWebService.ProjectSettings projectSettings = new CxSDKWebService.ProjectSettings();
                        if (projectId > -1)
                        {
                            CxSDKWebService.ProjectConfiguration configuration = service.GetProjectConfiguration(projectId);
                            projectSettings = configuration.ProjectSettings;
                        }
                        else
                        {
                            projectSettings.ProjectName = teamName + "\\"+ projectName;
                            projectSettings.PresetID = presetId;
                            projectSettings.Owner = username;
                            projectSettings.ScanConfigurationID = configurationId;
                        }
                        
                        CxSDKWebService.SourceCodeSettings sourceCodeSettings = new CxSDKWebService.SourceCodeSettings();
                        sourceCodeSettings.SourceOrigin = CxSDKWebService.SourceLocationType.Local;
                        if(zipFile.Length > 0)
                        {
                            sourceCodeSettings.PackagedCode = new CxSDKWebService.LocalCodeContainer();
                            sourceCodeSettings.PackagedCode.FileName = Path.GetFileName(zipFile);
                            sourceCodeSettings.PackagedCode.ZippedFile = File.ReadAllBytes(zipFile);
                        }
                        else
                        {
                            CxSDKWebService.ScanPath path = new CxSDKWebService.ScanPath();
                            path.IncludeSubTree = true;
                            path.Path = locationPath;

                            sourceCodeSettings.PathList = new CxSDKWebService.ScanPath[] { path };
                        }

                        //TODO - Support Source Control Settings

                        Console.Write("Scanning {0}, please wait...\r", (zipFile.Length > 0) ? zipFile : locationPath);
                        string runId = service.Scan(projectSettings, sourceCodeSettings, isIncremental, isPrivate, cronString, utcEpochStartTime, utcEpochEndTime);
                        
                        bool running = true;
                        while (running)
                        {
                            CxSDKWebService.CxWSResponseScanStatus status = service.GetScanStatus(runId);
                            switch(status.CurrentStatus)
                            {
                                case CxSDKWebService.CurrentStatusEnum.Canceled:
                                case CxSDKWebService.CurrentStatusEnum.Deleted:
                                    Console.WriteLine("Scanning {0}, please wait...cancelled or deleted! {1}",
                                        (zipFile.Length > 0) ? zipFile : locationPath,
                                        status.ErrorMessage);
                                    running = false;
                                    break;

                                case CxSDKWebService.CurrentStatusEnum.Failed:
                                    Console.WriteLine("Scanning {0}, please wait...failed: {1}",
                                        (zipFile.Length > 0) ? zipFile : locationPath,
                                        status.ErrorMessage);
                                    running = false;
                                    break;

                                case CxSDKWebService.CurrentStatusEnum.Finished:
                                    Console.WriteLine("Scanning {0}, please wait...done: {1}", 
                                        (zipFile.Length > 0) ? zipFile : locationPath, 
                                        CxUtils.FromCxDateTime(status.TimeFinished));
                                    var summary = service.GetScanSummary(status.ScanId);
                                    Console.WriteLine("{0}loc, {1} high, {2} medium, {3} low, {4} info vulnerabilities.",
                                        summary.LOC,
                                        summary.High,
                                        summary.Medium,
                                        summary.Low,
                                        summary.Info);
                                    running = false;
                                    break;

                                case CxSDKWebService.CurrentStatusEnum.Unknown:
                                case CxSDKWebService.CurrentStatusEnum.Queued:
                                case CxSDKWebService.CurrentStatusEnum.Unzipping:
                                case CxSDKWebService.CurrentStatusEnum.WaitingToProcess:
                                case CxSDKWebService.CurrentStatusEnum.Working:
                                    Console.Write("Scanning {4}, please wait..."+ Environment.NewLine +" [{0}] {2} ({1}% current / {3}% total)\r", 
                                        status.CurrentStage, 
                                        status.CurrentStagePercent, 
                                        status.CurrentStatus, 
                                        status.TotalPercent,
                                        (zipFile.Length > 0) ? zipFile : locationPath);
                                    break;

                            }

                            if(running)
                                Thread.Sleep(100);

                        }
                    }
                }
            }
            else if(config.Command == Commands.list)
            {
                bool projects = config.Keys.Contains("Projects");
                bool scans = config.Keys.Contains("Scans");
                bool presets = config.Keys.Contains("Presets");
                bool configurations = config.Keys.Contains("Configurations");
                bool users = config.Keys.Contains("Users");

                using (CxWebService service = new CxWebService(config.UseSSL))
                {
                    if (service.Login(username, password))
                    {
                        if (projects)
                        {
                            foreach (var project in service.GetProjectsToDisplay())
                            {
                                Console.WriteLine("[{0}] {1} was last scanned on {2} ({3} total scans)",
                                    project.projectID,
                                    project.ProjectName,
                                    CxUtils.FromCxDateTime(project.LastScanDate),
                                    project.TotalScans);
                            }
                        }
                        else if(scans)
                        {
                            foreach(var scan in service.GetProjectScannedDisplayData())
                            {
                                Console.WriteLine("[{0}] {1} scanned at {6}: {2} high, {3} medium, {4} low, {5} info",
                                    scan.LastScanID,
                                    scan.ProjectName,
                                    scan.HighVulnerabilities,
                                    scan.MediumVulnerabilities,
                                    scan.LowVulnerabilities,
                                    scan.InfoVulnerabilities,
                                    DateTime.FromFileTimeUtc(scan.LastScanDate));
                                
                            }
                        }
                        else if(presets)
                        {
                            foreach(var preset in service.GetPresets())
                            {
                                Console.WriteLine("[{0}] {1}", preset.ID, preset.PresetName);
                            }
                        }
                        else if(configurations)
                        {
                            foreach(var c in service.GetConfigurationSetList())
                            {
                                Console.WriteLine("[{0}] {1}", c.ID, c.ConfigSetName);
                            }
                        }
                        else if(users)
                        {
                            foreach(var user in service.GetAllUsers())
                            {
                                Console.WriteLine("[{0}] {4} {1} {2} {3}", user.ID, String.Format("{0}, {1}", user.LastName, user.FirstName), user.Email, user.LastLoginDate, user.UserName);
                            }
                        }
                        else
                        {
                            Help("Error: -Users, -Presets, -Projects, -Configurations, or -Scans option is required.");
                        }
                    }
                    else
                        Help("Error: User/Pass was invalid. Please try again.");

                }
            }
            else if(config.Command == Commands.report)
            {
                CxSDKWebService.CxWSReportRequest reportRequest = new CxSDKWebService.CxWSReportRequest();
                reportRequest.ScanID = long.Parse(config.GetValueWithCheck("ScanId"));
                reportRequest.Type = CxSDKWebService.CxWSReportType.PDF;

                if(config.Keys.Contains("Format"))
                {
                    string format = config.GetValueWithCheck("Format");
                    if (format.Equals("pdf", StringComparison.CurrentCultureIgnoreCase))
                        reportRequest.Type = CxSDKWebService.CxWSReportType.PDF;
                    else if (format.Equals("csv", StringComparison.CurrentCultureIgnoreCase))
                        reportRequest.Type = CxSDKWebService.CxWSReportType.CSV;
                    else if (format.Equals("rtf", StringComparison.CurrentCultureIgnoreCase))
                        reportRequest.Type = CxSDKWebService.CxWSReportType.RTF;
                    else if (format.Equals("xml", StringComparison.CurrentCultureIgnoreCase))
                        reportRequest.Type = CxSDKWebService.CxWSReportType.XML;

                }

                using (CxWebService service = new CxWebService(config.UseSSL))
                {
                    if (service.Login(username, password))
                    {
                        Console.Write(String.Format("Generating report for scan ID {0} in {1} format, please wait...",
                            reportRequest.ScanID,
                            reportRequest.Type.ToString().ToUpper()));
                        long reportId = service.CreateScanReport(reportRequest);
                        while(true)
                        {
                            CxSDKWebService.CxWSReportStatusResponse statusResponse = service.GetScanReportStatus(reportId);
                            if(statusResponse.IsFailed)
                            {
                                Console.WriteLine("failed: {0}", statusResponse.ErrorMessage);
                                break;
                            }
                            else if(statusResponse.IsReady)
                            {
                                Console.WriteLine("done.");
                                Console.Write("Downloading report, please wait...");
                                string fileName = config.Keys.Contains("Out") ? 
                                    config.GetValueWithCheck("Out") : 
                                    Guid.NewGuid().ToString() + "." + reportRequest.Type.ToString().ToLower();
                                byte[] data = service.GetScanReport(reportId);
                                File.WriteAllBytes(fileName, data);
                                Console.WriteLine("done: {0}", fileName);
                                break;
                            }
                        }
                    }
                }
            }
            else if(config.Command == Commands.register)
            {
                string name = config.GetValueWithCheck("Name");
                string uri = config.GetValueWithCheck("Url");
                int minLoc = (config.Keys.Contains("MinLOC") ? config.GetValueWithCheck("MinLOC") : -1);
                int maxLoc = (config.Keys.Contains("MaxLOC") ? config.GetValueWithCheck("MaxLOC") : -1);
                bool blocked = (config.Keys.Contains("IsBlocked") ? config.GetValueWithCheck("IsBlocked") : false);

                CxRestService service = new CxRestService(new Uri(cxServer));
                bool loggedIn = service.Login(username, password).GetAwaiter().GetResult();
                if (!loggedIn)
                    Help("Error: User/Pass was invalid. Please try again.");

                Console.Write("Registering scan engine '{0}' ({1}), please wait...", name, uri);
                int engineId = service.RegisterEngine(name, uri, minLoc, maxLoc, blocked).GetAwaiter().GetResult();
                if (engineId != -1)
                    Console.WriteLine("complete: {0}", engineId);
                else
                    Console.WriteLine("failed.");


            }
            else if(config.Command == Commands.unregister)
            {
                int engineId = config.GetValueWithCheck("EngineId");
                bool blockOnly = config.Keys.Contains("BlockOnly");

                CxRestService service = new CxRestService(new Uri(cxServer));
                bool loggedIn = service.Login(username, password).GetAwaiter().GetResult();
                if (!loggedIn)
                    Help("Error: User/Pass was invalid. Please try again.");

                Console.Write("Unregistering/Updating scan engine {0}, please wait...", engineId);
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
                    Console.WriteLine("done.");
                else
                    Console.Error.WriteLine("failed.");

            }

        }
    }
}
