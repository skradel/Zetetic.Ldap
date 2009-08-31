using System;
using System.Collections.Generic;
using System.Text;

namespace Zetetic.Ldap
{
    public struct PivotColumn
    {
        public string Source, Destination, Joiner;
        public int Index;

        public static readonly int LAST_INDEX = -1;

        public PivotColumn(string src, string dest, string join)
        {
            this.Source = src;
            this.Destination = dest;
            this.Joiner = join;
            this.Index = 0;
        }

        public PivotColumn(string src, string dest, int index)
        {
            this.Source = src;
            this.Destination = dest;
            this.Joiner = null;
            this.Index = index;
        }

        public PivotColumn(string src, string dest, string join, int index)
        {
            this.Source = src;
            this.Destination = dest;
            this.Joiner = join;
            this.Index = index;
        }
    }
}
