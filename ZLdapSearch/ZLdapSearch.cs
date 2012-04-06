using System;
using System.Collections.Generic;
using System.Text;
using System.DirectoryServices.Protocols;
using Zetetic.Ldap;

namespace Zetetic.Ldap
{
    class ZLdapSearch
    {
        static void ShowUsage()
        {
            Console.Error.WriteLine(@"
ZLdapSearch... an ldapsearch-like tool
Part of the Zetetic.Ldap project at https://github.com/skradel/Zetetic.Ldap

Arguments:

Mandatory
----
-s     servername[:port] (alternate: -h)
-f     LDAP filter       (alternate: -r)
-b     search base

Optional
----
-l     attribute list (comma or space separated)
-o     file output
-m     size limit
-a     authentication type
       (see: http://msdn.microsoft.com/en-us/library/system.directoryservices.protocols.authtype.aspx)
-x     use Basic authentication
-anon  use Anonymous authentication
-u     bind username    (alternate: -D)
-p     bind password    (alternate: -w)
-d     bind domain
-ssl
");
        }

        static void Main(string[] args)
        {
            try
            {
                AuthType auth = AuthType.Negotiate;
                string server = null, searchBase = null, filter = null, user = null, pass = null, domain = null, fileOut = null;
                int maxcount = 0;
                bool ssl = false;
                string[] attrs = null;

                // Parse arguments

                for (int i = 0; i < args.Length; i++)
                {
                    switch (args[i])
                    {
                        case "-s":
                        case "-h":
                            server = args[++i];
                            break;

                        case "-o":
                            fileOut = args[++i];
                            break;

                        case "-b":
                            searchBase = args[++i];
                            break;

                        case "-f":
                        case "-r":
                            filter = args[++i];
                            break;

                        case "-l":
                            attrs = args[++i].Split(',', ' ');
                            break;

                        case "-m":
                            maxcount = int.Parse(args[++i]);
                            break;

                        case "-anon":
                            auth = AuthType.Anonymous;
                            break;

                        case "-x":
                            auth = AuthType.Basic;
                            break;

                        case "-a":
                            auth = (AuthType)Enum.Parse(typeof(AuthType), args[++i]);
                            break;

                        case "-D":
                        case "-u":
                            user = args[++i];
                            break;

                        case "-w":
                        case "-p":
                            pass = args[++i];
                            break;

                        case "-d":
                            domain = args[++i];
                            break;

                        case "-ssl":
                            ssl = true;
                            break;

                        default:
                            Console.Error.WriteLine("Unexpected argument: {0}", args[i]);
                            break;
                    }
                }

                if (args.Length == 0
                    || args[0].IndexOf('?') > -1
                    || string.IsNullOrEmpty(server)
                    || string.IsNullOrEmpty(filter))
                {
                    ShowUsage();
                    return;
                }

                using (var conn = new LdapConnection(server))
                {
                    conn.SessionOptions.ProtocolVersion = 3;
                    conn.SessionOptions.SecureSocketLayer = ssl;
                    conn.AuthType = auth;

                    if (!string.IsNullOrEmpty(user))
                    {
                        conn.Credential = string.IsNullOrEmpty(domain) ? new System.Net.NetworkCredential(user, pass)
                            : new System.Net.NetworkCredential(user, pass, domain);
                    }

                    conn.AutoBind = false;
                    conn.Bind();

                    var pager = new PagingHelper()
                    {
                        Connection = conn,
                        Attrs = attrs,
                        Filter = filter,
                        DistinguishedName = searchBase,
                        SizeLimit = maxcount
                    };

                    LdifWriter ldif = !string.IsNullOrEmpty(fileOut) ? new LdifWriter(fileOut)
                        : new LdifWriter(System.Console.Out);

                    ldif.WriteSummary = false;

                    using (ldif)
                    {
                        foreach (var entry in pager.GetResults())
                        {
                            ldif.BeginEntry(entry.DistinguishedName);

                            foreach (DirectoryAttribute attr in entry.Attributes.Values)
                            {
                                if (attr.Count == 0)
                                {
                                    Console.Error.WriteLine("Attribute {0} contains no values; possible ranged attr", attr.Name);
                                    continue;
                                }

                                try
                                {
                                    foreach (string s in attr.GetValues(typeof(string)))
                                    {
                                        ldif.WriteAttr(attr.Name, s);
                                    }
                                }
                                catch
                                {
                                    foreach (byte[] bytes in attr.GetValues(typeof(byte[])))
                                    {
                                        ldif.WriteAttr(attr.Name, bytes);
                                    }
                                }
                            }
                            ldif.EndEntry();
                        }
                        ldif.Close();
                    }
                }
            }
            catch (Exception e)
            {
                Console.Error.WriteLine("Error type {0}, message {1}: {2}", e.GetType(), e.Message, e.StackTrace);

                System.Environment.ExitCode = 1;
            }
        }
    }
}
