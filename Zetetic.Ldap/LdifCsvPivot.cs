using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace Zetetic.Ldap
{
    public class LdifCsvPivot
    {
        public List<PivotColumn> Columns = new List<PivotColumn>();

        public string Separator = "\t";
        public TextWriter Output = System.Console.Out;

        public void AddColumn(string ldapName, string title, string join)
        {
            Columns.Add(new PivotColumn(ldapName, title, join));
        }

        public void AddColumn(string ldapName, string title, int index)
        {
            Columns.Add(new PivotColumn(ldapName, title, index));
        }

        public void Process(string path, bool emitHeader)
        {
            Process(path, emitHeader, Encoding.GetEncoding("iso-8859-1"));
        }

        public void Process(string path, bool emitHeader, Encoding encoding)
        {
            using (StreamReader sr = new StreamReader(path, encoding))
            {
                Process(sr, emitHeader);
            }
        }

        protected void Write(string val) 
        {
            if (string.IsNullOrEmpty(val))
            {
                // Do nothing
            }
            else if (val.Contains(this.Separator))
            {
                Output.Write("\"" + val + "\"");
            }
            else
            {
                Output.Write(val);
            }
        }

        public void Process(StreamReader sr, bool emitHeader)
        {
            if (emitHeader)
            {
                for (int i = 0; i < Columns.Count; i++)
                {
                    Output.Write(Columns[i].Destination);

                    if (i + 1 < Columns.Count)
                        Output.Write(this.Separator);
                }
                Output.WriteLine("");
            }

            using (LdifEntryReader ldr = new LdifEntryReader(new LdifReader(sr)))
            {
                for (Entry e = ldr.ReadEntry(); e != null; e = ldr.ReadEntry())
                {
                    for (int i = 0; i < Columns.Count; i++)
                    {
                        PivotColumn col = Columns[i];

                        switch (col.Source)
                        {
                            case "dn":
                                Write(e.DistinguishedName);
                                break;

                            case "rdn":
                                Write(e.RDN);
                                break;

                            case "parent":
                            case "superior":
                                Write(e.SuperiorDn);
                                break;

                            default:
                                if (e.HasAttribute(col.Source))
                                {
                                    string[] values = e.GetAttrStringValues(col.Source);

                                    if (string.IsNullOrEmpty(col.Joiner))
                                    {
                                        if (col.Index == PivotColumn.LAST_INDEX)
                                        {
                                            Write(values[values.Length - 1]);
                                        }
                                        else if (values.Length > col.Index)
                                        {
                                            Write(values[col.Index]);
                                        }
                                    }
                                    else
                                    {
                                        Write(string.Join(col.Joiner, values));
                                    }
                                }
                                break;
                        }

                        if (i + 1 < Columns.Count)
                            Output.Write(this.Separator);
                    }
                    Output.WriteLine("");
                }
            }
        }
    }

}
