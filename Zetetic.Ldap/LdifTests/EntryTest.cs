using Zetetic.Ldap;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.DirectoryServices.Protocols;

namespace LdifTests
{
    
    
    /// <summary>
    ///This is a test class for EntryTest and is intended
    ///to contain all EntryTest Unit Tests
    ///</summary>
    [TestClass()]
    public class EntryTest
    {


        private TestContext testContextInstance;

        /// <summary>
        ///Gets or sets the test context which provides
        ///information about and functionality for the current test run.
        ///</summary>
        public TestContext TestContext
        {
            get
            {
                return testContextInstance;
            }
            set
            {
                testContextInstance = value;
            }
        }

        #region Additional test attributes
        // 
        //You can use the following additional attributes as you write your tests:
        //
        //Use ClassInitialize to run code before running the first test in the class
        //[ClassInitialize()]
        //public static void MyClassInitialize(TestContext testContext)
        //{
        //}
        //
        //Use ClassCleanup to run code after all tests in a class have run
        //[ClassCleanup()]
        //public static void MyClassCleanup()
        //{
        //}
        //
        //Use TestInitialize to run code before running each test
        //[TestInitialize()]
        //public void MyTestInitialize()
        //{
        //}
        //
        //Use TestCleanup to run code after each test has run
        //[TestCleanup()]
        //public void MyTestCleanup()
        //{
        //}
        //
        #endregion

        [TestMethod]
        public void Entry_Indexer_BasicTest()
        {
            Entry target = new Entry(@"uid=jblow,ou=users,dc=test,dc=com");
            target["test"] = new Attr("test", new[] { "value" });

            string expected = @"value";
            string actual = (string) target["TEST"].Value[0];

            foreach (var a in target)
            {
                System.Console.WriteLine("Attribute {0} in enumerator", a.Name);
            }

            Assert.AreEqual(expected, actual);
            Assert.IsFalse(target.IsDnDirty);
        }

        /// <summary>
        ///A test for RDN
        ///</summary>
        [TestMethod()]
        public void RDN_Getter_Simple_Parse()
        {
            
            Entry target = new Entry(@"uid=jblow,ou=users,dc=test,dc=com");

            string expected = @"uid=jblow";
            string actual = target.RDN;

            Assert.AreEqual(expected, actual);
            Assert.IsFalse(target.IsDnDirty);
        }

        [TestMethod()]
        public void RDN_Getter_Escaped_Parse()
        {
            Entry target = new Entry(@"uid=lunchbox\, joe,ou=users,dc=test,dc=com");

            string expected = @"uid=lunchbox\, joe";
            string actual = target.RDN;

            Assert.AreEqual(expected, actual);
            Assert.IsFalse(target.IsDnDirty);
        }

        [TestMethod()]
        public void RDN_Setter_Escaped_Parse()
        {

            Entry target = new Entry(@"uid=lunchbox\, joe,ou=users,dc=test,dc=com");
            target.RDN = "uid=joe";

            string expected = @"uid=joe,ou=users,dc=test,dc=com";
            string actual = target.DistinguishedName;

            Assert.AreEqual(expected, actual);
            Assert.IsTrue(target.IsDnDirty);
        }

        /// <summary>
        ///A test for EscapeNamingComponent
        ///</summary>
        [TestMethod()]
        public void EscapeNamingComponentTest()
        {
            string namingComponent = @"cn=blow, joe";
            string expected = @"cn=blow\, joe";
            string actual = Entry.EscapeNamingComponent(namingComponent);
            Assert.AreEqual(expected, actual);
        }

        /// <summary>
        ///A test for SuperiorDn
        ///</summary>
        [TestMethod()]
        public void SuperiorDnTest()
        {
            Entry t = new Entry("uid=joe,ou=users,dc=test,dc=com");
            t.SuperiorDn = "ou=superusers,dc=test,dc=com";

            string expected = "ou=superusers,dc=test,dc=com";
            string actual = t.SuperiorDn;

            Assert.AreEqual(expected, actual);
            Assert.IsTrue(t.IsDnDirty);
            Assert.IsTrue(t.IsSuperiorDirty);
        }
    }
}
