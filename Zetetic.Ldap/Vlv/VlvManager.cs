using System;
using System.Collections.Generic;
using System.Text;
using System.DirectoryServices.Protocols;

namespace Zetetic.Ldap.Vlv
{
    public class VlvManager
    {
        private readonly Dictionary<string, int> _estimates = new Dictionary<string, int>();

        public VlvData GetData(LdapConnection conn, string distinguishedName, string filter, string sortBy, SearchScope scope, int skip, int take, string[] attrs)
        {
            string key = string.Join(",", new[]{ distinguishedName, filter, scope.ToString(), sortBy});

            int contentEstimate;

            var known = _estimates.TryGetValue(key, out contentEstimate);

            if (take < 1)
            {
                if (known)
                    return new VlvData
                    {
                        ContentCount = contentEstimate
                    };

                take = 1;
            }

            var req = new SearchRequest(distinguishedName, filter, scope, attrs);

            req.Controls.Add(new SortRequestControl(sortBy, false));

            req.Controls.Add(new VlvRequestControl
            {
                EstimateCount = contentEstimate,
                BeforeCount = 0,
                AfterCount = take - 1,
                Offset = skip < 1 ? 1 : skip + 1
            });

            SearchResponse resp = (SearchResponse)conn.SendRequest(req);

            VlvResponseControl vlvresp = null;

            foreach (var control in resp.Controls)
            {
                if ((vlvresp = control as VlvResponseControl) != null)
                {
                    break;
                }
            }

            if (vlvresp == null)
                throw new ApplicationException("No VlvResponseControl found in response");

            if (!known)
            {
                _estimates[key] = vlvresp.ContentCount;
            }

            return new VlvData
            {
                ContentCount = vlvresp.ContentCount,
                Entries = resp.Entries,
                VlvContextId = vlvresp.ContextId,
                IsDone = vlvresp.TargetPosition + take > vlvresp.ContentCount
            };
        }
    }
}
