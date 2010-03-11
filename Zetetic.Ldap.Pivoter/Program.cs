using System;
using System.Collections.Generic;
using System.Text;
using Zetetic.Ldap;

namespace Zetetic.Ldap.Pivoter
{
    class Program
    {
        private static readonly string SamplePath = @"%TEMP%\serdemo.xml";

        private static void EmitSampleXml(LdifCsvPivot pivot, System.Xml.Serialization.XmlSerializer ser)
        {
            try
            {
                string temp = System.Environment.GetEnvironmentVariable("TEMP");
                string opath = SamplePath.Replace("%TEMP%", temp ?? @"c:\temp");

                if (!System.IO.File.Exists(opath))
                {
                    using (System.IO.FileStream fs = new System.IO.FileStream(opath, System.IO.FileMode.Create))
                    {
                        ser.Serialize(fs, pivot.Columns);
                    }
                }
            }
            catch (Exception x)
            {
                Console.Error.WriteLine("Couldn't generate sample config at {0}: {1}",
                    SamplePath, x.Message);
            }
        }

        private static void Run(List<string> ldifs, string colConfig, string fileOut)
        {
            System.Xml.Serialization.XmlSerializer ser = new System.Xml.Serialization.XmlSerializer(
                typeof(List<PivotColumn>));

            LdifCsvPivot pivot = new LdifCsvPivot();

            if (string.IsNullOrEmpty(colConfig))
            {
                pivot.AddColumn("dn", "dn", null);
                pivot.AddColumn("rdn", "rdn", null);
                pivot.AddColumn("parent", "parent", null);
                pivot.AddColumn("samaccountname", "uid", null);
                pivot.AddColumn("objectclass", "objectclass", PivotColumn.LAST_INDEX);
                pivot.AddColumn("sn", "sn", null);
                pivot.AddColumn("givenName", "givenName", null);
                pivot.AddColumn("mail", "mail", null);

                EmitSampleXml(pivot, ser);
            }
            else
            {
                using (System.IO.FileStream fs = new System.IO.FileStream(colConfig, System.IO.FileMode.Open, System.IO.FileAccess.Read))
                {
                    pivot.Columns = (List<PivotColumn>)ser.Deserialize(fs);
                }
            }

            if (!string.IsNullOrEmpty(fileOut))
                pivot.Output = new System.IO.StreamWriter(fileOut, false, new System.Text.UTF8Encoding(false, true));

            try
            {
                int i = 0;
                foreach (string s in ldifs)
                {
                    try
                    {
                        pivot.Process(s, i++ == 0);
                    }
                    catch (Exception)
                    {
                        Console.Error.WriteLine("Failed on file {0}", s);
                        throw;
                    }
                }
            }
            finally
            {
                if (!string.IsNullOrEmpty(fileOut))
                    pivot.Output.Dispose();
            }
        }

        static void PrintUsage()
        {
            Console.WriteLine(@"");
            Console.WriteLine(@"Zetetic.Ldap.Pivoter - Usage:");
            Console.WriteLine(@"");
            Console.WriteLine(@"This is a simple tool to transform an LDIF file into a delimited");
            Console.WriteLine(@"file for relational analysis.  It uses the Zetetic.Ldap library for");
            Console.WriteLine(@"advanced LDIF parsing.");
            Console.WriteLine(@"");
            Console.WriteLine(@"Zetetic.Ldap.Pivoter.exe -f <ldif spec> [-c columnConfig.xml] [-o outfile.txt]");
            Console.WriteLine(@"");
            Console.WriteLine(@"<ldif spec> = a comma-separated list of LDIF paths");
            Console.WriteLine(@"  For wildcard support, separate the path and search pattern with ]");
            Console.WriteLine(@"  Example:  -f c:\temp]*.ldif,c:\ldifs]*.ldif");
            Console.WriteLine(@"");
            Console.WriteLine(@"The -c argument tells Pivoter what values to pull from the LDIF and");
            Console.WriteLine(@"how to handle them (for example, whether or not to join all the values");
            Console.WriteLine(@"from multivalued attributes).");
            Console.WriteLine(@"");
            Console.WriteLine(@"If specified, 'outfile' will be UTF8 encoded (without BOM).");
            Console.WriteLine(@"Any existing file will be replaced.");
            Console.WriteLine(@"");
            Console.WriteLine(@"If you call Pivoter without any columnConfig, one will be generated");
            Console.WriteLine(@"for you at {0}, if that file does not already exist.", SamplePath);
            Console.WriteLine(@"");
        }

        static void Main(string[] args)
        {
            List<string> ldifs = new List<string>();
            string colConfig = null, fileOut = null;
            bool wantUsage = false;

            try
            {
                for (int i = 0; i < args.Length; i++)
                {
                    switch (args[i])
                    {
                        case "-?":
                            wantUsage = true;
                            break;

                        case "-o":
                            fileOut = args[++i];
                            break;

                        case "-f":
                            foreach (string s in args[++i].Split(','))
                            {
                                int pos = s.IndexOf(']');
                                if (pos > 0)
                                {
                                    string dirPart = s.Substring(0, pos),
                                        searchPart = s.Substring(pos + 1);
                                    try
                                    {
                                        ldifs.AddRange(System.IO.Directory.GetFiles(dirPart, searchPart));
                                    }
                                    catch (Exception e)
                                    {
                                        Console.Error.WriteLine("Failed to search dir {0}, query {1}, stack {2}", dirPart, searchPart, e.StackTrace);
                                        throw;
                                    }
                                }
                                else
                                {
                                    ldifs.Add(s);
                                }
                            }
                            break;

                        case "-c":
                            colConfig = args[++i];
                            break;
                    }
                }

                if (wantUsage || ldifs.Count == 0)
                {
                    PrintUsage();
                }
                else
                {
                    Run(ldifs, colConfig, fileOut);
                }
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e.Message + " :: " + e.StackTrace);
                System.Environment.ExitCode = 1;
            }
        }
    }
}
