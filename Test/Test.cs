using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Diagnostics.Eventing.Reader;
using System.Security;
using System.Collections;

namespace EventQuery
{
    [TestClass]
    public class EventQueryTest
    {
        private EventQueryExample _sut;
        [TestInitialize]
        public void SetUp()
        {
            _sut = new EventQueryExample();
        }

        [TestMethod]
        public void QueryActiveLog()
        {
            _sut.Verbose = false;
            _sut.QueryActiveLog();
        }
    }
}
