### FiddlerCore Page Performance Collector with SQLite database
Collect details of the web navigation by establishing the proxy with the help of [FiddlerCore API](http://www.google.com/url?sa=t&rct=j&q=&esrc=s&source=web&cd=1&cad=rja&uact=8&ved=0CCoQFjAAahUKEwjAjsXr44XGAhUCz4AKHa-LAKA&url=http%3A%2F%2Fwww.telerik.com%2Ffiddler%2Ffiddlercore&ei=IYV4VYD6OYKegwSvl4KACg&usg=AFQjCNFytjHPn-EXeXR3Vr-LT-syJw-huw&bvm=bv.95277229,d.eXY)
for the duration of the execution of the test.

### Writing Tests
Include the following code from `Program.cs` into your project 
```c#
proxy = new Monitor();
proxy.Start();
// your test case here
proxy.Stop();
```
  
and merge `nuget.config` with yours. This will provide delegate to
`OnSessionComplete` event where selected headers
 * `id`
 * `url`
 * `referer`
 * `duration`
 * `status`

are stored in the SQLite database `fiddler-data.db` for measuring page performance at the individual Page element level.

![SQLite database capture](https://raw.githubusercontent.com/sergueik/fiddlercore_sqlite/master/screenshots/capture1.png)

The `duration` is computed from [Fiddler session timers](http://fiddler.wikidot.com/timers) as
```c#
var timers = fiddler_session.Timers;
TimeSpan duration = (TimeSpan)(timers.ClientDoneResponse - timers.ClientBeginRequest);
```
Alternatively  you can run the `Program` as standalone application while you are executing the test step in question.

The `OnNotification`, `BeforeRequest` and `BeforeResponse`
[events](https://github.com/jimevans/WebDriverProxyExamples/blob/master/lib/FiddlerCore4.XML) are not currently used.
A replica of [SQLite Helper (C#) project sources](http://sh.codeplex.com) is included as the `Sqlite.Utils` namespace (Utils directory).
This provides low-level, non-LINQ access to SQL database API.

### References
* [FiddlerCore API](https://github.com/rkprajapat/webtester/blob/master/FiddlerCoreAPI/FiddlerCore.chm)
* [Using FiddlerCore to capture HTTP Requests with .NET](http://weblog.west-wind.com/posts/2014/Jul/29/Using-FiddlerCore-to-capture-HTTP-Requests-with-NET)
* [FiddlerCore dealing with Certificates](http://stackoverflow.com/questions/24969198/how-do-i-get-fiddlercore-programmatic-certificate-installation-to-stick)

### Note
The [browsermob-proxy](https://github.com/lightbody/browsermob-proxy) offers similar functionality for Java - see e.g. [http://amormoeba.blogspot.com/2014/02/how-to-use-browser-mob-proxy.html][http://amormoeba.blogspot.com/2014/02/how-to-use-browser-mob-proxy.html]

### Author
[Serguei Kouzmine](kouzmine_serguei@yahoo.com)