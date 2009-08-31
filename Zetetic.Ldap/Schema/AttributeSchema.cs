using System;
using System.Collections.Generic;
using System.Text;

namespace Zetetic.Ldap.Schema
{
    public class AttributeSchema
    {
        public bool IsMultiValued { get; internal set; }
        public string DisplayName { get; internal set; }
        public string OID { get; internal set; }
        public AttrLangType LangType { get; internal set; }

        public override string ToString()
        {
            return "Attribute=" + this.DisplayName + "; OID=" + this.OID + "; Multivalued=" + this.IsMultiValued;
        }
    }
}
