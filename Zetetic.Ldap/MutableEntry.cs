using System;
using System.Collections.Generic;
using System.Text;
using System.DirectoryServices.Protocols;
using NLog;

namespace Zetetic.Ldap
{
    /// <summary>
    /// MutableEntry is a wrapper around a basic Zetetic.Ldap.Entry with change-tracking.
    /// You can create, inspect, rename, modify, and delete a MutableEntry, and save it to an LDAP host, 
    /// without dealing with the semantics of the LDAP operations.
    /// </summary>
    public class MutableEntry : Entry, IDisposable
    {
        public enum ConflictModes { Error, Accept };

        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        protected readonly Dictionary<string, DirectoryAttributeModification> _changes = 
            new Dictionary<string, DirectoryAttributeModification>();

        private ConflictModes _conflict = ConflictModes.Accept;

        public MutableEntry(string dn)
            : base(dn)
        {
            this.IsNewEntry = true;
        }

        public MutableEntry(SearchResultEntry se)
            : base(se)
        {
        }

        public static MutableEntry CreateUncommitted(string dn, params string[] objectClasses)
        {
            if (String.IsNullOrEmpty(dn))
                throw new ArgumentException("dn cannot be null or empty", "dn");

            MutableEntry entry = new MutableEntry(dn);
            entry.SetAttr("objectClass", objectClasses);
            return entry;
        }

        protected void CheckForDeletion()
        {
            if (this.IsDeleted)
                throw new InvalidOperationException("Object has already been deleted");
        }

        public ConflictModes ConflictMode
        {
            get { return _conflict; }
            set { _conflict = value; }
        }

        /// <summary>
        /// Indicates whether this entry represents a new/uncommited entry.
        /// </summary>
        public bool IsNewEntry { get; protected set; }

        public bool IsDeleted
        {
            get;
            protected set;
        }

        public int PendingChangeCount
        {
            get
            {
                return _changes.Count;
            }
        }

        

        public void Delete(LdapConnection ldap)
        {
            CheckForDeletion();

            if (this.IsNewEntry)
            {
                throw new InvalidOperationException(String.Format("Entry {0} was never committed - cannot delete", 
                    this.DistinguishedName));
            }

            DeleteRequest del = new DeleteRequest(this.DistinguishedName);
            ldap.SendRequest(del);
            
            this.IsDeleted = true;
        }

        public override bool HasAttribute(string attrName)
        {
            CheckForDeletion();
            return base.HasAttribute(attrName) && !_changes.ContainsKey(attrName + "*d");
        }

        public bool ContainsAttrValue(string propName, object value)
        {
            if (value == null)
                throw new ArgumentNullException("value");

            if (this.HasAttribute(propName))
            {
                return this.Attrs[propName.ToLowerInvariant()].Value.Contains(value);
            }
            return false;
        }

        public string GetAttrStringValue(string propName)
        {
            string[] s = GetAttrStringValues(propName);
            if (s == null) return null;
            return s[0];
        }

        public void AppendAttrValue(string attrName, string value)
        {
            CheckForDeletion();

            string propName = attrName.ToLowerInvariant();

            DirectoryAttributeModification dam;
            if (_changes.ContainsKey(propName))
            {
                dam = _changes[propName];

                if (this.ConflictMode == ConflictModes.Error && dam.Operation != DirectoryAttributeOperation.Add)
                {
                    throw new IncompatibleMutationException(
                        String.Format("Can't process a request to {0} and append on attr {1} - indeterminate results",
                            dam.Operation, propName));
                }
                else
                {
                    logger.Info("Request on attr {0} is {1}, but appending value {2} anyway",
                        propName, dam.Operation, value);

                    if (dam.Contains(""))
                    {
                        logger.Info("Removing empty-replacement value on {0}", propName);
                        dam.Remove("");
                    }

                    if (!dam.Contains(value))
                        dam.Add(value);
                }
            }
            else
            {
                dam = new DirectoryAttributeModification();
                dam.Name = propName;
                dam.Operation = DirectoryAttributeOperation.Add;
                dam.Add(value);
                _changes[propName] = dam;
            }
        }

        /// <summary>
        /// On a new/uncommitted entry, check for any in-queue Add / Replace operations on 'propName'
        /// and remove 'value' if present.  On a regular existing entry, queue a deletion of the
        /// specified value.
        /// </summary>
        /// <param name="propName"></param>
        /// <param name="value"></param>
        public void RemoveAttrValue(string attrName, string value)
        {
            CheckForDeletion();

            string propName = attrName.ToLowerInvariant();

            if (this.IsNewEntry)
            {
                if (_changes.ContainsKey(propName))
                {
                    DirectoryAttributeModification dam = _changes[propName];
                    if (dam.Contains(value))
                        dam.Remove(value);
                }
            }
            else
            {
                string key = propName + "*d";

                if (_changes.ContainsKey(key))
                {
                    DirectoryAttributeModification dam = _changes[key];
                    if (!dam.Contains(value))
                        dam.Add(value);
                }
                else
                {
                    DirectoryAttributeModification dam = new DirectoryAttributeModification();
                    dam.Operation = DirectoryAttributeOperation.Delete;
                    dam.Name = propName;
                    dam.Add(value);
                    _changes[key] = dam;
                }
            }
        }

        /// <summary>
        /// Collect the set of pending modifications for sending to the LDAP DSA
        /// </summary>
        /// <returns></returns>
        internal DirectoryAttributeModificationCollection ChangesAsDAMC()
        {
            DirectoryAttributeModificationCollection damc = new DirectoryAttributeModificationCollection();
            foreach (DirectoryAttributeModification dam in _changes.Values)
                damc.Add(dam);

            return damc;
        }

        /// <summary>
        /// Send all pending changes to the directory service.  If there is a pending rename / re-superior,
        /// it will fire first.
        /// </summary>
        /// <param name="ldap"></param>
        public void CommitChanges(LdapConnection ldap)
        {
            CheckForDeletion();

            if (this.IsDnDirty)
            {
                ModifyDNRequest req = new ModifyDNRequest();
                req.DistinguishedName = this.OriginalDn;

                req.NewName = this.RDN;
                logger.Info("Request new name {0}", req.NewName);

                req.DeleteOldRdn = true;

                if (this.IsSuperiorDirty)
                {
                    req.NewParentDistinguishedName = this.SuperiorDn;
                    logger.Info("Request new superior {0}", req.NewParentDistinguishedName);
                }

                ldap.SendRequest(req);

                this.IsDnDirty = false;
                this.IsSuperiorDirty = false;
                this.OriginalDn = this.DistinguishedName;
            }

            if (_changes.Count > 0)
            {
                if (this.IsNewEntry)
                {
                    AddRequest req = new AddRequest(this.DistinguishedName);

                    foreach (DirectoryAttributeModification dm in this.ChangesAsDAMC())
                        req.Attributes.Add(new DirectoryAttribute(dm.Name, dm.GetValues(typeof(string))));

                    ldap.SendRequest(req);
                }
                else
                {
                    ModifyRequest req = new ModifyRequest(this.DistinguishedName);

                    foreach (DirectoryAttributeModification dm in this.ChangesAsDAMC())
                        req.Modifications.Add(dm);

                    ldap.SendRequest(req);
                }

                _changes.Clear();
                this.IsNewEntry = false;

                logger.Info("Commit on {0} complete", this.DistinguishedName);
            }
            else
            {
                logger.Info("Nothing to commit on {0}", this.DistinguishedName);

                if (this.IsNewEntry)
                    throw new InvalidOperationException(
                        "Cannot commit a new directory object with no attributes");
            }
        }

        /// <summary>
        /// Replace the values of 'propName' with 'value'.  If there is already an in-flight Replace request on
        /// 'propName', this method will add to its values.
        /// </summary>
        /// <param name="propName"></param>
        /// <param name="value"></param>
        public void SetAttr(string attrName, string value)
        {
            SetAttr(attrName, new object[] { value });
        }

        /// <summary>
        /// Replace the values of 'propName' with 'values'.  If there is already an in-flight Replace request on
        /// 'propName', this method will add to its values.
        /// </summary>
        /// <param name="propName"></param>
        /// <param name="values"></param>
        public void SetAttr(string attrName, object[] values)
        {
            CheckForDeletion();
            attrName = attrName.ToLowerInvariant();

            DirectoryAttributeModification dam;

            if (_changes.ContainsKey(attrName))
            {
                dam = _changes[attrName];

                if (dam.Operation != DirectoryAttributeOperation.Replace)
                {
                    if (this.ConflictMode == ConflictModes.Error)
                    {
                        throw new IncompatibleMutationException(String.Format(
                            "Change buffer already contains a request to modify {0} as {1}",
                            attrName, dam.Operation));
                    }
                    else
                    {
                        logger.Info("Overriding {0} request on attr {1} with Replace", dam.Operation, attrName);

                        dam = new DirectoryAttributeModification();
                        dam.Name = attrName;
                        dam.Operation = DirectoryAttributeOperation.Replace;
                        _changes[attrName] = dam;
                    }
                }
            }
            else
            {
                logger.Debug("Preparing replacement operation on attr {0}", attrName);

                dam = new DirectoryAttributeModification();
                dam.Name = attrName;
                dam.Operation = DirectoryAttributeOperation.Replace;
                _changes[attrName] = dam;
            }

            foreach (object o in values)
            {
                if (!dam.Contains(o))
                {
                    if (o is byte[])
                    {
                        dam.Add((byte[])o);
                    }
                    else if (o is string)
                    {
                        dam.Add((string)o);
                    }
                    else if (o is Uri)
                    {
                        dam.Add((Uri)o);
                    }
                    else
                    {
                        throw new ArgumentException("No support for type " + o.GetType().FullName, "values");
                    }
                    logger.Debug("Added value to collection {0} of {1}", attrName, o);
                }
                else
                {
                    logger.Debug("Collection {0} already contains value {1}", attrName, o);
                }
            }
        }

        /// <summary>
        /// Replace all values of 'attrName' with the empty string.
        /// </summary>
        /// <param name="propName">The name of the property / attribute to clear</param>
        public void ClearAttr(string attrName)
        {
            CheckForDeletion();
            attrName = attrName.ToLowerInvariant();

            if (this.GetAttrValueCount(attrName) > 0)
            {
                DirectoryAttributeModification dam;
                if (_changes.ContainsKey(attrName))
                {
                    dam = _changes[attrName];
                    if (dam.Operation == DirectoryAttributeOperation.Replace)
                    {
                        if (dam.Count == 0)
                        {
                            logger.Debug("Already contains request to set {0} with Replace/Clear", attrName);
                            return;
                        }
                    }

                    if (this.ConflictMode == ConflictModes.Error)
                    {
                        throw new IncompatibleMutationException(String.Format(
                                "Change buffer already contains a non-blank request to modify attr {0} as {1}",
                                attrName, dam.Operation));
                    }
                    else
                    {
                        logger.Info("Overriding non-blank {0} request on attr {1} with Replace/Clear",
                            dam.Operation, attrName);


                        dam = new DirectoryAttributeModification() { Name = attrName, Operation = DirectoryAttributeOperation.Replace };
                        dam.Clear();
                        _changes[attrName] = dam;

                        /*
                        dam = new DirectoryAttributeModification() { Name = propName, Operation = DirectoryAttributeOperation.Delete };
                        _changes[propName] = dam;
                        */
                        if (_changes.ContainsKey(attrName + "*d"))
                        {
                            logger.Warn("Removing useless single-value deletion on attr {0}", attrName);
                            _changes.Remove(attrName + "*d");
                        }
                    }
                }
                else
                {
                    dam = new DirectoryAttributeModification() { Name = attrName, Operation = DirectoryAttributeOperation.Replace };
                    dam.Clear();
                    _changes[attrName] = dam;

                    logger.Debug("Set Replace/Clear on attr {0}", attrName);

                    if (_changes.ContainsKey(attrName + "*d"))
                    {
                        logger.Warn("Removing useless single-value deletion on attr {0}");
                        _changes.Remove(attrName + "*d");
                    }
                }
            }
        }



        #region IDisposable Members

        public void Dispose()
        {
            Dispose(true);
        }

        #endregion

        protected void Dispose(bool isDisposing)
        {
            if (isDisposing)
            {
                GC.SuppressFinalize(this);
            }
        }

        ~MutableEntry()
        {
            Dispose(false);
        }
    }

    /// <summary>
    /// This exception indicates that the MutableLdapEntry has been given inconsistent changes (such as replacing and appending
    /// to existing attribute values) without an intermediate commit.
    /// </summary>
    public class IncompatibleMutationException : System.Exception
    {
        public IncompatibleMutationException(string message) : base(message) { }
    }
}
