using System;
using System.Collections.Generic;
using System.Text;

namespace Zetetic.Ldap.Schema
{
    public enum AttrLangType
    {
        DirectoryString, CaseInsensitiveString, OID, NtSecurityDescriptor,
        NtSID, Boolean, DN, DNBinary, GeneralizedTime, PrintableString, NumericString,
        OctetString, Int32, Int64
    };
}
