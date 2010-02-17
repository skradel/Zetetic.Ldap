using System;
using System.Collections.Generic;
using System.Text;
using System.DirectoryServices.Protocols;
using System.Xml.Serialization;

namespace Zetetic.Ldap
{
    [XmlRoot("entry", Namespace="http://zetetic.net/schema/Zetetic.Ldap.Entry")]
    public class Entry
    {
        [XmlAttribute("dn")]
        public string DistinguishedName { get; protected set; }

        [XmlIgnore]
        public string OriginalDn { get; protected set; }

        [XmlIgnore]
        public bool IsDnDirty { get; protected set; }

        [XmlIgnore]
        public bool IsSuperiorDirty { get; protected set; }

        [XmlElement("attribute")]
        protected readonly Dictionary<string, Attr> Attrs = new Dictionary<string, Attr>();

        public Entry(string distinguishedName)
        {
            this.DistinguishedName = distinguishedName;
            this.OriginalDn = distinguishedName;
        }

        public Entry(SearchResultEntry se)
        {
            this.ReloadFromEntry(se);
        }

        public void ReloadFromEntry(SearchResultEntry se)
        {
            this.DistinguishedName = se.DistinguishedName;
            this.OriginalDn = se.DistinguishedName;
            this.IsDnDirty = false;
            this.IsSuperiorDirty = false;

            this.Attrs.Clear();

            foreach (string s in se.Attributes.AttributeNames)
            {
                this.Attrs.Add(s.ToLowerInvariant(),
                    new Attr(s.ToLowerInvariant(),
                        se.Attributes[s].GetValues(typeof(string))));
            }
        }

        /// <summary>
        /// Extract or set the RDN part of the current Entry's DistinguishedName; no built-in escaping of new RDN.
        /// </summary>
        [XmlAttribute("rdn")]
        public string RDN
        {
            get
            {
                if (string.IsNullOrEmpty(this.DistinguishedName))
                    return null;

                return ParseRdn(this.DistinguishedName);
            }
            set
            {
                this.ModifyRdn(value, false);
            }
        }

        [XmlIgnore]
        public string SuperiorDn
        {
            get
            {
                string rdn = this.RDN;

                if (rdn.Length >= this.DistinguishedName.Length)
                    return null;

                return this.DistinguishedName.Substring(1 + rdn.Length).Trim();
            }
            set
            {
                string rdn = this.RDN;

                if (rdn.Length < this.DistinguishedName.Length)
                {
                    if (!this.DistinguishedName.Substring(rdn.Length + 1).Equals(value, StringComparison.InvariantCultureIgnoreCase))
                    {
                        this.DistinguishedName = rdn + "," + value;
                        this.IsSuperiorDirty = true;
                        this.IsDnDirty = true;
                    }
                }
                else
                {
                    throw new InvalidOperationException("Cannot change superior of " + this.DistinguishedName);
                }
            }
        }

        /// <summary>
        /// Write the entry out for inspection.  This is a convenience method, and is not
        /// guaranteed to follow LDIF rules (like 7-bit encoding, line folding, etc.).
        /// </summary>
        /// <param name="tw"></param>
        public virtual void Dump(System.IO.TextWriter tw)
        {
            tw.WriteLine("dn: {0}", this.DistinguishedName);

            foreach (string a in this.Attrs.Keys)
                foreach (object o in this.Attrs[a].Value)
                    tw.WriteLine("{0}: {1}", a, o);
        }

        /// <summary>
        /// Append a value to local attribute storage information without invoking change-tracking
        /// </summary>
        /// <param name="attrName"></param>
        /// <param name="attrValue"></param>
        protected internal void AddAttrValue(string attrName, object attrValue)
        {
            if (string.IsNullOrEmpty(attrName))
                throw new ArgumentNullException("attrName");

            string key = attrName.ToLowerInvariant();

            if (this.Attrs.ContainsKey(key))
            {
                this.Attrs[key].Value.Add(attrValue);
            }
            else
            {
                this.Attrs[key] = new Attr(key, new List<object>() { attrValue });
            }
        }

        /// <summary>
        /// Append to local attribute storage information without invoking change-tracking.
        /// </summary>
        /// <param name="attrName"></param>
        /// <param name="attrValue"></param>
        protected internal void AddAttr(Attr attr)
        {
            if (attr == null)
                throw new ArgumentNullException("attr");

            this.Attrs[attr.Name.ToLowerInvariant()] = attr;
        }

        /// <summary>
        /// Count the number of values associated to the named attribute.
        /// </summary>
        /// <param name="propName"></param>
        /// <returns></returns>
        public int GetAttrValueCount(string propName)
        {
            if (this.Attrs.ContainsKey(propName.ToLowerInvariant()))
            {
                return this.Attrs[propName.ToLowerInvariant()].Value.Count;
            }
            return 0;
        }

        /// <summary>
        /// Convert each element in the attribute's values to a string in a new array; or,
        /// null if the attribute is not present.
        /// </summary>
        /// <param name="attrName"></param>
        /// <returns></returns>
        public string[] GetAttrStringValues(string attrName)
        {
            if (this.HasAttribute(attrName))
            {
                object[] v = this.Attrs[attrName.ToLowerInvariant()].Value.ToArray();
                string[] s = new string[v.Length];

                for (int i = 0; i < v.Length; i++)
                    s[i] = Convert.ToString(v[i]);

                return s;
            }
            return null;
        }

        /// <summary>
        /// Check that the named attribute 
        /// </summary>
        /// <param name="attrName"></param>
        /// <returns></returns>
        public virtual bool HasAttribute(string attrName)
        {
            return this.GetAttrValueCount(attrName) > 0;
        }

        /// <summary>
        /// Extract the RDN component from a distinguished name.  This will include the attribute name, e.g., "cn=xyz".
        /// This will also include any escaping backslashes, e.g., "cn=xyz\, user".
        /// </summary>
        /// <param name="dn"></param>
        /// <returns></returns>
        public static string ParseRdn(string dn)
        {
            string rdn = "";

            bool isEscape = false;
            foreach (char c in dn)
            {
                if (c == '\\')
                {
                    isEscape = !isEscape;
                }
                else
                {
                    if (c == ',' && !isEscape)
                        break;

                    isEscape = false;
                }

                rdn += c;
            }

            return rdn;
        }

        /// <summary>
        /// Utility method to backslash-escape a DN naming component; e.g., "cn=xyz, user" becomes "cn=xyz\, user".
        /// </summary>
        /// <param name="namingComponent"></param>
        /// <returns></returns>
        public static string EscapeNamingComponent(string namingComponent)
        {
            string strOut = "";
            string spChars = ",+\"\\<>;";

            foreach (char c in namingComponent)
            {
                if (spChars.IndexOf(c) > -1)
                    strOut += "\\";

                strOut += c;
            }
            return strOut;
        }

        /// <summary>
        /// Update the Entry's DistinguishedName with a new RDN.  This utility method accepts an extra argument
        /// to escape the RDN.
        /// </summary>
        /// <param name="newRdn"></param>
        /// <param name="escape"></param>
        public virtual void ModifyRdn(string newRdn, bool escape)
        {
            if (escape) newRdn = EscapeNamingComponent(newRdn);

            this.ModifyRdn(newRdn);
        }

        /// <summary>
        /// Update the Entry's DistinguishedName with a new RDN; no built-in escaping of newRdn.
        /// </summary>
        /// <param name="newRdn"></param>
        public virtual void ModifyRdn(string newRdn)
        {
            int currentRdnLength = ParseRdn(this.DistinguishedName).Length;

            string dn = newRdn + this.DistinguishedName.Substring(currentRdnLength);

            if (!dn.Equals(this.DistinguishedName))
            {
                this.DistinguishedName = dn;
                this.IsDnDirty = true;
            }
        }
    }
}
