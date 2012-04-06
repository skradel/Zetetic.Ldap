using System;
using System.Collections.Generic;
using System.Text;
using Zetetic.Ldap.Vlv;
using System.DirectoryServices.Protocols;

namespace Zetetic.Ldap.VlvDemo
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                string server = null, dn = null, file = null;
                bool oneline = false;

                for (int i = 0; i < args.Length; i++)
                {
                    switch (args[i])
                    {
                        case "-s":
                        case "-server":
                            server = args[++i];
                            break;

                        case "-D":
                        case "-dn":
                        case "-b":
                        case "-base":
                            dn = args[++i];
                            break;

                        case "-o":
                            oneline = true;
                            break;

                        case "-f":
                        case "-file":
                            file = args[++i];
                            break;
                    }
                }

                using (var conn = new LdapConnection(server))
                {
                    conn.SessionOptions.ProtocolVersion = 3;
                    conn.AutoBind = false;

                    Console.Write("Binding... ");
                    
                    conn.Bind();

                    Console.WriteLine("Bound at {0}", DateTime.Now);

                    if (dn == null)
                    {
                        var t = (SearchResponse)conn.SendRequest(new SearchRequest("", "(&(objectClass=*))", SearchScope.Base, "defaultNamingContext"));
                        dn = t.Entries[0].Attributes["defaultNamingContext"][0].ToString();
                    }

                    var mgr = new VlvManager();

                    if (!string.IsNullOrEmpty(file))
                    {
                        using (var sr = new System.IO.StreamReader(file))
                        {
                            for (string line = sr.ReadLine(); line != null; line = sr.ReadLine())
                            {
                                if (line.StartsWith("#"))
                                    continue;

                                var parts = line.Split(new[]{'\t'}, StringSplitOptions.None);

                                if (parts.Length > 3)
                                {
                                    // Dn, Filter, Sort, Scope, Skip, Take

                                    Console.Write(line); Console.Write(oneline ? "\t" : Environment.NewLine);

                                    var sw = System.Diagnostics.Stopwatch.StartNew();

                                    try
                                    {
                                        var resp = mgr.GetData(
                                            conn: conn,
                                            distinguishedName: string.IsNullOrEmpty(parts[0]) ? dn : parts[0],
                                            filter: parts[1],
                                            sortBy: parts[2],
                                            scope: string.IsNullOrEmpty(parts[3]) ? SearchScope.OneLevel : (SearchScope)Enum.Parse(typeof(SearchScope), parts[3]),
                                            skip: parts.Length > 4 ? int.Parse(parts[4]) : 0,
                                            take: parts.Length > 5 ? int.Parse(parts[5]) : 25,
                                            attrs: null);

                                        Console.WriteLine("OK\t{0}\t{1}", sw.ElapsedMilliseconds, resp.ContentCount);
                                    }
                                    catch (Exception e)
                                    {
                                        Console.WriteLine("Error\t{0}\t0\t{1}", sw.ElapsedMilliseconds, e.Message);
                                    }
                                }
                                else
                                {
                                    Console.Error.WriteLine("Skip line: {0}", line);
                                }
                            }
                        }
                    }
                    else
                    {
                        while (true)
                        {
                            Console.Write("Enter search base [{0}] > ", dn);
                            string tmp = Console.ReadLine();
                            if (!string.IsNullOrEmpty(tmp))
                            {
                                dn = tmp;
                            }

                            Console.Write("Sort attr: [displayName] > ");
                            string sort = Console.ReadLine();

                            if (string.IsNullOrEmpty(sort))
                                sort = "displayName";

                            Console.Write("Enter filter: [{0}=*] > ", sort);
                            string filter = Console.ReadLine();

                            if (string.IsNullOrEmpty(filter))
                                filter = string.Format("(&({0}=*))", sort);

                            Console.Write("How many to skip? [0] ");
                            tmp = Console.ReadLine();

                            int skip = string.IsNullOrEmpty(tmp) ? 0 : int.Parse(tmp);

                            Console.Write("How many to take? [25] > ");
                            tmp = Console.ReadLine();

                            int take = string.IsNullOrEmpty(tmp) ? 25 : int.Parse(tmp);

                            Console.Write("Enter searchScope (base, onelevel, subtree) [onelevel] > ");
                            tmp = Console.ReadLine();

                            SearchScope scope;

                            switch (tmp.ToLowerInvariant())
                            {
                                case "b":
                                case "base":
                                    scope = SearchScope.Base;
                                    break;

                                case "s":
                                case "sub":
                                case "subtree":
                                    scope = SearchScope.Subtree;
                                    break;

                                default:
                                    scope = SearchScope.OneLevel;
                                    break;
                            }

                            var sw = System.Diagnostics.Stopwatch.StartNew();

                            try
                            {
                                var vr = mgr.GetData(conn, dn, filter, sort, scope, skip, take, new[] { sort });

                                sw.Stop();
                                Console.WriteLine("# VLV took {0} msec; content estimate = {1}", sw.ElapsedMilliseconds, vr.ContentCount);

                                if (take > 0)
                                {
                                    int i = 1;
                                    foreach (SearchResultEntry se in vr.Entries)
                                    {
                                        Console.WriteLine(" {0,4} {1}", i++, se.DistinguishedName);
                                    }
                                }
                            }
                            catch (DirectoryOperationException de)
                            {
                                Console.WriteLine("Directory exception: {0}, {1}", de.Message, de.Response.ResultCode);

                                if (de.Data != null)
                                    foreach (var key in de.Data.Keys)
                                        Console.WriteLine("  Data: {0} = {1}", key, de.Data[key]);

                            }

                            Console.WriteLine("");
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("{0}, {1} :: {2}", e.GetType(), e.Message, e.StackTrace);
            }
        
        }
    }
}
