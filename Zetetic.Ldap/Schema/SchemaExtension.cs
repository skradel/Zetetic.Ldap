using System;
using System.Collections.Generic;
using System.Text;
using System.Xml.Serialization;

namespace Zetetic.Ldap.Schema
{
    [XmlRoot("schemaExtension")]
    public class SchemaExtension
    {
        [XmlElement("add")]
        public List<SchemaExtensionEntry> Entries;
    }

    public struct SchemaExtensionEntry
    {
        [XmlAttribute("oid")]
        public string OID;

        [XmlAttribute("syntax")]
        public AttrLangType Syntax;
    }
}
