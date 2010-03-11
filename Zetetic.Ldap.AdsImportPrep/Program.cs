using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using Zetetic.Ldap;

namespace Zetetic.Ldap
{
    /// <summary>
    /// Reformat an LDIF exported from Active Directory, i.e., with ldifde, for re-importing
    /// by stripping out most of the stuff Active Directory won't accept via LDAP.
    /// </summary>
    public class AdsImportPrep
    {
        public void ReformatLdif(LdifReader r, LdifWriter w, Encoding passwordEncoding, string defaultPwd, List<string> ignoredAttrs,
            bool allowMsExchange, Func<string, bool> ignoreIt)
        {
            if (!allowMsExchange)
            {
                ignoredAttrs.Add("showinaddressbook");
                ignoredAttrs.Add("legacyexchangedn");
                ignoredAttrs.Add("homemta");
                ignoredAttrs.Add("homemdb");
                ignoredAttrs.Add("mailnickname");
                ignoredAttrs.Add("mdbusedefaults");
                ignoredAttrs.Add("publicdelegatesbl");
                ignoredAttrs.Add("protocolsettings");
                ignoredAttrs.Add("publicdelegates");
                ignoredAttrs.Add("deleteditemflags");
                ignoredAttrs.Add("mDBStorageQuota".ToLowerInvariant());
                ignoredAttrs.Add("mDBOverQuotaLimit".ToLowerInvariant());
                ignoredAttrs.Add("garbageCollPeriod".ToLowerInvariant());
                ignoredAttrs.Add("mDBOverHardQuotaLimit".ToLowerInvariant());
                ignoredAttrs.Add("altrecipient");
                ignoredAttrs.Add("deliverandredirect");
                ignoredAttrs.Add("securityprotocol");
                ignoredAttrs.Add("reporttooriginator");
                ignoredAttrs.Add("reporttoowner");
                ignoredAttrs.Add("oOFReplyToOriginator".ToLowerInvariant());
                ignoredAttrs.Add("mapirecipient");
                ignoredAttrs.Add("internetencoding");
                ignoredAttrs.Add("targetaddress");
                ignoredAttrs.Add("altrecipientbl");
            }

            bool ignored = false, hasPw = false;
            int pwdScore = 0;
            string thisDn = null;

            r.OnBeginEntry += (s, a) =>
            {
                thisDn = a.DistinguishedName;

                hasPw = false;

                if (ignoreIt != null && ignoreIt.Invoke(a.DistinguishedName))
                {
                    ignored = true;
                }
                // Ignore domain trusts / special accounts
                // This part could use some rethinking.
                else if (a.DistinguishedName.Contains("$,CN=Users,") || a.DistinguishedName.Contains("krbtgt") || a.DistinguishedName.Contains("ForeignSecurityP"))
                {
                    ignored = true;
                }
                else if (!allowMsExchange && a.DistinguishedName.Contains("Exchange System"))
                {
                    ignored = true;
                }
                else
                {
                    ignored = false;
                    w.BeginEntry(a.DistinguishedName);
                }
            };

            r.OnEndEntry += (s, a) =>
            {
                if (!ignored)
                {
                    if (pwdScore > 1 && !hasPw)

                        w.WriteAttr("unicodePwd", passwordEncoding.GetBytes(
                            string.Format("\"{0}\"", defaultPwd))
                            );

                    w.EndEntry();
                }
                pwdScore = 0;
            };

            r.OnAttributeValue += (s, a) =>
            {
                if (!ignored)
                {

                    if (a.Name == "unicodePwd")
                        hasPw = true;
                    else if (a.Name == "objectCategory" && ((string)a.Value).StartsWith("CN=Person"))
                        pwdScore++;
                    else if (a.Name == "objectClass" && "user".Equals(a.Value))
                        pwdScore++;

                    if (string.Equals(a.Name, "objectSID", StringComparison.InvariantCultureIgnoreCase))
                    {
                        if (thisDn.IndexOf("ForeignSecurity", StringComparison.InvariantCultureIgnoreCase) > 0)
                        {
                            if (a.Value is byte[])
                            {
                                System.Security.Principal.SecurityIdentifier sid = new System.Security.Principal.SecurityIdentifier((byte[])a.Value, 0);

                                w.WriteAttr(a.Name, sid.ToString());
                            }
                            else if (a.Value is string)
                            {
                                w.WriteAttr(a.Name, (string)a.Value);
                            }
                        }
                    }
                    else
                    {
                        if (a.Value != null && !ignoredAttrs.Contains(a.Name.ToLowerInvariant()))
                        {
                            if (allowMsExchange ||
                                (!a.Name.StartsWith("msExch", StringComparison.InvariantCultureIgnoreCase)
                                  && !a.Name.StartsWith("extensionAttribute")))
                            {
                                if (a.Value is string)
                                {
                                    w.WriteAttr(a.Name, (string)a.Value);
                                }
                                else if (a.Value is byte[])
                                {
                                    w.WriteAttr(a.Name, (byte[])a.Value);
                                }
                                else
                                {
                                    Console.Error.WriteLine("Warn: type of {0} is {1}", a.Name, a.Value.GetType());
                                    w.WriteAttr(a.Name, Convert.ToString(a.Value));
                                }
                            }
                        }
                    }
                }
            };

            while (r.Read())
            {
                // Keep reading
            }

            w.Close();
        }

        public void IMain(string[] args)
        {
            // Note default password encoding is UTF-16 LE


            bool msExch = false;
            string fileIn = null, fileOut = null, defaultPwd = "1FakePwd50!", extraIgnores = null;
            Encoding passwordEncoding = new System.Text.UnicodeEncoding(false, false, true), srcenc = Encoding.GetEncoding("iso-8859-1");

            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "-srcenc":
                        srcenc = Encoding.GetEncoding(args[++i]);
                        break;

                    case "-enc":
                        passwordEncoding = Encoding.GetEncoding(args[++i]);
                        break;

                    case "-i":
                        fileIn = args[++i];
                        break;

                    case "-o":
                        fileOut = args[++i];
                        break;

                    case "-exch":
                        msExch = true;
                        break;

                    case "-p":
                    case "-pwd":
                        defaultPwd = args[++i];
                        break;

                    case "-e":
                        extraIgnores = args[++i];
                        break;

                    default:
                        Console.Error.WriteLine("Unknown argument {0}", args[i]);
                        break;
                }
            }

            // Standard list of system / operational attributes to ignore
            // Add to this list with the -e argument.
            List<string> ignoredAttrs = new List<string>(){
                "samaccounttype", "memberof", "sidhistory", "objectguid",
                "badpasswordtime", "lockouttime", "instancetype", "dscorepropagationdata",
                "whencreated", "whenchanged", "usncreated", "usnchanged", "name",
                "pwdlastset", "primarygroupid", "admincount", "logoncount", "badpwdcount",
                "lastlogon", "lastlogontimestamp", "lastlogoff", "localpolicyflags",
                "iscriticalsystemobject", "manager", "directreports",
                "systemflags", "showinadvancedviewonly", "managedobjects", 
                "managedby", "unixuserpassword", "netbootscpbl",
                "iscriticalsystemobject", "systemflags", "showinadvancedviewonly"
                };

            if (!string.IsNullOrEmpty(extraIgnores))
                ignoredAttrs.AddRange(extraIgnores.Split(',').Select(word => word.ToLowerInvariant()));

            Console.Error.WriteLine("Source file {0}, encoding {1}", fileIn, srcenc);

            using (System.IO.TextReader tr = new System.IO.StreamReader(fileIn, srcenc))
            {
                using (LdifReader r = new LdifReader(tr))
                {
                    using (LdifWriter w = new LdifWriter(fileOut))
                    {
                        this.ReformatLdif(r, w, passwordEncoding, defaultPwd, ignoredAttrs, msExch,
                            null);
                    }
                }
            }
        }

        static void Main(string[] args)
        {
            try
            {
                AdsImportPrep p = new AdsImportPrep();
                p.IMain(args);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.Message + " :: " + ex.StackTrace);
                System.Environment.ExitCode = 1;
            }
        }
    }
}
