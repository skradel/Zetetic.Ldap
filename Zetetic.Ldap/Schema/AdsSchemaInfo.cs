using System;
using System.Collections.Generic;
using System.Text;
using System.Security.Principal;
using System.DirectoryServices.Protocols;
using NLog;
using System.Security.AccessControl;
using System.Collections.ObjectModel;

namespace Zetetic.Ldap.Schema
{
    public class AdsSchemaInfo : SchemaInfoBase
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        public AdsSchemaInfo() : base() { }

        #region ISchemaInfo Members

        protected override AttrLangType InferType(string syntaxOid)
        {
            switch (syntaxOid)
            {
                case "2.5.5.17":
                    return AttrLangType.NtSID;

                case "2.5.5.15":
                    return AttrLangType.NtSecurityDescriptor;

                default:
                    return base.InferType(syntaxOid);
            }
        }

        public override ReadOnlyCollection<object> GetValuesAsLanguageType(SearchResultEntry se, string attrName)
        {
            AttributeSchema aSchema = this.GetAttribute(attrName);
            if (aSchema == null)
                throw new ApplicationException("Unknown attribute " + attrName);

            List<object> results = new List<object>();

            switch (attrName.ToLower())
            {
                case "pwdlastset":
                case "accountexpires":
                case "badpasswordtime":
                case "lockouttime":
                case "lastlogontimestamp":
                case "lastlogon":
                    {
                        foreach (string s in se.Attributes[attrName].GetValues(typeof(string)))
                        {
                            Int64 n = Int64.Parse(s);

                            if (n == 0 || n == 0x7FFFFFFFFFFFFFFF)
                                results.Add("NEVER");
                            else
                                try
                                {
                                    results.Add(DateTime.FromFileTimeUtc(n));
                                }
                                catch (Exception ex)
                                {
                                    logger.Warn("Couldn't convert {0} to DateTime: {1}", s, ex.Message);
                                }
                        }
                    }
                    break;

                case "objectguid":
                    {
                        foreach (byte[] bytes in se.Attributes[attrName].GetValues(typeof(byte[])))
                            results.Add(new Guid(bytes));
                    }
                    break;

                default:
                    switch (aSchema.LangType)
                    {
                        case AttrLangType.NtSID:
                            {
                                foreach (byte[] bytes in se.Attributes[attrName].GetValues(typeof(byte[])))
                                    results.Add(new SecurityIdentifier(bytes, 0));
                            }
                            break;

                        case AttrLangType.NtSecurityDescriptor:
                            {
                                foreach (byte[] bytes in se.Attributes[attrName].GetValues(typeof(byte[])))
                                    results.Add(new RawSecurityDescriptor(bytes, 0));
                            }
                            break;

                        default:
                            return base.GetValuesAsLanguageType(se, attrName);
                    }
                    break;
            }

            return new System.Collections.ObjectModel.ReadOnlyCollection<object>(results);
        }

        public override void Initialize(LdapConnection conn)
        {
            SearchRequest req = new SearchRequest("", (string)null, SearchScope.Base, "schemaNamingContext");
            SearchResponse resp = (SearchResponse)conn.SendRequest(req);

            string schemaNcDn = StringOrNull(resp.Entries[0], "schemaNamingContext");

            //
            // Find the attributes
            //
            string[] wantedAttrs = new string[]{
                "lDAPDisplayName",
                "isSingleValued",
                "attributeSyntax",
                "attributeID",
                "searchFlags"
            };

            PagingHelper helper = new PagingHelper()
            {
                MaxPages = 0,
                Connection = conn,
                Attrs = wantedAttrs,
                Filter = "(&(objectClass=attributeSchema))",
                DistinguishedName = schemaNcDn
            };

            
            foreach (SearchResultEntry se in helper.GetResults())
            {
                string attrName = StringOrNull(se, wantedAttrs[0]);

                try
                {
                    _attrs.Add(attrName.ToLower(), new AttributeSchema()
                    {
                        DisplayName = attrName,
                        IsMultiValued = !"TRUE".Equals(StringOrNull(se, wantedAttrs[1])),
                        LangType = this.InferType(StringOrNull(se, wantedAttrs[2])),
                        OID = StringOrNull(se, wantedAttrs[3]),
                        SearchFlags = Convert.ToInt32(StringOrNull(se, wantedAttrs[4]) ?? "0")
                    });
                }
                catch (Exception ex)
                {
                    logger.ErrorException("Couldn't add attr '" + attrName
                        + "' from " + se.DistinguishedName + " to collection: " + ex.Message, ex);
                }
            }

            //
            // Find the objectclasses
            //
            wantedAttrs = new string[]{
                "lDAPDisplayName",
                "mustContain",
                "systemMustContain",
                "mayContain",
                "systemMayContain",
                "objectClassCategory",
                "subClassOf",
                "governsID"
            };

            helper.Attrs = wantedAttrs;
            helper.Filter = "(&(objectClass=classSchema))";

            foreach (SearchResultEntry se in helper.GetResults())
            {
                ObjectClassSchema oc = new ObjectClassSchema();
                oc.DisplayName = StringOrNull(se, wantedAttrs[0]);
                oc.SuperiorClassName = StringOrNull(se, wantedAttrs[6]);
                oc.OID = StringOrNull(se, wantedAttrs[7]);

                foreach (string src in new string[] { wantedAttrs[1], wantedAttrs[2] })
                {
                    if (se.Attributes.Contains(src))
                        foreach (string s in se.Attributes[src].GetValues(typeof(string)))
                        {
                            oc.AddMandatory(_attrs[s.ToLower()]);
                        }
                }

                foreach (string src in new string[] { wantedAttrs[3], wantedAttrs[4] })
                {
                    if (se.Attributes.Contains(src))
                        foreach (string s in se.Attributes[src].GetValues(typeof(string)))
                        {
                            oc.AddOptional(_attrs[s.ToLower()]);
                        }
                }

                switch (StringOrNull(se, wantedAttrs[5]))
                {
                    case "1":
                        oc.ClassType = ObjectClassType.Structural;
                        break;

                    case "2":
                        oc.ClassType = ObjectClassType.Abstract;
                        break;

                    case "3":
                        oc.ClassType = ObjectClassType.Auxiliary;
                        break;

                    default:
                        oc.ClassType = ObjectClassType.Unknown;
                        break;
                }

                try
                {
                    this._ocs.Add(oc.DisplayName.ToLower(), oc);
                }
                catch (Exception ex)
                {
                    logger.ErrorException("Couldn't add OC '" + oc.DisplayName 
                        + "' from " + se.DistinguishedName + " to collection: " + ex.Message, ex);
                }
            } 
        }

        #endregion
    }
}
