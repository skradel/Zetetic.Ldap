using System;
using System.Collections.Generic;
using System.Text;
using NLog;
using System.DirectoryServices.Protocols;
using System.Threading;

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

        public bool IsSizeLimitExceeded { get; protected set; }

        private bool _abort;
        private readonly System.Threading.ManualResetEvent _abortHandle = new System.Threading.ManualResetEvent(false);

        protected readonly IList<DirectoryControl> UserControls = new List<DirectoryControl>();

        public PagingHelper()
        {
            this.PageSize = 1000;
            this.MaxPages = 1;
            this.SearchScope = SearchScope.Subtree;
            this.MaxSearchTimePerPage = TimeSpan.FromSeconds(12);
        }

        /// <summary>
        /// Allow a superclass to cache or otherwise inspect the response
        /// </summary>
        /// <param name="key"></param>
        /// <param name="resp"></param>
        protected virtual void OnSearchResponse(string key, SearchResponse resp)
        {
        }

        protected virtual SearchResponse ExtractResponseFromException(DirectoryOperationException doe)
        {
            if (doe.Response.ResultCode == ResultCode.SizeLimitExceeded && this.SizeLimit > 0)
            {
                logger.Info("Keeping SizeLimitExceeded results, count: {0}", this.SizeLimit);

                this.IsSizeLimitExceeded = true;

                return (SearchResponse)doe.Response;
            }
            else
            {
                logger.Error("Operation exception; rc {0}, msg {1}",
                    doe.Response.ResultCode, doe.Response.ErrorMessage);

                return null;
            }
        }

        public void Abort()
        {
            if (!_abort)
            {
                _abort = true;
                _abortHandle.Set();
            }
        }

        protected virtual SearchResponse GetSearchResponse(string key, SearchRequest req)
        {
            logger.Debug("Dispatch search to DSA: {0}", key);

            var async = this.Connection.BeginSendRequest(
                req,
                PartialResultProcessing.NoPartialResultSupport,
                null,
                null);

            int finishedFirst = WaitHandle.WaitAny(new WaitHandle[] { _abortHandle, async.AsyncWaitHandle });

            logger.Debug("GetSearchResponse: whnd = {0}", finishedFirst);

            if (finishedFirst == 0)
            {
                this.Connection.Abort(async);
                return null;
            }

            SearchResponse resp = null;

            try
            {
                resp = (SearchResponse)this.Connection.EndSendRequest(async);
            }
            catch (DirectoryOperationException doe)
            {
                resp = ExtractResponseFromException(doe);
            }

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

                if (c != null && c.Cookie != null && c.Cookie.Length > 0)
                    return new PageResultRequestControl
                    {
                        Cookie = c.Cookie,
                        PageSize = this.PageSize,
                        IsCritical = true
                    };
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
            SearchRequest req = new SearchRequest
            {
                DistinguishedName = this.DistinguishedName,
                Filter = this.Filter,
                Scope = this.SearchScope
            };

            if (this.MaxSearchTimePerPage.TotalSeconds > 0)
                req.TimeLimit = this.MaxSearchTimePerPage;

            if (this.SizeLimit > 0)
                req.SizeLimit = this.SizeLimit;

            string alist = string.Empty;
            if (this.Attrs != null && this.Attrs.Length > 0)
            {
                alist = string.Join("!", this.Attrs);
                req.Attributes.AddRange(this.Attrs);
            }

            PageResultRequestControl prc = new PageResultRequestControl(this.PageSize);
            prc.IsCritical = false;

            logger.Trace("Initial pg of {0}, max pages {1}", this.PageSize, this.MaxPages);

            int currentPage = 0;

            while (!_abort && prc != null && (currentPage++ < this.MaxPages || this.MaxPages < 1))
            {
                if (this.PageSize > 0 && (this.PageSize < this.SizeLimit || this.SizeLimit == 0))
                {
                    if (currentPage > 1)
                        req.Controls.Clear();

                    req.Controls.Add(prc);
                }
                else
                {
                    logger.Trace("Unpaged search (pgsz {0}, sizelimit {1})", this.PageSize, this.SizeLimit);
                }

                foreach (var dc in this.UserControls)
                    req.Controls.Add(dc);

                string key = this.DistinguishedName + ";" + this.SearchScope.ToString()
                    + ";f=" + this.Filter + ";cp=" + currentPage + ";psz=" + this.PageSize
                    + ";szl=" + this.SizeLimit + ";att" + alist;

                SearchResponse resp;

                try
                {
                    resp = this.GetSearchResponse(key, req);

                    if (resp != null)
                        logger.Debug("{0} total results", resp.Entries.Count);
                }
                catch (LdapException lde)
                {
                    if (_abort && lde.ErrorCode == 88)
                    {
                        logger.Info("Canceled by user");
                        yield break;
                    }
                    else
                    {
                        logger.Error("Ldap server msg {0}, code {1}, ex msg {2}",
                            lde.ServerErrorMessage, lde.ErrorCode, lde.Message);
                        throw;
                    }
                }
                // Note that Directory(Operation)Exception is NOT a subclass of LdapException
                // nor vice versa... verified

                if (_abort || resp == null)
                    yield break;

                foreach (SearchResultEntry se in resp.Entries)
                {
                    if (_abort)
                    {
                        logger.Info("Request aborted in enum");
                        yield break;
                    }

                    yield return se;
                }

                prc = UpdatePrc(resp);
            }
        }

        #region IDisposable Members

        public void Dispose()
        {
            this.Connection = null;
        }
        #endregion
    }
}
