using System;
using Zetetic.Ldap;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LdifTests
{
    
    
    /// <summary>
    ///This is a test class for LdifReaderTest and is intended
    ///to contain all LdifReaderTest Unit Tests
    ///</summary>
    [TestClass()]
    public class LdifReaderTest
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
        ///A test for LdifReader Constructor
        ///</summary>
        [TestMethod()]
        public void LdifReaderConstructorTest()
        {
            LdifReader target = new LdifReader(@"c:\temp\testinput.ldif");

            target.OnAttributeValue += delegate(object sender, AttributeEventArgs args)
            {
                Console.WriteLine("Attr name {0}, value {1}", args.Name, args.Value);
                if (args.Value != null && args.Value is byte[])
                {
                    Console.WriteLine(" ---> " + ((byte[])args.Value).Length);
                }
            };

            target.OnBeginEntry += delegate(object sender, DnEventArgs args)
            {
                Console.WriteLine("Begin: {0}", args.DistinguishedName);
            };

            target.OnEndEntry += delegate(object sender, DnEventArgs args)
            {
                Console.WriteLine("End: {0}", args.DistinguishedName);
            };

            int i = 0;
            while (target.Read() && i++ < 200)
            {
                // Do stuff!
            }

            Assert.Inconclusive("TODO: Implement code to verify target");
        }

        
    }
}
