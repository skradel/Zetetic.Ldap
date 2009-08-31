using System;
using System.Collections.Generic;
using System.Text;

namespace Zetetic.Ldap
{
    

    public class LdifEntryReader : IDisposable
    {
        private LdifReader _ldif;
        protected Entry _work;
        protected bool _complete;

        public LdifEntryReader(LdifReader ldif)
        {
            _ldif = ldif;
            _ldif.OnBeginEntry += _ldif_OnBeginEntry;
            _ldif.OnAttributeValue += _ldif_OnAttributeValue;
            _ldif.OnEndEntry += _ldif_OnEndEntry;
        }

        void _ldif_OnBeginEntry(object sender, DnEventArgs e)
        {
            _complete = false;
            _work = new Entry(e.DistinguishedName);
        }

        void _ldif_OnEndEntry(object sender, DnEventArgs e)
        {
            _complete = true;
        }

        void _ldif_OnAttributeValue(object sender, AttributeEventArgs e)
        {
            _work.AddAttrValue(e.Name, e.Value);
        }

        public Entry ReadEntry()
        {
            while (_ldif.Read())
            {
                if (_complete)
                {
                    _complete = false;
                    return _work;
                }
            }

            return null;
        }

        #region IDisposable Members

        public void Dispose()
        {
            if (_ldif != null)
            {
                _ldif.OnBeginEntry -= _ldif_OnBeginEntry;
                _ldif.OnAttributeValue -= _ldif_OnAttributeValue;
                _ldif.OnEndEntry -= _ldif_OnEndEntry;
                _ldif = null;
            }
        }

        #endregion

        ~LdifEntryReader()
        {
            Dispose();
        }
    }
}
