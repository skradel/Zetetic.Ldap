using System;
using System.Collections.Generic;
using System.Text;
using System.DirectoryServices.Protocols;
using System.Collections.ObjectModel;

namespace Zetetic.Ldap.Schema
{
    public abstract class SchemaInfoBase : ISchemaInfo
    {
        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        public abstract void Initialize(LdapConnection conn);

        protected Dictionary<string, AttributeSchema> _attrs = new Dictionary<string, AttributeSchema>();
        protected Dictionary<string, ObjectClassSchema> _ocs = new Dictionary<string, ObjectClassSchema>();
        protected Dictionary<string, AttrLangType> _schemaExtensions = new Dictionary<string, AttrLangType>();

        protected static string StringOrNull(SearchResultEntry se, string attrName)
        {
            if (se.Attributes.Contains(attrName))
            {
                return ((string[])se.Attributes[attrName].GetValues(typeof(string)))[0];
            }
            return null;
        }

        /// <summary>
        /// Supplement the schema reader's knowledge of attribute-OID-to-usable-type mappings from an XML file;
        /// see the serialization hints in SchemaExtension.cs
        /// </summary>
        /// <param name="path"></param>
        public virtual void LoadExtension(string path)
        {
            SchemaExtension se;

            System.Xml.Serialization.XmlSerializer ser = new System.Xml.Serialization.XmlSerializer(typeof(SchemaExtension));
            using (System.IO.StreamReader sr = new System.IO.StreamReader(path))
            {
                se = (SchemaExtension)ser.Deserialize(sr);
            }

            foreach (SchemaExtensionEntry e in se.Entries)
            {
                _schemaExtensions[e.OID] = e.Syntax;
            }
        }

        protected virtual AttrLangType InferType(string syntaxOid)
        {
            if (string.IsNullOrEmpty(syntaxOid))
                throw new ArgumentException("Cannot interpret empty OID", "syntaxOid");

            switch (syntaxOid)
            {
                case "2.5.5.3":
                case "2.5.5.12":
                    return AttrLangType.DirectoryString;

                case "2.5.5.16":
                    return AttrLangType.Int64;

                case "2.5.5.1":
                    return AttrLangType.DN;

                case "2.5.5.10":
                    return AttrLangType.OctetString;

                case "2.5.5.5":
                    return AttrLangType.PrintableString;

                case "2.5.5.6":
                    return AttrLangType.NumericString;

                case "2.5.5.11":
                    return AttrLangType.GeneralizedTime;

                case "2.5.5.9":
                    return AttrLangType.Int32;

                case "2.5.5.7":
                    return AttrLangType.DNBinary;

                case "2.5.5.2":
                    return AttrLangType.OID;

                case "2.5.5.8":
                    return AttrLangType.Boolean;

                case "2.5.5.4":
                    return AttrLangType.CaseInsensitiveString;

                default:
                    if (_schemaExtensions.ContainsKey(syntaxOid))
                    {
                        return _schemaExtensions[syntaxOid];
                    }

                    logger.Warn("Unknown syntax OID {0}; treating as string", syntaxOid);
                    return AttrLangType.DirectoryString;
            }
        }



        public IEnumerable<AttributeSchema> Attributes
        {
            get { foreach (AttributeSchema a in _attrs.Values) yield return a; }
        }

        public IEnumerable<ObjectClassSchema> ObjectClasses
        {
            get { foreach (ObjectClassSchema o in _ocs.Values) yield return o; }
        }

        public AttributeSchema GetAttribute(string attrName)
        {
            if (string.IsNullOrEmpty(attrName))
                throw new ArgumentException("Requested attribute cannot be null or empty", "attrName");

            if (_attrs.ContainsKey(attrName.ToLower()))
                return _attrs[attrName.ToLower()];

            return null;
        }

        public ObjectClassSchema GetObjectClass(string ocName)
        {
            if (string.IsNullOrEmpty(ocName))
                throw new ArgumentException("Requested objectClass cannot be null or empty", "attrName");

            if (_ocs.ContainsKey(ocName.ToLower()))
                return _ocs[ocName.ToLower()];

            // Second chance - find by OID
            foreach (ObjectClassSchema oc in this.ObjectClasses)
                if (oc.OID == ocName)
                    return oc;

            return null;
        }

        public virtual ReadOnlyCollection<object> GetValuesAsLanguageType(SearchResultEntry se, string attrName)
        {
            AttributeSchema aSchema = this.GetAttribute(attrName);
            if (aSchema == null)
                throw new ApplicationException("Unknown attribute " + attrName);

            List<object> results = new List<object>();

            if (se.Attributes.Contains(attrName))
            {
                switch (aSchema.LangType)
                {
                    case AttrLangType.GeneralizedTime:
                        foreach (string s in se.Attributes[attrName].GetValues(typeof(string)))
                        {
                            // 20090203065703.0Z
                            string t = s.Replace(".0Z", "");

                            DateTime dt = DateTime.ParseExact(t, 
                                new string[] { "yyyyMMddHHmmss"}, 
                                null, System.Globalization.DateTimeStyles.AssumeUniversal);

                            results.Add(dt);
                        }
                        break;

                    case AttrLangType.Boolean:
                        {
                            foreach (string s in se.Attributes[attrName].GetValues(typeof(string)))
                                results.Add(bool.Parse(s));
                        }
                        break;

                    case AttrLangType.Int32:
                        {
                            foreach (string s in se.Attributes[attrName].GetValues(typeof(string)))
                                results.Add(Int32.Parse(s));
                        }
                        break;

                    case AttrLangType.Int64:
                        {
                            foreach (string s in se.Attributes[attrName].GetValues(typeof(string)))
                                results.Add(Int64.Parse(s));
                        }
                        break;

                    case AttrLangType.OctetString:
                        results.AddRange(se.Attributes[attrName].GetValues(typeof(byte[])));
                        break;

                    case AttrLangType.DirectoryString:
                    case AttrLangType.CaseInsensitiveString:
                    case AttrLangType.DN:
                    default:
                        results.AddRange(se.Attributes[attrName].GetValues(typeof(string)));
                        break;
                }
            }

            return new System.Collections.ObjectModel.ReadOnlyCollection<object>(results);
        }
    }
}
