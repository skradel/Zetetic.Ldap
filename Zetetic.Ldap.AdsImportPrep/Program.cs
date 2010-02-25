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
        public void ReformatLdif(LdifReader r, LdifWriter w, Encoding enc, string defaultPwd, List<string> ignoredAttrs)
        {
            bool ignored = false;
            int pwdScore = 0;

            r.OnBeginEntry += (s, a) =>
            {
                // Ignore domain trusts / special accounts
                // This part could use some rethinking.
                if (a.DistinguishedName.Contains("$,CN=Users,") || a.DistinguishedName.Contains("krbtgt"))
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
                    if (pwdScore > 1)
                        w.WriteAttr("unicodePwd", enc.GetBytes(
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
                    if (a.Name == "objectCategory" && "CN=Person,CN=Schema,CN=Configuration,DC=kad1,DC=local".Equals(a.Value))
                        pwdScore++;
                    if (a.Name == "objectClass" && "user".Equals(a.Value))
                        pwdScore++;

                    if (a.Value != null && !ignoredAttrs.Contains(a.Name.ToLowerInvariant()))
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
            };

            while (r.Read())
            {
                // Keep reading
            }

            w.Close();
        }

        public void IMain(string[] args)
        {
            string fileIn = null, fileOut = null, defaultPwd = "1FakePwd50!", extraIgnores = null;
            Encoding enc = new UnicodeEncoding(true, false, true);

            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "-enc":
                        enc = Encoding.GetEncoding(args[++i]);
                        break;

                    case "-i":
                        fileIn = args[++i];
                        break;

                    case "-o":
                        fileOut = args[++i];
                        break;

                    case "-p":
                    case "-pwd":
                        defaultPwd = args[++i];
                        break;

                    case "-e":
                        extraIgnores = args[++i];
                        break;
                }
            }

            // Standard list of system / operational attributes to ignore
            // Add to this list with the -e argument.
            List<string> ignoredAttrs = new List<string>(){
                "samaccounttype", "memberof", "objectsid", "sidhistory", "objectguid",
                "badpasswordtime", "lockouttime", "instancetype", "dscorepropagationdata",
                "whencreated", "whenchanged", "usncreated", "usnchanged", "name",
                "pwdlastset", "primarygroupid", "admincount", "logoncount", "badpwdcount",
                "lastlogon", "lastlogontimestamp", "lastlogoff", "localpolicyflags",
                "iscriticalsystemobject"
                };

            if (!string.IsNullOrEmpty(extraIgnores))
                ignoredAttrs.AddRange(extraIgnores.Split(','));

            using (LdifReader r = new LdifReader(fileIn))
            {
                using (LdifWriter w = new LdifWriter(fileOut))
                {
                    this.ReformatLdif(r, w, enc, defaultPwd, ignoredAttrs);
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
