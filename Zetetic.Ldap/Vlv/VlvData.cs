using System;
using System.Collections.Generic;
using System.Text;
using System.DirectoryServices.Protocols;

namespace Zetetic.Ldap.Vlv
{
    public class VlvData
    {
        public SearchResultEntryCollection Entries { get; set; }
        public int ContentCount { get; set; }
        public byte[] VlvContextId { get; set; }
        public bool IsDone { get; set; }
    }
}
