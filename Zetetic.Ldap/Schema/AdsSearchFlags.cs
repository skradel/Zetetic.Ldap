using System;
using System.Collections.Generic;
using System.Text;

namespace Zetetic.Ldap.Schema
{
    public enum AdsSearchFlags
    {
        None = 0,
        Index = 1,
        Containerized = 2,
        AmbiguousNameResolution = 4,
        TombstonePreserve = 8,
        CopyPreserve = 16,
        Tuple = 32,
        VlvSubtree = 64
    }
}
