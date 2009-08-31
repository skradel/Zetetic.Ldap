using System;
using System.Collections.Generic;
using System.Text;
using System.Collections.ObjectModel;
using System.DirectoryServices.Protocols;

namespace Zetetic.Ldap.Schema
{
    public interface ISchemaInfo
    {
        void Initialize(System.DirectoryServices.Protocols.LdapConnection conn);

        IEnumerable<AttributeSchema> Attributes { get; }

        IEnumerable<ObjectClassSchema> ObjectClasses { get; }

        AttributeSchema GetAttribute(string attrName);

        ObjectClassSchema GetObjectClass(string ocName);

        ReadOnlyCollection<object> GetValuesAsLanguageType(SearchResultEntry se, string attrName);
    }
}
