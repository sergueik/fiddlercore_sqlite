using Fiddler;

using Microsoft.Win32;

using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using SQLite.Utils;

using NUnit.Framework;
using OpenQA.Selenium;
using OpenQA.Selenium.Interactions;

using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Firefox;
using OpenQA.Selenium.IE;
using OpenQA.Selenium.PhantomJS;
using OpenQA.Selenium.Remote;
using OpenQA.Selenium.Support.UI;

using System.Diagnostics;

namespace WebTester
{
	public class Monitor
	{

		public static Monitor proxy;
		private string tableName = "";
		private string database;
		private string dataSource;
		private static string databaseName = "fiddler-data.db";
		
		public static string DatabaseName {
			get { return databaseName; }
			set { databaseName = value; }
		}
		private static string dataFolderPath = Directory.GetCurrentDirectory();
		
		public static string DataFolderPath {
			get { return dataFolderPath; }
			set { dataFolderPath = value; }
		}



        
		public Monitor()
		{
			// dataFolderPath = Directory.GetCurrentDirectory();
			database = String.Format(@"{0}\{1}", dataFolderPath, databaseName);
			dataSource = "data source=" + database;
			tableName = "product";
			#region Subscribe Event Handlers
			FiddlerApplication.AfterSessionComplete += FiddlerApplication_AfterSessionComplete;
			FiddlerApplication.OnNotification += FiddlerApplication_OnNotification;
			FiddlerApplication.Log.OnLogString += FiddlerApplication_OnLogString;
			FiddlerApplication.BeforeRequest += FiddlerApplication_BeforeRequest;
			FiddlerApplication.BeforeResponse += FiddlerApplication_BeforeResponse;
			#endregion
		}
		#region Event Handlers
		private void FiddlerApplication_BeforeResponse(Session session)
		{
			Console.WriteLine("{0}:HTTP {1} for {2}", session.id, session.responseCode, session.fullUrl);
			// Uncomment the following to decompress/unchunk the HTTP response
			// s.utilDecodeResponse();
		}

		private void FiddlerApplication_BeforeRequest(Session session)
		{
			Console.WriteLine("Before request for:\t" + session.fullUrl);
			// In order to enable response tampering, buffering mode must
			// be enabled; this allows FiddlerCore to permit modification of
			// the response in the BeforeResponse handler rather than streaming
			// the response to the client as the response comes in.
			session.bBufferResponse = true;
		}


		private void FiddlerApplication_OnNotification(object sender, NotificationEventArgs e)
		{
			Console.WriteLine("** NotifyUser: " + e.NotifyString);
		}

		private void FiddlerApplication_OnLogString(object sender, Fiddler.LogEventArgs e)
		{
			Console.WriteLine("** LogString: " + e.LogString);
		}

		private void FiddlerApplication_AfterSessionComplete(Session session)
		{
			// Ignore HTTPS connect requests
			if (session.RequestMethod == "CONNECT")
				return;

			if (session == null || session.oRequest == null || session.oRequest.headers == null)
				return;

			var full_url = session.fullUrl;
			Console.WriteLine("URL: " + full_url);

			HTTPRequestHeaders request_headers = session.RequestHeaders;
			HTTPResponseHeaders response_headers = session.ResponseHeaders;
			int http_response_code = response_headers.HTTPResponseCode;
			Console.WriteLine("HTTP Response: " + http_response_code.ToString());

			string referer = null;
			Dictionary<String, HTTPHeaderItem> request_headers_dictionary = request_headers.ToDictionary(p => p.Name);
			if (request_headers_dictionary.ContainsKey("Referer")) {
				referer = request_headers_dictionary["Referer"].Value;
			}

			//foreach (HTTPHeaderItem header_item in response_headers)
			//{
			//    Console.Error.WriteLine(header_item.Name + " " + header_item.Value);
			//}

			//foreach (HTTPHeaderItem header_item in request_headers)
			//{
			//    Console.Error.WriteLine(header_item.Name + " " + header_item.Value);
			//}
			Console.Error.WriteLine("Referer: " + referer);

			var timers = session.Timers;
			TimeSpan duration = (TimeSpan)(timers.ClientDoneResponse - timers.ClientBeginRequest);
			Console.Error.WriteLine(String.Format("Duration: {0:F10}", duration.Milliseconds));
			var dic = new Dictionary<string, object>() {
				{ "url" ,full_url }, { "status", http_response_code },
				{ "duration", duration.Milliseconds },
				{ "referer", referer }
			};
			insert(dic);

			// https://groups.google.com/forum/#!msg/httpfiddler/RuFf5VzKCg0/wcgq-WeUnCoJ
			//// the following code does not work as intended: request body is always blank
			//string request_body = session.GetRequestBodyAsString();

			//if (!string.IsNullOrEmpty(request_body))
			//{
			//    // TODO: UrlDecode
			//    Console.Error.WriteLine(string.Join(Environment.NewLine, request_body.Split(new char[] { '&' })));
			//}
		}
		#endregion
		#region DB Helpers
		bool TestConnection()
		{
			Console.WriteLine(String.Format("Testing database connection {0}...", database));
			try {
				using (SQLiteConnection conn = new SQLiteConnection(dataSource)) {
					conn.Open();
					conn.Close();
				}
				return true;
			} catch (Exception ex) {
				Console.Error.WriteLine(ex.ToString());
				return false;
			}
		}

		public bool insert(Dictionary<string, object> dic)
		{
			try {
				using (SQLiteConnection conn = new SQLiteConnection(dataSource)) {
					using (SQLiteCommand cmd = new SQLiteCommand()) {
						cmd.Connection = conn;
						conn.Open();
						SQLiteHelper sh = new SQLiteHelper(cmd);
						sh.Insert(tableName, dic);
						conn.Close();
						return true;
					}
				}
			} catch (Exception ex) {
				Console.Error.WriteLine(ex.ToString());
				return false;
			}
		}

		public void createTable()
		{
			using (SQLiteConnection conn = new SQLiteConnection(dataSource)) {
				using (SQLiteCommand cmd = new SQLiteCommand()) {
					cmd.Connection = conn;
					conn.Open();
					SQLiteHelper sh = new SQLiteHelper(cmd);
					sh.DropTable(tableName);

					SQLiteTable tb = new SQLiteTable(tableName);
					tb.Columns.Add(new SQLiteColumn("id", true));
					tb.Columns.Add(new SQLiteColumn("url", ColType.Text));
					tb.Columns.Add(new SQLiteColumn("referer", ColType.Text));
					tb.Columns.Add(new SQLiteColumn("status", ColType.Integer));
					tb.Columns.Add(new SQLiteColumn("duration", ColType.Decimal));
					sh.CreateTable(tb);
					conn.Close();
				}
			}
		}
		#endregion

		public void Start()
		{
			
			Console.WriteLine("Starting application...");

			// https://www.codeproject.com/Articles/1214209/Selenium-Series-Part-Killing-Laying-Around-Browser
			Console.WriteLine("Terminating runaway selenium drivers...");
			Process.GetProcessesByName("IEDriverServer").ToList().ForEach(p => p.Kill());
			Process.GetProcessesByName("chromedriver").ToList().ForEach(p => p.Kill());
			Process.GetProcessesByName("geckodriver").ToList().ForEach(p => p.Kill());

			Console.WriteLine("Starting Database connection...");
			// dataFolderPath = Directory.GetCurrentDirectory();
			database = String.Format(@"{0}\{1}", dataFolderPath, databaseName);
			dataSource = "data source=" + database;
			tableName = "product";

			TestConnection();
			// TODO:
			// http://dynamicjson.codeplex.com/
			createTable();
			int open_port = getAvailablePort();
			// allow connections to HTTPS sites w/invalid certificates
			Fiddler.CONFIG.IgnoreServerCertErrors = true;

			// To decrypt HTTPS traffic, makecert.exe must be present
			// TODO: read HKEY_CURRENT_USER\Software\Telerik\FiddlerCoreAPI
			// or rely on nuget to install
			// or define the MakeCert.exe path manually.
			// FiddlerApplication.Prefs.SetStringPref("fiddler.config.path.makecert", @"c:\tools\FiddlerCoreAPI\Makecert.exe");
			// FiddlerApplication.Prefs.SetBoolPref("fiddler.network.streaming.abortifclientaborts", true);//Abort session when client abort

			// https://github.com/rkprajapat/webtester/blob/master/FiddlerCoreAPI/FiddlerCore.chm
			// discourages the following
			// FiddlerApplication.Startup(open_port, /* Register As System Proxy */ true, /* Decrypt SSL */ true);
			// in favour of
			
			
			FiddlerApplication.Startup(open_port,
				FiddlerCoreStartupFlags.CaptureLocalhostTraffic |
				FiddlerCoreStartupFlags.RegisterAsSystemProxy |
				FiddlerCoreStartupFlags.MonitorAllConnections |
				FiddlerCoreStartupFlags.ChainToUpstreamGateway |
				(FiddlerCoreStartupFlags.Default & ~FiddlerCoreStartupFlags.DecryptSSL));

			int fiddler_listen_port = Fiddler.FiddlerApplication.oProxy.ListenPort;

			Console.WriteLine("Fiddler starting on port " + fiddler_listen_port);
			#region Firefox-specific
			// Usage:
			FirefoxOptions options = new FirefoxOptions();
			// TODO: detect browser application version
			options.UseLegacyImplementation = true;
			System.Environment.SetEnvironmentVariable("webdriver.gecko.driver", String.Format(@"{0}\geckodriver.exe", System.IO.Directory.GetCurrentDirectory()));
			// TODO: System.ArgumentException: Preferences cannot be set directly when using the legacy FirefoxDriver implementation. Set them in the profile.
			// options.SetPreference("network.automatic-ntlm-auth.trusted-uris", "http://,https://");
			// options.SetPreference("network.automatic-ntlm-auth.allow-non-fqdn", true);
			// options.SetPreference("network.negotiate-auth.delegation-uris", "http://,https://");
			// options.SetPreference("network.negotiate-auth.trusted-uris", "http://,https://");
			var profile = new FirefoxProfile();

			profile.SetPreference("network.automatic-ntlm-auth.trusted-uris", "http://,https://");
			profile.SetPreference("network.automatic-ntlm-auth.allow-non-fqdn", true);
			profile.SetPreference("network.negotiate-auth.delegation-uris", "http://,https://");
			profile.SetPreference("network.negotiate-auth.trusted-uris", "http://,https://");
			// profile.SetPreference("network.http.phishy-userpass-length", 255);
			// System.ArgumentException: Preference network.http.phishy-userpass-length may not be overridden: frozen value=255, requested value=255
			// profile.SetPreference("security.csp.enable", false);
			// Preference security.csp.enable may not be overridden: frozen value=False, requested value=False

			// TODO:  'OpenQA.Selenium.Firefox.FirefoxProfile.SetProxyPreferences(OpenQA.Selenium.Proxy)' is obsolete:
			// 'Use the FirefoxOptions class to set a proxy for Firefox.'
			// https://gist.github.com/temyers/e3246d666a27c59db04a
			// https://gist.github.com/temyers/e3246d666a27c59db04a			
			// Configure proxy
			profile.SetPreference("network.proxy.type", 1);
			profile.SetPreference("network.proxy.http", "localhost");
			profile.SetPreference("network.proxy.http_port", fiddler_listen_port);
			profile.SetPreference("network.proxy.ssl", "localhost");
			profile.SetPreference("network.proxy.ssl_port", fiddler_listen_port);
			profile.SetPreference("network.proxy.socks", "localhost");
			profile.SetPreference("network.proxy.socks_port", fiddler_listen_port);
			profile.SetPreference("network.proxy.ftp", "localhost");
			profile.SetPreference("network.proxy.ftp_port", fiddler_listen_port);
			profile.SetPreference("network.proxy.no_proxies_on", "localhost, 127.0.0.1");

			options.Profile = profile;
            
			var selenium = new FirefoxDriver(options);
			#endregion
		}

		private int getAvailablePort()
		{
			try {
				Socket sock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
				sock.Bind(new IPEndPoint(IPAddress.Any, 0));
				int port = ((IPEndPoint)sock.LocalEndPoint).Port;
				sock.Dispose();
				return port;
			} catch (Exception ex) {
				Console.Error.WriteLine(ex.Message);
				return 0;
			}
		}

		// TODO : extract cookies
		private void extract_headers_basic(string text)
		{
			foreach (Match m in Regex.Matches(text, @"(?<name>[^ ]+):(?<value>.+)\r\n")) {
				Console.Error.WriteLine(String.Format("Header name = [{0}]", m.Groups["name"]));
				Console.Error.WriteLine(String.Format("Data = [{0}]", m.Groups["value"]));
			}
		}

		public void Stop()
		{
			Console.WriteLine("Shut down Fiddler Application.");
			#region Unsubscribe Event handlers
			FiddlerApplication.AfterSessionComplete -= FiddlerApplication_AfterSessionComplete;
			// explicitly unsubscribe dangling events
			FiddlerApplication.OnNotification -= FiddlerApplication_OnNotification;
			FiddlerApplication.Log.OnLogString -= FiddlerApplication_OnLogString;
			FiddlerApplication.BeforeRequest -= FiddlerApplication_BeforeRequest;
			FiddlerApplication.BeforeResponse -= FiddlerApplication_BeforeResponse;
			// alternative cleanup ?
			// http://stackoverflow.com/questions/91778/how-to-remove-all-event-handlers-from-a-control
			FiddlerApplication.OnNotification += delegate(object sender, NotificationEventArgs e) {
			};
			FiddlerApplication.Log.OnLogString += delegate(object sender, Fiddler.LogEventArgs e) {
			};
			FiddlerApplication.BeforeRequest += delegate {
			};
			FiddlerApplication.BeforeResponse += delegate {
			};
			// https://bytes.com/topic/c-sharp/answers/274921-removing-all-event-handlers
			#endregion
			if (FiddlerApplication.IsStarted()) {
				FiddlerApplication.Shutdown();
			}
			System.Threading.Thread.Sleep(1);
		}

		// Not necessary when Fidddler Core is called from Powershell
		public static void Main(string[] args)
		{
			proxy = new Monitor();
			#region Subscribe Event Handlers
			NativeMethods.Handler = ConsoleEventCallback;
			NativeMethods.SetConsoleCtrlHandler(NativeMethods.Handler, true);
			#endregion
			proxy.Start();
			Console.WriteLine("Press CTRL-C to exit"); 
			Object forever = new Object();
			lock (forever) {
				System.Threading.Monitor.Wait(forever);
			}
		}

		#region Event Handlers
		// https://msdn.microsoft.com/en-us/library/windows/desktop/ms683242%28v=vs.85%29.aspx
		private static bool ConsoleEventCallback(int eventType)
		{
			try {
				proxy.Stop();
				System.Threading.Thread.Sleep(1);
				RegistryKey myKey = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Internet Settings", true);
				if (myKey != null) {
					myKey.SetValue("ProxyEnable", 0, RegistryValueKind.DWord);
				}
				myKey.Close();
			} catch {
				// ignored
			}
			return false;
		}
		#endregion

		#region Native Methods
		internal static class NativeMethods
		{
			// entry for GC
			internal static ConsoleEventDelegate Handler;
			[DllImport("kernel32.dll", SetLastError = true)]
			internal static extern bool SetConsoleCtrlHandler(ConsoleEventDelegate callback, bool add);
			// p/invoke
			internal delegate bool ConsoleEventDelegate(int eventType);
		}
		#endregion
	}
}