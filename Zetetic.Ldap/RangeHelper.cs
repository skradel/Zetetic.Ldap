using System;
using System.Collections.Generic;
using System.Text;
using System.DirectoryServices.Protocols;

namespace Zetetic.Ldap
{
    /// <summary>
    /// A simple helper to make it easier to get all the values of a large multivalued attribute from
    /// Active Directory.
    /// </summary>
    public class RangeHelper
    {
        /// <summary>
        /// Typical usage:
        /// foreach (string s in RangeHelper.StringValues(conn, "cn=test", "member", 0, null, false))
        ///  ....
        /// 
        /// </summary>
        /// <param name="conn"></param>
        /// <param name="entryDn"></param>
        /// <param name="attrName"></param>
        /// <param name="start"></param>
        /// <param name="end"></param>
        /// <returns></returns>
        public static IEnumerable<string> StringValues(LdapConnection conn, string entryDn, string attrName, int start, int? end, bool extendedDns)
        {
            int requested = 0, returned = 0;
            if (end != null)
                requested = end.Value - start;

            RangeResult r = GetRangeBlock(conn, entryDn, attrName, start, end, extendedDns);
            while (r != null)
            {
                foreach (string s in r.Values)
                {
                    if (requested > 0 && ++returned >= requested)
                        yield break;

                    yield return s;
                }

                if (r.IsFinal)
                    yield break;
                else
                    r = GetRangeBlock(conn, entryDn, attrName, r.End + 1, end, extendedDns);
            }

            yield break;
        }

        private static RangeResult GetRangeBlock(LdapConnection conn, string entryDn, string attrName, int start, int? end, bool extendedDns)
        {
            SearchRequest req = new SearchRequest();
            req.DistinguishedName = entryDn;
            req.Scope = SearchScope.Base;
            req.Filter = "(&(objectClass=*))";
            req.Attributes.Add(attrName + ";range=" + start + "-" + (end == null ? "*" : end.ToString()));

            if (extendedDns)
                req.Controls.Add(new ExtendedDNControl(ExtendedDNFlag.StandardString));

            SearchResponse resp = (SearchResponse)conn.SendRequest(req);
            if (resp.Entries.Count == 0)
                return null;

            SearchResultEntry e = resp.Entries[0];

            foreach (string s in e.Attributes.AttributeNames)
                if (s.StartsWith(attrName, StringComparison.InvariantCultureIgnoreCase))
                {
                    RangeResult res = new RangeResult();
                    DirectoryAttribute attr = e.Attributes[s];

                    res.Values = (string[])attr.GetValues(typeof(string));

                    if (s.EndsWith("*"))
                        res.IsFinal = true;

                    int pos = s.IndexOf('=');
                    int hyp = s.IndexOf('-', pos + 1);

                    res.Start = int.Parse(s.Substring(pos + 1, hyp - pos - 1));

                    if (!res.IsFinal)
                        res.End = int.Parse(s.Substring(hyp + 1));

                    return res;
                }

            return null;
        }
    }
}
