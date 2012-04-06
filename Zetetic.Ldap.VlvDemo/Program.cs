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
                string server = null, dn = null;

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
                            var vr = mgr.GetData(conn, dn, filter, sort, scope, skip, take, new[]{sort});

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
            catch (Exception e)
            {
                Console.WriteLine("{0}, {1} :: {2}", e.GetType(), e.Message, e.StackTrace);
            }
        
        }
    }
}
