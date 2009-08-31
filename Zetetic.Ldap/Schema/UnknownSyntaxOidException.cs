using System;
using System.Collections.Generic;
using System.Text;

namespace Zetetic.Ldap.Schema
{
    public class UnknownSyntaxOidException : Exception
    {
        public string OID { get; private set; }

        public UnknownSyntaxOidException(string message, string oid)
            : base(message)
        {
            this.OID = oid;
        }
    }
}
