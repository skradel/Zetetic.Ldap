using System;
using System.Collections.Generic;
using System.Text;
using System.DirectoryServices.Protocols;

namespace Zetetic.Ldap
{
    public class MutableEntryCollection : IEnumerable<MutableEntry>
    {
        private List<MutableEntry> _results = new List<MutableEntry>();


        public MutableEntryCollection(SearchResultEntryCollection results)
        {
            foreach (SearchResultEntry se in results)
                _results.Add(new MutableEntry(se));
        }

        public MutableEntry this[int index]
        {
            get
            {
                return _results[index];
            }
        }

        public int Count
        {
            get
            {
                return _results.Count;
            }
        }

        #region IEnumerable<MutableSearchResultEntry> Members

        public IEnumerator<MutableEntry> GetEnumerator()
        {
            foreach (MutableEntry se in _results)
                yield return se;
        }

        #endregion

        #region IEnumerable Members

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return _results.GetEnumerator();
        }

        #endregion
    }
}
