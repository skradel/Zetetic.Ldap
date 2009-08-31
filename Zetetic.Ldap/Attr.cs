using System;
using System.Collections.Generic;
using System.Text;
using System.Xml.Serialization;

namespace Zetetic.Ldap
{
    public class Attr
    {
        [XmlAttribute("name")]
        public string Name { get; set; }

        [XmlElement("value")]
        public List<object> Value { get; set; }

        [XmlAttribute("type")]
        public Zetetic.Ldap.Schema.AttrLangType AttrType { get; protected internal set; }

        public Attr() { }

        public Attr(string name, object[] value)
        {
            this.Name = name;
            this.Value = new List<object>(value);
        }

        public Attr(string name, List<object> value)
        {
            this.Name = name;
            this.Value = value;
        }
    }
}
