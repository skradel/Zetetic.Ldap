using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace Zetetic.Ldap
{
    public delegate void DeleteEntryEventHandler(object sender, DnEventArgs e);
    public delegate void EndEntryEventHandler(object sender, DnEventArgs e);
    public delegate void BeginEntryEventHandler(object sender, DnEventArgs e);
    public delegate void AttributeEventHandler(object sender, AttributeEventArgs e);

    /// <summary>
    /// LdifReader raises DnEventArgs when encountering a new entry or closing out an existing one
    /// </summary>
    public class DnEventArgs : EventArgs
    {
        public string DistinguishedName { get; protected set; }

        public DnEventArgs(string dn)
        {
            this.DistinguishedName = dn;
        }
    }

    /// <summary>
    /// The state of the entire LDIF; not allowed to mix "full entry" and "changetype".
    /// </summary>
    public enum LdifType { Unknown, Normal, Changetype }

    public enum ChangeType { Unknown, Add, Modify, Delete, ModRdn }

    public enum AttributeOperationDirective { Unknown, Add, Replace, Delete, NewRdn, DeleteOldRdn, NewSuperior }

    /// <summary>
    /// LdifReader raises AttributeEventArgs upon reading a complete single attribute value
    /// (which may span multiple folded lines)
    /// </summary>
    public class AttributeEventArgs : EventArgs
    {
        public string Name { get; protected set; }
        public object Value { get; protected set; }

        public AttributeEventArgs(string attrName, object attrValue)
        {
            this.Name = attrName;
            this.Value = attrValue;
        }
    }

    /// <summary>
    /// LdifReader is a low-overhead, event-based reader for LDIF formatted files.  Use LdifEntryReader
    /// for a more traditional, "whole-entry" reader.
    /// </summary>
    public class LdifReader : IDisposable
    {
        private TextReader _source;
        private bool _ownsStream;
        private bool _openEntry;
        private long _lineNum, _endLineNum;

        public LdifType LdifType { get; protected set; }

        public string LastDn { get; protected set; }

        public string LastAttribute { get; protected set; }

        public AttributeOperationDirective AttributeOperationDirective { get; set; }

        public ChangeType EntryChangeType { get; protected set; }

        /// <summary>
        /// Some LDIF implementations might use more than one space on folded lines.  If TrimFoldedLines
        /// is true, we will eliminate *all* initial whitespace on each folded line.  Note that exactly
        /// one space is correct per RFC 2849. 
        /// </summary>
        public bool TrimFoldedLines { get; set; }

        #region Constructors

        private LdifReader()
        {
            this.LdifType = LdifType.Unknown;
        }

        /// <summary>
        /// Open the ASCII-encoded LDIF file at 'path' for reading.
        /// </summary>
        /// <param name="path"></param>
        public LdifReader(string path) : this()
        {
            _source = new StreamReader(new FileStream(path, FileMode.Open, FileAccess.Read), Encoding.ASCII);
            _ownsStream = true;
        }

        /// <summary>
        /// Read from an already-open TextReader, without assuming any particular encoding.
        /// </summary>
        /// <param name="sr"></param>
        public LdifReader(TextReader sr)
            : this()
        {
            _source = sr;
            _ownsStream = false;
        }

        /// <summary>
        /// Read from an already-open FileStream, assumed to be ASCII-encoded.
        /// </summary>
        /// <param name="fs"></param>
        public LdifReader(FileStream fs) : this()
        {
            _source = new StreamReader(fs, Encoding.ASCII);
            _ownsStream = false;
        }
        #endregion

        public event EndEntryEventHandler OnEndEntry;
        public event BeginEntryEventHandler OnBeginEntry;
        public event AttributeEventHandler OnAttributeValue;

        public event DeleteEntryEventHandler OnDeleteEntry;

        /// <summary>
        /// Raise any events from reading the next set of instructions from the LDIF.
        /// Return false if we have reached the end of the file.  Otherwise, true.
        /// </summary>
        /// <returns>True if there is still more to read, false if EOF</returns>
        public bool Read()
        {
            string line = ReadLogicalLine();

            if (line != null)
            {
                if (line.StartsWith("#"))
                {
                    // Skip comment line; don't alter this.LastAttribute
                    return true;
                }

                if (!_openEntry && !line.StartsWith("dn:", StringComparison.InvariantCultureIgnoreCase))
                {
                    // Preamble stuff, such as version: 1
                    this.LastAttribute = "OTHER";
                    return true;
                }
            }

            if (string.IsNullOrEmpty(line))
            {
                if (_openEntry)
                {
                    _openEntry = false;

                    if (OnEndEntry != null)
                        OnEndEntry(this, new DnEventArgs(this.LastDn));

                    this.LastAttribute = null;
                }

                // End of file?
                return (line != null);
            }

            //--- Handle main cases ---
            if (line.StartsWith("dn:", StringComparison.InvariantCultureIgnoreCase))
            {
                _openEntry = true;

                line = line.Substring(3);

                this.LastDn = this.ParseAttributeValue(line);

                this.LastAttribute = "DN";

                if (OnBeginEntry != null)
                    OnBeginEntry(this, new DnEventArgs(this.LastDn));
            }
            else if (line.StartsWith("changetype:", StringComparison.InvariantCultureIgnoreCase))
            {
                if (!"DN".Equals(this.LastAttribute))
                    throw new ApplicationException("changetype must follow DN");

                if (this.LdifType == LdifType.Unknown)
                {
                    // Possibly raise an event here - detection of the file type
                    this.LdifType = LdifType.Changetype;
                }
                else if (this.LdifType == LdifType.Normal)
                {
                    throw new ApplicationException("Cannot mix changetype and normal content");
                }

                string chtype = ParseAttributeValue(line.Substring(11));
                switch (chtype)
                {
                    case "add":
                        this.EntryChangeType = ChangeType.Add;
                        break;

                    case "delete":
                        this.EntryChangeType = ChangeType.Delete;
                        break;

                    case "moddn":
                    case "modrdn":
                        this.EntryChangeType = ChangeType.ModRdn;
                        break;

                    case "modify":
                        this.EntryChangeType = ChangeType.Modify;
                        break;

                    default:
                        throw new ApplicationException("Unknown changetype " + chtype + " at line " + _lineNum);
                }
            }
            else if ("-".Equals(line))
            {
                if (this.LdifType != LdifType.Changetype)
                    throw new ApplicationException("Encountered '-' in non-changetype LDIF");

                // @todo Raise new event OnEndAttributeChanges
            }
            else
            {
                int fc = line.IndexOf(':');

                if (fc == -1)
                    throw new ApplicationException("Malformed LDIF on line " + _lineNum);

                string attrName = line.Substring(0, fc);
                string attrVal = ParseAttributeValue(line.Substring(fc + 1));

                AttributeOperationDirective op;
                if (this.LdifType == LdifType.Changetype && IsChangeDirective(attrVal, out op))
                {
                    this.AttributeOperationDirective = op;
                }
                else
                {
                    this.LastAttribute = attrName;

                    if (OnAttributeValue != null)
                        OnAttributeValue(this, new AttributeEventArgs(attrName, attrVal));
                    
                }
            }

            return true;
        }

        protected bool IsChangeDirective(string attrVal, out AttributeOperationDirective op)
        {
            switch (attrVal)
            {
                case "add":
                    op = AttributeOperationDirective.Add;
                    return true;

                case "replace":
                    op = AttributeOperationDirective.Replace;
                    return true;

                case "delete":
                    op = AttributeOperationDirective.Delete;
                    return true;

                case "newrdn":
                    op = AttributeOperationDirective.NewRdn;
                    return true;

                case "deleteoldrdn":
                    op = AttributeOperationDirective.DeleteOldRdn;
                    return true;

                case "newsuperior":
                    op = AttributeOperationDirective.NewSuperior;
                    return true;

                default:
                    op = AttributeOperationDirective.Unknown;
                    return false;
            }
        }

        private string ParseAttributeValue(String attr)
        {
            if (string.IsNullOrEmpty(attr) || attr.Trim().Length == 0)
                return null;

            if (attr[0] == ':')
            {
                return Encoding.UTF8.GetString(Convert.FromBase64String(attr.Substring(1).Trim()));
            }
            else
            {
                return attr.Substring(1).Trim();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private String ReadLogicalLine()
        {
            _lineNum = ++_endLineNum;

            string line = _source.ReadLine();
            
            if (!string.IsNullOrEmpty(line))
            {
                //Can be continued if it starts with a space
                while (_source.Peek() == (int)' ')
                {
                    _endLineNum++;

                    String extra = _source.ReadLine().Substring(1);

                    if (this.TrimFoldedLines)
                        extra = extra.TrimStart(' ');
                    
                    line += extra;
                }
            }
            return line;
        }

        /// <summary>
        /// Close the underlying stream if we own it; otherwise, just nullify our reference.
        /// </summary>
        /// <param name="isDisposing"></param>
        public void Dispose(bool isDisposing)
        {
            if (_source != null)
            {
                if (_ownsStream)
                    _source.Dispose();

                _source = null;
            }

            if (isDisposing)
                System.GC.SuppressFinalize(this);
        }

        #region IDisposable Members

        public void Dispose()
        {
            Dispose(true);
        }

        #endregion

        ~LdifReader()
        {
            Dispose(false);
        }
    }
}
