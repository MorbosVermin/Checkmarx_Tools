using log4net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;

namespace Com.WaitWha.Checkmarx.SOAP
{
    /// <summary>
    /// Class to interact with a Checkmarx SOAP API service. 
    /// 
    /// Requirements:
    ///     Two (2) Service References:
    ///         1. https://checkmarx.server/CxWebInterface/CxWsResolver.asmx  (CxWSResolver)
    ///         2. https://checkmarx.server/CxWebInterface/SDK/CxSDKWebService.asmx?wsdl  (CxSDKWebService)
    ///     Log4Net
    /// </summary>
    /// <example>
    /// using(CxWebService service = new CxWebService())
    /// {
    ///     if(service.Login(username, password))
    ///     {
    ///         foreach(CxSDKWebService.Preset preset in service.GetPresets())
    ///         {
    ///             Console.WriteLine(preset.PresetName);
    ///         }
    ///     }
    /// }
    /// </example>
    public class CxWebService : IDisposable
    {

        /// <summary>
        /// Current API version is 1
        /// </summary>
        public static readonly int API_VERSION = 1;

        /// <summary>
        /// Max size in bytes for messages to/from SOAP service. (1gb)
        /// </summary>
        public static readonly int MAX_RECEIVED_MESSAGE_SIZE = 1024 * 1024 * 1024;

        static readonly ILog log = LogManager.GetLogger(typeof(CxWebService));
        bool _disposed;
        string _sessionId;

        /// <summary>
        /// Whether or not to use TLS/SSL for the connection to Checkmarx.
        /// </summary>
        public bool UseSSL { get; private set; }

        /// <summary>
        /// EndPoint for Checkmarx
        /// </summary>
        Uri _endpoint;
        EndpointAddress EndpointAddress
        {
            get
            {
                if (_endpoint == null)
                {
                    log.Debug("Discovering endpoint...");
                    CxWSResolver.CxWSResolverSoapClient soapClient = new CxWSResolver.CxWSResolverSoapClient();
                    CxWSResolver.CxClientType clientType = CxWSResolver.CxClientType.SDK;
                    CxWSResolver.CxWSResponseDiscovery disco = soapClient.GetWebServiceUrl(clientType, API_VERSION);
                    _endpoint = new Uri(disco.ServiceURL);
                    log.Debug(String.Format("Caching endpoint: {0}", _endpoint));
                }

                return new EndpointAddress(_endpoint);
            }
        }

        /// <summary>
        /// SOAP Proxy Client
        /// </summary>
        CxSDKWebService.CxSDKWebServiceSoapClient _client;
        CxSDKWebService.CxSDKWebServiceSoapClient SoapClient
        {
            get
            {
                //cache the SOAP client as well.
                if (_client == null)
                {
                    BasicHttpBinding binding = 
                        new BasicHttpBinding((UseSSL) ? BasicHttpSecurityMode.Transport : BasicHttpSecurityMode.None);
                    binding.MaxReceivedMessageSize = MAX_RECEIVED_MESSAGE_SIZE;
                    binding.UseDefaultWebProxy = true;

                    _client = new CxSDKWebService.CxSDKWebServiceSoapClient(binding, EndpointAddress);
                }

                return _client;
            }
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="useSsl">Whether or not to use TLS/SSL for the connection to Checkmarx.</param>
        public CxWebService(bool useSsl)
        {
            _disposed = false;
            UseSSL = useSsl;
        }

        /// <summary>
        /// Default constructor for TLS/SSL connections to Checkmarx. 
        /// </summary>
        public CxWebService() : this(true) { }

        /// <summary>
        /// Destructor
        /// </summary>
        ~CxWebService()
        {
            Disposing(false);
        }

        #region Authentication

        /// <summary>
        /// Uses the given username and password to login to Checkmarx and establish a session. This method must
        /// be called before all others to ensure that the session is properly established. 
        /// </summary>
        /// <param name="username">Checkmarx username. If you are using a domain login, remember to prepend the domain name to the username like so: DOMAIN\username.</param>
        /// <param name="password">Checkmarx password.</param>
        /// <returns>bool</returns>
        /// <example>
        /// using(CxWebService service = new CxWebService())
        /// {
        ///     Console.Write("Logging into Checkmarx as {0}: ", username);
        ///     Console.WriteLine("{0}.", (service.Login(username, password) ? "OK" : "FAILED"));
        /// } //service.Logout() is automatically called.
        /// </example>
        public bool Login(string username, string password)
        {
            CxSDKWebService.Credentials creds = new CxSDKWebService.Credentials();
            creds.User = username;
            creds.Pass = password;

            bool ok = false;
            try
            {
                CxSDKWebService.CxWSResponseLoginData response = CallCheckmarxApi(() => SoapClient.Login(creds, 1033));
                _sessionId = response.SessionId; //cache the session ID
                log.Info(String.Format("Successfully logged into Checkmarx WS at {0}: {1}", _endpoint.DnsSafeHost, _sessionId));
                ok = true;
            }
            catch (ResponseException e)
            {
                log.Error(String.Format("Error, unable to logon to Checkmarx WS at {0}: {1}", _endpoint.DnsSafeHost, e.Message), e);
            }

            return ok;
        }

        /// <summary>
        /// Logs out of Checkmarx (closes the session) and cleans up caches. This method is automatically 
        /// called when within a <i>using</i> (IDisposable) so you should not typically need to call it.
        /// </summary>
        public void Logout()
        {
            log.Debug(String.Format("Closing session {1} on server {0}", _endpoint.DnsSafeHost, _sessionId));
            SoapClient.Logout(_sessionId);
            SoapClient.Close();
            log.Debug(String.Format("Successfully logged out of session: {0}", _sessionId));

            //cleanup caches
            _client = null;
            _sessionId = null;
            _endpoint = null;
        }

        #endregion

        #region Working with CxSAST Projects

        /// <summary>
        /// Returns a list of CxSAST projects available to the current user. This is used primarily for display purposes. However, you
        /// can use this list to get projectIDs which will be used later on.
        /// </summary>
        /// <returns>CxSDKWebService.ProjectDisplayData[]</returns>
        public CxSDKWebService.ProjectDisplayData[] GetProjectsToDisplay()
        {
            if (_sessionId == null)
                throw new AuthenticationException();

            CxSDKWebService.ProjectDisplayData[] projects = new CxSDKWebService.ProjectDisplayData[0];
            try
            {
                CxSDKWebService.CxWSResponseProjectDisplayData response = 
                    CallCheckmarxApi(() => SoapClient.GetProjectDisplayData(_sessionId));
                return response.projectList;
            }
            catch (ResponseException e)
            {
                log.Error(String.Format("Unable to get projects to display: {0} {1}", e.GetType().Name, e.Message), e);
            }
            catch (CommunicationException e)
            {
                log.Error(String.Format("Unable to communicate to SOAP API at endpoint {0}: {1} {2}", _endpoint.DnsSafeHost, e.GetType().Name, e.Message), e);
            }

            return projects;
        }

        /// <summary>
        /// Returns the configuration of the project with the given ID. 
        /// </summary>
        /// <param name="projectId">Project ID (long)</param>
        /// <returns>CxSDKWebService.ProjectConfiguration</returns>
        /// <see cref="GetProjectsToDisplay"/> 
        public CxSDKWebService.ProjectConfiguration GetProjectConfiguration(long projectId)
        {
            if (_sessionId == null)
                throw new AuthenticationException();

            CxSDKWebService.ProjectConfiguration configuration = null;
            try
            {
                CxSDKWebService.CxWSResponseProjectConfig response = 
                    CallCheckmarxApi(() => SoapClient.GetProjectConfiguration(_sessionId, projectId));
                return response.ProjectConfig;
            }
            catch (ResponseException e)
            {
                log.Error(String.Format("Unable to get project configuration for project {2}: {0} {1}", e.GetType().Name, e.Message, projectId), e);
            }
            catch (CommunicationException e)
            {
                log.Error(String.Format("Unable to communicate to SOAP API at endpoint {0}: {1} {2}", _endpoint.DnsSafeHost, e.GetType().Name, e.Message), e);
            }

            return configuration;
        }

        /// <summary>
        /// Updates a project by the given ID with the given configuration and returns whether or not this process 
        /// was successfull.
        /// </summary>
        /// <param name="projectId">Project ID</param>
        /// <param name="configuration">Project Configuration</param>
        /// <returns>bool</returns>
        /// <seealso cref="https://checkmarx.atlassian.net/wiki/spaces/KC/pages/5767327/Project+Configuration+Members"/> 
        /// <see cref="GetProjectConfiguration(long)"/>
        /// <example>
        /// using(CxWebService service = new CxWebService())
        /// {
        ///     if(service.Login(username, password))
        ///     {
        ///         long projectId = 200;
        ///         long presetId = 100035; //A custom preset
        ///         long scanConfigurationId = 4; //Multi-Language Scan
        ///         
        ///         CxSDKWebService.ProjectConfiguration configuration = service.GetProjectConfiguration(projectId);
        ///         configuration.PresetID = presetId;
        ///         configuration.ScanConfigurationID = scanConfigurationId;
        ///         
        ///         Console.Write("Updating project {0}: ", projectId);
        ///         bool updated = service.UpdateProjectIncrementalConfiguration(projectId, configuration))
        ///         Console.WriteLine("{0}.", (updated ? "OK" : "FAILED"));
        ///     }
        /// }
        /// </example>
        public bool UpdateProjectIncrementalConfiguration(long projectId, CxSDKWebService.ProjectConfiguration configuration)
        {
            if (_sessionId == null)
                throw new AuthenticationException();

            bool wasUpdated = false;
            try
            {
                CxSDKWebService.CxWSBasicRepsonse response = 
                    CallCheckmarxApi(() => SoapClient.UpdateProjectIncrementalConfiguration(_sessionId, projectId, configuration));
                wasUpdated = response.IsSuccesfull;
            }
            catch (ResponseException e)
            {
                log.Error(String.Format("Unable to update project {2} configuration: {0} {1}", e.GetType().Name, e.Message, projectId), e);
            }
            catch (CommunicationException e)
            {
                log.Error(String.Format("Unable to communicate to SOAP API at endpoint {0}: {1} {2}", _endpoint.DnsSafeHost, e.GetType().Name, e.Message), e);
            }

            return wasUpdated;
        }

        /// <summary>
        /// Returns a list of all public projects in the system with a risk level and summary of results by severity (high, medium, low).
        /// </summary>
        /// <returns>CxSDKWebService.ProjectScannedDisplayData[]</returns>
        public CxSDKWebService.ProjectScannedDisplayData[] GetProjectScannedDisplayData()
        {
            if (_sessionId == null)
                throw new AuthenticationException();

            CxSDKWebService.ProjectScannedDisplayData[] data = new CxSDKWebService.ProjectScannedDisplayData[0];
            try
            {
                CxSDKWebService.CxWSResponseProjectScannedDisplayData response =
                    CallCheckmarxApi(() => SoapClient.GetProjectScannedDisplayData(_sessionId));
                data = response.ProjectScannedList;
            }
            catch (ResponseException e)
            {
                log.Error(String.Format("Error, unable to get list of public projects: {0}", e.Message), e);
            }
            catch (CommunicationException e)
            {
                log.Error(String.Format("Unable to communicate to SOAP API at endpoint {0}: {1} {2}", _endpoint.DnsSafeHost, e.GetType().Name, e.Message), e);
            }

            return data;
        }

        /// <summary>
        /// Delete(s) an existing project(s) and all related scans. Scans that are currently running are stopped and deleted 
        /// together with the project. If there's even a single scan that cannot be deleted (due to security reasons) the operation 
        /// is marked as failed and an error message is returned.
        /// </summary>
        /// <param name="projectIds"></param>
        /// <returns>bool</returns>
        public bool DeleteProjects(long[] projectIds)
        {
            if (_sessionId == null)
                throw new AuthenticationException();

            CxSDKWebService.ArrayOfLong aLong = new CxSDKWebService.ArrayOfLong();
            foreach (long projectId in projectIds)
                aLong.Add(projectId);

            bool deleted = false;
            try
            {
                CxSDKWebService.BasicRepsonse response = 
                    CallCheckmarxApi(() => SoapClient.DeleteProjects(_sessionId, aLong));
                deleted = response.IsSuccesfull;
            }
            catch (ResponseException e)
            {
                log.Error(String.Format("Error, unable to delete project(s): {0}", e.Message), e);
            }
            catch (CommunicationException e)
            {
                log.Error(String.Format("Unable to communicate to SOAP API at endpoint {0}: {1} {2}", _endpoint.DnsSafeHost, e.GetType().Name, e.Message), e);
            }

            return deleted;
        }

        /// <summary>
        /// Convenience method to delete just one project.
        /// </summary>
        /// <param name="projectId">Project Id to delete</param>
        /// <returns>bool</returns>
        /// <see cref="DeleteProjects(long[])"/> 
        public bool DeleteProject(long projectId)
        {
            return DeleteProjects(new long[] { projectId });
        }

        /// <summary>
        /// Returns an array containing the query presets within Checkmarx.
        /// </summary>
        /// <returns>CxSDKWebService.Preset[]</returns>
        public CxSDKWebService.Preset[] GetPresets()
        {
            if (_sessionId == null)
                throw new AuthenticationException();

            CxSDKWebService.Preset[] presets = new CxSDKWebService.Preset[0];
            try
            {
                CxSDKWebService.CxWSResponsePresetList response = 
                    CallCheckmarxApi(() => SoapClient.GetPresetList(_sessionId));
                return response.PresetList;
            }
            catch (ResponseException e)
            {
                log.Error(String.Format("Error, unable to get list of presets: {0}", e.Message), e);
            }
            catch(CommunicationException e)
            {
                log.Error(String.Format("Unable to communicate to SOAP API at endpoint {0}: {1} {2}", _endpoint.DnsSafeHost, e.GetType().Name, e.Message), e);
            }

            return presets;
        }

        #endregion

        #region Working with Scans

        /// <summary>
        /// Starts a scan. If the projectId property of the ProjectSettings is set, this is will scan for an existing project. 
        /// However, if a project name is given, this will create a new project and scan for this newly created project. Once a 
        /// scan is started, you should use the runId returned to periodically check the status and get the results.
        /// </summary>
        /// <param name="projectSettings">Project Settings</param>
        /// <param name="sourceCodeSettings">Source Code Settings</param>
        /// <param name="isIncremental">Whether or not this is an incremental scan. (default = false)</param>
        /// <param name="isPrivate">Whetther or not this is a private scan, which should not be shared with others in your team. (default = false)</param>
        /// <param name="cronString">An appropriate CRON style string for the schdule time. See https://checkmarx.atlassian.net/wiki/spaces/KC/pages/26542199/Running+Scheduled+Scans for more information (only useed in scheduled scans)</param>
        /// <param name="utcEpochEndTime">Those long type parameters are used to determine a repetitive (scheduled) scan start and end time. The default values are 0 (zero) in order to indicate that it should start now (though initiate a scan by the cron expression schedule) and never end / last forever. (only used in scheduled scans).</param>
        /// <param name="utcEpochStartTime">Those long type parameters are used to determine a repetitive (scheduled) scan start and end time. The default values are 0 (zero) in order to indicate that it should start now (though initiate a scan by the cron expression schedule) and never end / last forever. (only used in scheduled scans).</param>
        /// <returns>string runId; NULL for failures.</returns>
        /// <see cref="GetScanStatus(string)"/> 
        /// <example>
        /// using(CxWebService service = new CxWebService())
        /// {
        ///     if(service.Login(username, password))
        ///     {
        ///         long projectID = 35;
        ///         
        ///         CxSDKWebService.ProjectSettings pSettings = new CxSDKWebService.ProjectSettings();
        ///         pSettings.ProjectID = projectID;
        ///         
        ///         CxSDKWebService.SourceCodeSettings scSettings = new CxSDKWebService.SourceCodeSettings();
        ///         scSettings.SourceOrigin = CxSDKWebService.SourceLocationType.Local;
        ///         scSettings.PackagedCode = new CxSDKWebService.LocalCodeContainer();
        ///         scSettings.PackagedCode.FileName = @"C:\Temp\MyProjectSources.zip";
        ///         scSettings.PackagedCode.ZippedFile = File.ReadAllBytes(scSettings.PackagedCode.FileName);
        ///         
        ///         //Start an incremental scan which will only focus on changes in the sources since the last scan.
        ///         string runId = service.Scan(pSettings, scSettings, true);
        ///         while(true)
        ///         {
        ///             CxSDKWebService.ScanStatus scanStatus = service.GetScanStatus(runId);
        ///             switch(scanStatus)
        ///             {
        ///                 //TODO 
        ///             }
        ///             
        ///             Thread.Sleep(300);
        ///         }
        ///     }
        /// }
        /// </example>
        public string Scan(
            CxSDKWebService.ProjectSettings projectSettings, 
            CxSDKWebService.SourceCodeSettings sourceCodeSettings, 
            bool isIncremental = false, 
            bool isPrivate = false, 
            string cronString = "",
            long utcEpochStartTime = 0L,
            long utcEpochEndTime = 0L)
        {
            if (_sessionId == null)
                throw new AuthenticationException();

            CxSDKWebService.CliScanArgs scanArgs = new CxSDKWebService.CliScanArgs();
            scanArgs.PrjSettings = projectSettings;
            scanArgs.SrcCodeSettings = sourceCodeSettings;
            scanArgs.IsIncremental = isIncremental;
            scanArgs.IsPrivate = isPrivate;

            string runId = null;
            try
            {
                CxSDKWebService.CxWSResponseRunID response = (cronString.Length > 0) ? 
                    CallCheckmarxApi(() => SoapClient.ScanWithSchedulingWithCron(_sessionId, scanArgs, cronString, utcEpochStartTime, utcEpochEndTime)) : 
                    CallCheckmarxApi(() => SoapClient.Scan(_sessionId, scanArgs));
                runId = response.RunId;
            }
            catch (ResponseException e)
            {
                log.Error(String.Format("Error, unable to start scan: {0}", e.Message), e);
            }
            catch (CommunicationException e)
            {
                log.Error(String.Format("Unable to communicate to SOAP API at endpoint {0}: {1} {2}", _endpoint.DnsSafeHost, e.GetType().Name, e.Message), e);
            }

            return runId;
        }

        /// <summary>
        /// Returns the status of the scan running with the ID given. 
        /// </summary>
        /// <param name="runId"></param>
        /// <returns>CxSDKWebService.ScanStatus</returns>
        public CxSDKWebService.ScanStatusEnum GetScanStatus(string runId)
        {
            if (_sessionId == null)
                throw new AuthenticationException();

            try
            {
                CxSDKWebService.CxWSResponseScanStatus response = 
                    CallCheckmarxApi(() => SoapClient.GetSingleScanStatus(_sessionId, runId));
                return response.CurrentStatus;
            }
            catch (ResponseException e)
            {
                log.Error(String.Format("Error, unable to get scan status of run ID {1}: {0}", e.Message, runId), e);
            }
            catch (CommunicationException e)
            {
                log.Error(String.Format("Unable to communicate to SOAP API at endpoint {0}: {1} {2}", _endpoint.DnsSafeHost, e.GetType().Name, e.Message), e);
            }

            return null; //TODO -- Fix me.
        }

        /// <summary>
        /// Add a comment to scan results. If there is already a comment, it will be overwritten.
        /// </summary>
        /// <param name="scanId">Scan ID</param>
        /// <param name="comment">Comment to add/overwrite</param>
        /// <returns>bool</returns>
        public bool UpdateScanComment(long scanId, string comment)
        {
            if (_sessionId == null)
                throw new AuthenticationException();

            bool updated = false;
            try
            {
                CxSDKWebService.CxWSBasicRepsonse response = 
                    CallCheckmarxApi(() => SoapClient.UpdateScanComment(_sessionId, scanId, comment));
                updated = response.IsSuccesfull;
            }
            catch (ResponseException e)
            {
                log.Error(String.Format("Error, unable to update comment for scan {1}: {0}", e.Message, scanId), e);
            }
            catch (CommunicationException e)
            {
                log.Error(String.Format("Unable to communicate to SOAP API at endpoint {0}: {1} {2}", _endpoint.DnsSafeHost, e.GetType().Name, e.Message), e);
            }

            return updated;
        }

        /// <summary>
        /// Cancel a scan in progress. The scan can be canceled while waiting in the queue or during a scan.
        /// </summary>
        /// <param name="runId">Run ID</param>
        /// <returns>bool</returns>
        /// <see cref="Scan(CxSDKWebService.ProjectSettings, CxSDKWebService.SourceCodeSettings, bool, bool, string, long, long)"/> 
        /// <see cref="GetScanStatus(string)"/> 
        public bool CancelScan(string runId)
        {
            if (_sessionId == null)
                throw new AuthenticationException();

            bool canceled = false;
            try
            {
                CxSDKWebService.CxWSBasicRepsonse response = 
                    CallCheckmarxApi(() => SoapClient.CancelScan(_sessionId, runId));
                canceled = response.IsSuccesfull;
            }
            catch (ResponseException e)
            {
                log.Error(String.Format("Error, unable to cancel scan {1}: {0}", e.Message, runId), e);
            }
            catch (CommunicationException e)
            {
                log.Error(String.Format("Unable to communicate to SOAP API at endpoint {0}: {1} {2}", _endpoint.DnsSafeHost, e.GetType().Name, e.Message), e);
            }

            return canceled;
        }

        /// <summary>
        /// Deletes requested scans. Scans that are currently running won't be deleted. If there's even a single scan that the user can't 
        /// delete (due to security reasons) the operation will fail and an error message is returned.
        /// </summary>
        /// <param name="scanIds">Scan IDs to delete</param>
        /// <returns>bool</returns>
        /// <see cref="GetScanStatus(string)"/>
        public bool DeleteScans(long[] scanIds)
        {
            if (_sessionId == null)
                throw new AuthenticationException();

            CxSDKWebService.ArrayOfLong aLong = new CxSDKWebService.ArrayOfLong();
            foreach (long scanId in scanIds)
                aLong.Add(scanId);

            bool deleted = false;
            try
            {
                CxSDKWebService.CxWSBasicRepsonse response = 
                    CallCheckmarxApi(() => SoapClient.DeleteScans(_sessionId, aLong));
                deleted = response.IsSuccesfull;
            }
            catch (ResponseException e)
            {
                log.Error(String.Format("Error, unable to delete scan(s): {0}", e.Message), e);
            }
            catch (CommunicationException e)
            {
                log.Error(String.Format("Unable to communicate to SOAP API at endpoint {0}: {1} {2}", _endpoint.DnsSafeHost, e.GetType().Name, e.Message), e);
            }

            return deleted;
        }

        /// <summary>
        /// Convenience method to delete only one scan.
        /// </summary>
        /// <param name="scanId">Scan ID</param>
        /// <returns>bool</returns>
        public bool DeleteScan(long scanId)
        {
            return DeleteScans(new long[] { scanId });
        }

        #endregion

        #region Working with Scan Result Reports

        /// <summary>
        /// Generate a result report for a scan, by Scan ID. This will return a reportID which should be used in 
        /// getting the status of the process and downloading the report.
        /// </summary>
        /// <param name="reportRequest">CxSDKWebService.CxWSReportRequest</param>
        /// <returns>Report ID used in other calls to check status and download the report.</returns>
        /// <see cref="GetScanReportStatus(long)"/>
        /// <see cref="GetScanReport(long)"/>
        /// <example>
        /// using(CxWebService service = new CxWebService())
        /// {
        ///     if(service.Login(username, password))
        ///     {
        ///         long scanId = 135;
        ///         
        ///         CxSDKWebService.CxWSReportRequest req = new CxSDKWebService.CxWSReportRequest();
        ///         req.Type = CxSDKWebService.CxWSReportType.PDF;
        ///         req.ScanID = scanId;
        ///         
        ///         long reportId = service.CreateScanReport(req);
        ///         while(true)
        ///         {
        ///             CxSDKWebService.CxWSReportStatus status = service.GetReportStatus(reportId);
        ///             //TODO
        ///             
        ///             
        ///         }
        ///         
        ///         byte[] reportContents = service.GetScanReport(reportId);
        ///         File.WriteAllBytes(@"C:\Temp\MyProjectScan.pdf", reportContents);
        ///     }
        /// }
        /// </example>
        public long CreateScanReport(CxSDKWebService.CxWSReportRequest reportRequest)
        {
            if (_sessionId == null)
                throw new AuthenticationException();

            long id = 0L;
            try
            {
                CxSDKWebService.CxWSReportResponse response = CallCheckmarxApi(() => SoapClient.CreateScanReport(_sessionId, reportRequest));
                id = response.ID;
            }
            catch (ResponseException e)
            {
                log.Error(String.Format("Error, unable create scan report: {0}", e.Message), e);
            }
            catch (CommunicationException e)
            {
                log.Error(String.Format("Unable to communicate to SOAP API at endpoint {0}: {1} {2}", _endpoint.DnsSafeHost, e.GetType().Name, e.Message), e);
            }

            return id;
        }

        /// <summary>
        /// Track the status of a report generation request.
        /// </summary>
        /// <param name="reportId">Report ID</param>
        /// <returns>CxSDKWebService.CxWSReportStatus</returns>
        /// <see cref="CreateScanReport(CxSDKWebService.CxWSReportRequest)"/> 
        public CxSDKWebService.CxWSReportStatus GetScanReportStatus(long reportId)
        {
            if (_sessionId == null)
                throw new AuthenticationException();

            try
            {
                CxSDKWebService.CxWSReportStatusResponse response = CallCheckmarxApi(() => SoapClient.GetScanReportStatus(_sessionId, reportId));
                return response.ScanReportStatus;
            }
            catch (ResponseException e)
            {
                log.Error(String.Format("Error, unable to get scan report status for {1}: {0}", e.Message, reportId), e);
            }
            catch (CommunicationException e)
            {
                log.Error(String.Format("Unable to communicate to SOAP API at endpoint {0}: {1} {2}", _endpoint.DnsSafeHost, e.GetType().Name, e.Message), e);
            }

            return null;
        }

        /// <summary>
        /// Once a scan result report has been generated and the report is ready, the API client can retrieve the report's content.
        /// </summary>
        /// <param name="reportId">Report ID</param>
        /// <returns>Scan Results in the format requested.</returns>
        /// <see cref="CreateScanReport(CxSDKWebService.CxWSReportRequest)"/>
        /// <see cref="GetScanReportStatus(long)"/> 
        public byte[] GetScanReport(long reportId)
        {
            if (_sessionId == null)
                throw new AuthenticationException();

            try
            {
                CxSDKWebService.CxWSResponseScanResults response = CallCheckmarxApi(() => SoapClient.GetScanReport(_sessionId, reportId));
                return response.ScanResults;
            }
            catch (ResponseException e)
            {
                log.Error(String.Format("Error, unable to get scan report status for {1}: {0}", e.Message, reportId), e);
            }
            catch (CommunicationException e)
            {
                log.Error(String.Format("Unable to communicate to SOAP API at endpoint {0}: {1} {2}", _endpoint.DnsSafeHost, e.GetType().Name, e.Message), e);
            }

            return null;
        }

        #endregion

        #region Getting Group Information

        /// <summary>
        /// Get information on all groups related to the current user.
        /// </summary>
        /// <returns></returns>
        public CxSDKWebService.CxWSGroupList[] GetAssociatedGroupsList()
        {
            if (_sessionId == null)
                throw new AuthenticationException();

            try
            {
                CxSDKWebService.CxWSResponseGroupList response = CallCheckmarxApi(() => SoapClient.GetAssociatedGroupsList(_sessionId));
                return response.GroupList;
            }
            catch (ResponseException e)
            {
                log.Error(String.Format("Error, unable to get associated groups list (teams): {0}", e.Message), e);
            }
            catch (CommunicationException e)
            {
                log.Error(String.Format("Unable to communicate to SOAP API at endpoint {0}: {1} {2}", _endpoint.DnsSafeHost, e.GetType().Name, e.Message), e);
            }

            return null;
        }

        #endregion

        #region Getting Available Encoding Options
        //TODO
        #endregion

        #region Managing Users

        /// <summary>
        /// Returns a list of all users in the system that are visible to the current user. Server Manager and Company 
        /// Manager role members can see all users within Checkmarx.
        /// </summary>
        /// <returns>CxSDKWebService.UserData[]</returns>
        public CxSDKWebService.UserData[] GetAllUsers()
        {
            if (_sessionId == null)
                throw new AuthenticationException();

            CxSDKWebService.UserData[] data = new CxSDKWebService.UserData[0];
            try
            {
                CxSDKWebService.CxWSResponseUserData responses = 
                    CallCheckmarxApi(() => SoapClient.GetAllUsers(_sessionId));
                data = response.UserDataList;
            }
            catch (ResponseException e)
            {
                log.Error(String.Format("Unable to get projects to display: {0} {1}", e.GetType().Name, e.Message), e);
            }
            catch (CommunicationException e)
            {
                log.Error(String.Format("Unable to communicate to SOAP API at endpoint {0}: {1} {2}", _endpoint.DnsSafeHost, e.GetType().Name, e.Message), e);
            }

            return data;
        }

        /// <summary>
        /// Deletes a given user by the ID given. You should ensure that projects, scans and reports are properly
        /// assigned to others before making this call. Not doing so can result in orphaned/loss of data. Only those
        /// with Server Manager or Company Manager roles assigned may delete users in Checkmarx.
        /// </summary>
        /// <param name="userId">User ID to delete.</param>
        /// <returns>bool</returns>
        /// <see cref="GetAllUsers"/> 
        public bool DeleteUser(int userId)
        {
            if (_sessionId == null)
                throw new AuthenticationException();

            bool deleted = false;
            try
            {
                CxSDKWebService.CxWSBasicRepsonse response = 
                    CallCheckmarxApi(() => SoapClient.DeleteUser(_sessionId, userId));
                deleted = response.IsSuccesfull;
            }
            catch (ResponseException e)
            {
                log.Error(String.Format("Unable to get projects to display: {0} {1}", e.GetType().Name, e.Message), e);
            }
            catch (CommunicationException e)
            {
                log.Error(String.Format("Unable to communicate to SOAP API at endpoint {0}: {1} {2}", _endpoint.DnsSafeHost, e.GetType().Name, e.Message), e);
            }

            return deleted;
        }

        #endregion

        /// <summary>
        /// Encapsulates the calls to the SOAP API and returns the response.
        /// </summary>
        /// <typeparam name="TResult"></typeparam>
        /// <param name="apiCall">SOAP API call to make.</param>
        /// <returns>CxWSBasicRepsonse, the base for all responses from Checkmarx SOAP API.</returns>
        /// <exception cref="ResponseException">Thrown when a call is not successful, but communications are OK.</exception>
        /// <exception cref="CommunicationException">Thrown when communications prevent the call to the service from succeeding.</exception>
        TResult CallCheckmarxApi<TResult>(Func<TResult> apiCall)
            where TResult : CxSDKWebService.CxWSBasicRepsonse
        {
            if (apiCall == null)
            {
                log.Debug(String.Format("No API call given; calling Logout for session: {0}", _sessionId));
                apiCall = SoapClient.Logout(_sessionId);
            }

            try
            {
                TResult t = apiCall();
                if (!t.IsSuccesfull)
                {
                    throw new ResponseException(t.ErrorMessage, _endpoint);
                }

                return t;
            }
            catch (CommunicationException ex)
            {
                throw ex;
            }

        }

        #region IDisposable Implementation

        protected virtual void Disposing(bool disposing)
        {
            if(! _disposed)
            {
                try
                {
                    Logout();
                }
                catch (Exception) { }

                _disposed = true;
            }
        } 

        public void Dispose()
        {
            Disposing(true);
            GC.SuppressFinalize(this);
        }

        #endregion

    }
}
