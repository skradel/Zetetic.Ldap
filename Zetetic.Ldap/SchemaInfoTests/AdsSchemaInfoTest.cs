using Zetetic.Ldap.Schema;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.DirectoryServices.Protocols;

namespace SchemaInfoTests
{
    
    
    /// <summary>
    ///This is a test class for AdsSchemaInfoTest and is intended
    ///to contain all AdsSchemaInfoTest Unit Tests
    ///</summary>
    [TestClass()]
    public class AdsSchemaInfoTest
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


        /// <summary>
        ///A test for Initialize
        ///</summary>
        [TestMethod()]
        public void InitializeTest()
        {
            ISchemaInfo target = new AdsSchemaInfo();
            using (LdapConnection conn = new LdapConnection("localhost:20389"))
            {
                conn.Bind(System.Net.CredentialCache.DefaultNetworkCredentials);
                target.Initialize(conn);
            }

            int ocs = 0;
            foreach (ObjectClassSchema o in target.ObjectClasses)
            {
                System.Console.WriteLine("oc: {0}", o);
                foreach (AttributeSchema a in o.MustHave)
                    System.Console.WriteLine("  must: {0} as {1}", a, a.LangType);

                foreach (AttributeSchema a in o.MayHave)
                    System.Console.WriteLine("  may : {0} as {1}", a, a.LangType);

                ocs++;
            }

            Assert.IsTrue(ocs >= 10, "At least 10 object classes found");

            ObjectClassSchema user = target.GetObjectClass("USER");
            Assert.IsTrue(user != null, "Found 'USER' (mixed case) objectclass");

            user = target.GetObjectClass("NO-SUCH-THING");
            Assert.IsNull(user);

            AttributeSchema attr = target.GetAttribute("cn");
            Assert.IsNotNull(attr);

            attr = target.GetAttribute("NO-ATTRIBUTE");
            Assert.IsNull(attr);
        }
    }
}
