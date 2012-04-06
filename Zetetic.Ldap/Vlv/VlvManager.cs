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
            string key = string.Format("{0},{1},{2}", distinguishedName, filter, scope);

            int contentEstimate;
            var known = _estimates.TryGetValue(key, out contentEstimate);

            if (take < 1 && known)
            {
                return new VlvData
                {
                    ContentCount = contentEstimate
                };
            }

            var req = new SearchRequest(distinguishedName, filter, scope, attrs);

            req.Controls.Add(new SortRequestControl(sortBy, false));

            if (take < 1)
                take = 1;

            var vlvreq = new VlvRequestControl
            {
                EstimateCount = contentEstimate,
                BeforeCount = 0,
                AfterCount = take - 1,
                Offset = skip < 1 ? 1 : skip + 1
            };

            req.Controls.Add(vlvreq);

            SearchResponse resp = (SearchResponse)conn.SendRequest(req);

            VlvResponseControl vlvresp = null;
            
            foreach (var control in resp.Controls) 
            {
                vlvresp = control as VlvResponseControl;

                if (vlvreq != null) 
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
