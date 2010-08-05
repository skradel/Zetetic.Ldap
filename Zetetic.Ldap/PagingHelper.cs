using System;
using System.Collections.Generic;
using System.Text;
using NLog;
using System.DirectoryServices.Protocols;

namespace Zetetic.Ldap
{
    public class PagingHelper : IDisposable
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        public string Filter { get; set; }
        public int PageSize { get; set; }
        public string DistinguishedName { get; set; }
        public string[] Attrs { get; set; }
        public int MaxPages { get; set; }
        public LdapConnection Connection { get; set; }

        public SearchScope SearchScope { get; set; }
        public int SizeLimit { get; set; }
        public TimeSpan MaxSearchTimePerPage { get; set; }

        protected readonly IList<DirectoryControl> UserControls = new List<DirectoryControl>();

        public PagingHelper()
        {
            this.PageSize = 1000;
            this.MaxPages = 1;
            this.SearchScope = SearchScope.Subtree;
            this.MaxSearchTimePerPage = TimeSpan.FromSeconds(12);
        }

        protected virtual void OnSearchResponse(string key, SearchResponse resp)
        {
        }

        protected virtual SearchResponse GetSearchResponse(string key, SearchRequest req)
        {
            var resp = (SearchResponse)this.Connection.SendRequest(req);

            this.OnSearchResponse(key, resp);

            return resp;
        }

        protected PageResultRequestControl UpdatePrc(SearchResponse resp)
        {
            if (this.PageSize < 1)
                return null;

            foreach (DirectoryControl dc in resp.Controls)
            {
                PageResultResponseControl c = dc as PageResultResponseControl;

                if (c != null)
                {
                    if (c.Cookie != null && c.Cookie.Length > 0)
                    {
                        PageResultRequestControl prc = new PageResultRequestControl();
                        prc.Cookie = c.Cookie;
                        prc.PageSize = this.PageSize;
                        prc.IsCritical = true;
                        return prc;
                    }
                }
            }
            return null;
        }

        public void AddControl(DirectoryControl control)
        {
            this.UserControls.Add(control);
        }

        /// <summary>
        /// Lazy-loading pure IEnumerable with transparent paging
        /// </summary>
        /// <returns></returns>
        public virtual IEnumerable<SearchResultEntry> GetResults()
        {
            SearchRequest req = new SearchRequest();
            req.DistinguishedName = this.DistinguishedName;
            req.Filter = this.Filter;
            req.Scope = this.SearchScope;

            if (this.MaxSearchTimePerPage.TotalSeconds > 0)
                req.TimeLimit = this.MaxSearchTimePerPage;

            if (this.SizeLimit > 0)
                req.SizeLimit = this.SizeLimit;

            string alist = "";
            if (this.Attrs != null && this.Attrs.Length > 0)
            {
                alist = string.Join("!", this.Attrs);
                req.Attributes.AddRange(this.Attrs);
            }

            PageResultRequestControl prc = new PageResultRequestControl(this.PageSize);
            prc.IsCritical = false;

            logger.Trace("Initial pg of {0}, max pages {1}", this.PageSize, this.MaxPages);

            int currentPage = 0;

            while (prc != null && (currentPage++ < this.MaxPages || this.MaxPages < 1))
            {
                if (this.PageSize > 0)
                {
                    if (currentPage > 1)
                        req.Controls.Clear();

                    req.Controls.Add(prc);
                }
                else
                    logger.Trace("Unpaged search");

                foreach (var dc in this.UserControls)
                    req.Controls.Add(dc);

                string key = this.DistinguishedName + ":" + this.SearchScope.ToString()
                    + ":" + this.Filter + "," + currentPage + "," + this.PageSize + "," + alist;

                SearchResponse resp;

                try
                {
                    resp = this.GetSearchResponse(key, req);
                    logger.Debug("{0} total results", resp.Entries.Count);
                }
                catch (LdapException lde)
                {
                    logger.Error("Ldap server msg {0}, code {1}, ex msg {2}",
                        lde.ServerErrorMessage, lde.ErrorCode, lde.Message);

                    throw;
                }
                catch (DirectoryOperationException doe)
                {
                    if (doe.Response.ResultCode == ResultCode.SizeLimitExceeded && this.SizeLimit > 0)
                    {
                        logger.Info("Keeping SizeLimitExceeded results, count: {0}", this.SizeLimit);

                        resp = (SearchResponse)doe.Response;
                    }
                    else
                    {
                        logger.Error("Operation exception; rc {0}, msg {1}",
                            doe.Response.ResultCode, doe.Response.ErrorMessage);

                        throw;
                    }
                }

                foreach (SearchResultEntry se in resp.Entries)
                    yield return se;

                prc = UpdatePrc(resp);
            }
            yield break;
        }

        #region IDisposable Members

        public void Dispose()
        {
            this.Connection = null;
        }

        #endregion
    }
}
