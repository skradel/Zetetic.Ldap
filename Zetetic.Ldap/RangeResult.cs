using System;
using System.Collections.Generic;
using System.Text;

namespace Zetetic.Ldap
{
    class RangeResult
    {
        public RangeResult() { }
        public int Start, End;
        public bool IsFinal;
        public string[] Values;
    }
}
