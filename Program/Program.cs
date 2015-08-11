using System;
using System.Diagnostics.Eventing.Reader;
using System.Security;
using System.Collections;
using Newtonsoft.Json;

namespace EventQuery
{
    public class EventQueryExample
    {
        // log the entries to console
        private bool _verbose;
        public bool Verbose
        {
            get
            {
                return _verbose;
            }
            set
            {
                _verbose = value;
            }

        }

        private String _query = @"<QueryList>" +
                  "<Query Id=\"0\" Path=\"Microsoft-Windows-TaskScheduler/Operational\">" +
                  "<Select Path=\"Microsoft-Windows-TaskScheduler/Operational\">" +
                  "*[System[(Level=1  or Level=2 or Level=3 or Level=4) and " +
                  "TimeCreated[timediff(@SystemTime) &lt;= 14400000]]]" + "</Select>" +
                  "</Query>" +
                  "</QueryList>";
        public String Query
        {
            get
            {
                return _query;
            }
            set
            {
                _query = value;
            }

        }


        public object[] QueryActiveLog()
        {
            // TODO: Extend structured query to two different event logs.
            EventLogQuery eventsQuery = new EventLogQuery("Application", PathType.LogName, Query);
            EventLogReader logReader = new EventLogReader(eventsQuery);
            return DisplayEventAndLogInformation(logReader);
        }

        private object[] DisplayEventAndLogInformation(EventLogReader logReader)
        {
            ArrayList eventlog_json_arraylist = new ArrayList();
            for (EventRecord eventInstance = logReader.ReadEvent();
                null != eventInstance; eventInstance = logReader.ReadEvent())
            {

                string eventlog_json = JsonConvert.SerializeObject(eventInstance);
                eventlog_json_arraylist.Add(eventlog_json);

                if (Verbose)
                {
                    Console.WriteLine("-----------------------------------------------------");
                    Console.WriteLine("Event ID: {0}", eventInstance.Id);
                    Console.WriteLine("Level: {0}", eventInstance.Level);
                    Console.WriteLine("LevelDisplayName: {0}", eventInstance.LevelDisplayName);
                    Console.WriteLine("Opcode: {0}", eventInstance.Opcode);
                    Console.WriteLine("OpcodeDisplayName: {0}", eventInstance.OpcodeDisplayName);
                    Console.WriteLine("TimeCreated: {0}", eventInstance.TimeCreated);
                    Console.WriteLine("Publisher: {0}", eventInstance.ProviderName);
                }
                try
                {
                    if (Verbose)
                    {
                        Console.WriteLine("Description: {0}", eventInstance.FormatDescription());
                    }
                }
                catch (EventLogException)
                {

                    // The event description contains parameters, and no parameters were 
                    // passed to the FormatDescription method, so an exception is thrown.

                }

                // Cast the EventRecord object as an EventLogRecord object to 
                // access the EventLogRecord class properties
                EventLogRecord logRecord = (EventLogRecord)eventInstance;
                if (Verbose)
                {
                    Console.WriteLine("Container Event Log: {0}", logRecord.ContainerLog);
                }
            }
            object[] result = eventlog_json_arraylist.ToArray();
            return result;
        }


    }
}
