using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Net;

namespace Zetetic.Ldap
{
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
        private long _lineNum;

        public string LastDn { get; protected set; }

        /// <summary>
        /// Some LDIF implementations might use more than one space on folded lines.  If TrimFoldedLines
        /// is true, we will eliminate *all* initial whitespace on each folded line.  Note that exactly
        /// one space is correct per RFC 2849. 
        /// </summary>
        public bool TrimFoldedLines { get; set; }

        /// <summary>
        /// Open the ASCII-encoded LDIF file at 'path' for reading.
        /// </summary>
        /// <param name="path"></param>
        public LdifReader(string path)
        {
            _source = new StreamReader(new FileStream(path, FileMode.Open, FileAccess.Read), Encoding.ASCII);
            _ownsStream = true;
        }

        /// <summary>
        /// Read from an already-open TextReader, without assuming any particular encoding.
        /// </summary>
        /// <param name="sr"></param>
        public LdifReader(TextReader sr)
        {
            _source = sr;
            _ownsStream = false;
        }

        /// <summary>
        /// Read from an already-open FileStream, assumed to be ASCII-encoded.
        /// </summary>
        /// <param name="fs"></param>
        public LdifReader(FileStream fs)
        {
            _source = new StreamReader(fs, Encoding.ASCII);
            _ownsStream = false;
        }

        public event EndEntryEventHandler OnEndEntry;
        public event BeginEntryEventHandler OnBeginEntry;
        public event AttributeEventHandler OnAttributeValue;

        /// <summary>
        /// Raise any events from reading the next set of instructions from the LDIF.
        /// Return false if we have reached the end of the file.  Otherwise, true.
        /// </summary>
        /// <returns>True if there is still more to read, false if EOF</returns>
        public bool Read()
        {
            string line = _source.ReadLine();

            if (line != null)
            {
                _lineNum++;

                if (line.StartsWith("#"))
                {
                    // Skip comment line
                    return true;
                }

                if (!_openEntry && !line.StartsWith("dn:", StringComparison.InvariantCultureIgnoreCase))
                {
                    // Preamble stuff, such as version: 1
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
                }

                // End of file?
                return (line != null);
            }

            if (line.StartsWith("dn:", StringComparison.InvariantCultureIgnoreCase))
            {
                _openEntry = true;

                string dn;

                if (line.TrimEnd(' ').ToLowerInvariant() == "dn:")
                {
                    dn = null;
                }
                else
                {
                    bool b64 = (line[3] == ':');
                    dn = line.Substring(b64 ? 4 : 3).TrimStart();

                    while (_source.Peek() == (int)' ')
                    {
                        _lineNum++;

                        if (this.TrimFoldedLines)
                            dn += _source.ReadLine().Substring(1).TrimStart(' ');
                        else
                            dn += _source.ReadLine().Substring(1);
                    }

                    if (b64) dn = Encoding.UTF8.GetString(Convert.FromBase64String(dn));
                }

                this.LastDn = dn;

                if (OnBeginEntry != null)
                    OnBeginEntry(this, new DnEventArgs(dn));

            }
            else
            {
                int fc = line.IndexOf(':');

                // Note that the parser will currently NOT read changetype-oriented
                // LDIF ('-' on a line by itself between attr instructions).
                if (fc == -1)
                    throw new ApplicationException("Malformed LDIF on line " + _lineNum);

                string attrName = line.Substring(0, fc);

                string attrVal;
                bool b64 = false, url = false;

                if (fc + 1 == line.Length)
                {
                    attrVal = null;
                }
                else
                {
                    b64 = (line[fc + 1] == ':');
                    url = (line[fc + (b64 ? 2 : 1)] == '<');

                    attrVal = line.Substring(b64 ? fc + 2 : fc + 1).TrimStart();

                    if (url)
                        attrVal = attrVal.Substring(1);

                    while (_source.Peek() == (int)' ')
                    {
                        _lineNum++;

                        if (this.TrimFoldedLines)
                            attrVal += _source.ReadLine().Substring(1).TrimStart(' ');
                        else
                            attrVal += _source.ReadLine().Substring(1);
                    }
                }

                if (OnAttributeValue != null)
                {
                    if (attrVal == null)
                    {
                        OnAttributeValue(this, new AttributeEventArgs(attrName, null));
                    }
                    else if (b64)
                    {
                        byte[] bytes = Convert.FromBase64String(attrVal);
                        if (url)
                        {
                            System.Uri uri = new Uri(System.Text.Encoding.UTF8.GetString(bytes));
                            OnAttributeValue(this, new AttributeEventArgs(attrName, uri));
                        }
                        else
                        {
                            OnAttributeValue(this, new AttributeEventArgs(attrName, bytes));
                        }
                    }
                    else
                    {
                        if (url)
                        {
                            System.Uri uri = new Uri(attrVal);
                            OnAttributeValue(this, new AttributeEventArgs(attrName, uri));
                        }
                        else
                        {
                            OnAttributeValue(this, new AttributeEventArgs(attrName, attrVal));
                        }
                    }
                }
            }

            return true;
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
