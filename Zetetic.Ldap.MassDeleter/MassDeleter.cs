using System;
using System.Collections.Generic;
using System.Text;
using System.DirectoryServices.Protocols;
using System.Threading;
using System.Threading.Tasks;

namespace Zetetic.Ldap
{
    class MassDeleter
    {
        static void Main(string[] args)
        {
            string server = null, searchBase = null, filter = null, user = null, password = null, domain = null;
            int connCount = 5;
            bool showUsage = false;
            System.Net.NetworkCredential nc = null;

            try
            {
                for (int i = 0; i < args.Length; i++)
                {
                    switch (args[i])
                    {
                        case "-?":
                        case "/?":
                            showUsage = true;
                            break;

                        case "-server":
                        case "-s":
                            server = args[++i];
                            break;

                        case "-filter":
                        case "-f":
                        case "-r":
                            filter = args[++i];
                            break;

                        case "-count":
                        case "-c":
                            connCount = int.Parse(args[++i]);
                            break;

                        case "-b":
                            searchBase = args[++i];
                            break;

                        case "-user":
                        case "-u":
                            user = args[++i];
                            break;

                        case "-domain":
                        case "-d":
                            domain = args[++i];
                            break;

                        case "-password":
                        case "-p":
                        case "-w":
                            password = args[++i];
                            break;
                    }
                }

                if (showUsage || string.IsNullOrEmpty(server) || string.IsNullOrEmpty(filter))
                {
                    Console.Error.WriteLine("Zetetic.Ldap.MassDeleter {0}", System.Reflection.Assembly.GetExecutingAssembly().GetName().Version);
                    Console.Error.WriteLine("Usage: -s <server> -b <searchBase>  -f <filter> [-c connectionCount] [-u <user> -p <pass> [[-d domain]]");
                    return;
                }

                if (!string.IsNullOrEmpty(user))
                {
                    if (string.IsNullOrEmpty(domain))
                    {
                        nc = new System.Net.NetworkCredential(user, password);
                    }
                    else
                    {
                        nc = new System.Net.NetworkCredential(user, password, domain);
                    }
                }

                Delete(server, searchBase, filter, connCount, nc);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Error: {0} :: {1}", ex.Message, ex.StackTrace);
                System.Environment.ExitCode = 1;
            }
        }

        static IEnumerable<SearchResultEntry> ISE(SearchResultEntryCollection results)
        {
            foreach (SearchResultEntry se in results)
                yield return se;
        }

        static void Delete(string server, string searchBase, string filter, int connCount, System.Net.NetworkCredential nc)
        {
            using (LdapConnection conn = new LdapConnection(server))
            {
                conn.SessionOptions.ProtocolVersion = 3;
                conn.Credential = nc ?? System.Net.CredentialCache.DefaultNetworkCredentials;
                conn.AutoBind = false;
                conn.Bind();

                IList<LdapConnection> conns = new List<LdapConnection>();
                for (int i = 0; i < connCount; i++)
                {
                    var c = new LdapConnection(server);

                    c.SessionOptions.ProtocolVersion = 3;
                    c.Credential = nc;
                    c.AutoBind = false;
                    c.Bind();

                    conns.Add(c);
                }

                Console.WriteLine("Created {0} connections", conns.Count);

                var req = new SearchRequest
                {
                    DistinguishedName = searchBase,
                    Filter = filter ?? "(&(objectClass=person)(cn=*CNF:*))"
                };

                req.Attributes.Add("1.1");

                req.Controls.Add(new PageResultRequestControl(1000)
                {
                    IsCritical = false
                });

                while (true)
                {
                    var resp = (SearchResponse)conn.SendRequest(req);
                    var lazy = new LazyCommitControl() { IsCritical = false };

                    Parallel.ForEach(ISE(resp.Entries), entry =>
                    {
                        try
                        {
                            var delreq = new DeleteRequest(entry.DistinguishedName);
                            delreq.Controls.Add(lazy);

                            conns[Thread.CurrentThread.ManagedThreadId % connCount].SendRequest(delreq);

                            Console.Error.WriteLine("Deleted {0}", entry.DistinguishedName);
                        }
                        catch (Exception ex)
                        {
                            Console.Error.WriteLine("Failed to delete {0}: {1}", entry.DistinguishedName, ex.Message);
                            throw;
                        }
                    }
                    );

                    if (resp.Controls.Length == 0)
                        break;

                    var prc = (PageResultResponseControl)resp.Controls[0];
                    if (prc.Cookie.Length == 0)
                        break;

                    Console.WriteLine("On to the next page!");

                    req.Controls.Clear();
                    req.Controls.Add(new PageResultRequestControl(prc.Cookie));
                }

                Console.WriteLine("Complete");
            }
        }
    }
}
