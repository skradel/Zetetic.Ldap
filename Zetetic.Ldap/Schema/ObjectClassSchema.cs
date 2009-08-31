using System;
using System.Collections.Generic;
using System.Text;

namespace Zetetic.Ldap.Schema
{
    public enum ObjectClassType { Structural, Auxiliary, Abstract, Unknown };

    public class ObjectClassSchema
    {
        protected List<AttributeSchema> _must = new List<AttributeSchema>(),
            _may = new List<AttributeSchema>();

        public string SuperiorClassName { get; set; }

        public string OID { get; internal set; }

        public string DisplayName { get; internal set; }

        public ObjectClassType ClassType { get; internal set; }

        public IEnumerable<AttributeSchema> MustHave
        {
            get { foreach (AttributeSchema a in _must) yield return a; }
        }

        public IEnumerable<AttributeSchema> MayHave
        {
            get { foreach (AttributeSchema a in _may) yield return a; }
        }

        internal void AddMandatory(AttributeSchema a) { _must.Add(a); }
        internal void AddOptional(AttributeSchema a) { _may.Add(a); }

        public override string ToString()
        {
            return "ObjectClass=" + this.DisplayName + ", OID=" + this.OID 
                + "; Superior=" + this.SuperiorClassName + "; ClassType=" + this.ClassType;
        }
    }
}
