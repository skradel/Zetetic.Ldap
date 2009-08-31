using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace Zetetic.Ldap
{
    public class LdifWriter : IDisposable
    {
        TextWriter _sw;
        bool _openEntry, _ownsStream;
        DateTime _started;
        long _entryCount;
        
        /// <summary>
        /// Instruct this LdifWriter whether or not to write a summary of object count and start+finish times
        /// during .Close().  The default is true.
        /// </summary>
        public bool WriteSummary { get; set; }

        /// <summary>
        /// Create an LdifWriter for ASCII file output.  The stream will close upon Disposing this LdifWriter.
        /// Call .Close to wrap up the last entry and write a summary.
        /// </summary>
        /// <param name="path"></param>
        public LdifWriter(string path)
        {
            _sw = new StreamWriter(path, false, System.Text.Encoding.ASCII);
            _ownsStream = true;
            this.WriteSummary = true;
        }

        /// <summary>
        /// Create an LdifWriter pointed at an existing output stream; the LdifWriter will not close
        /// the stream during Dispose.
        /// </summary>
        /// <param name="tw"></param>
        public LdifWriter(TextWriter tw)
        {
            _sw = tw;
            _ownsStream = false;
            this.WriteSummary = true;
        }

        /// <summary>
        /// Write a 'modrdn' instruction to LDIF.  Optionally specify newSuperior to move the object to a
        /// new point in the directory tree; or null/blank for RDN change only.
        /// </summary>
        /// <param name="dn"></param>
        /// <param name="newRdn"></param>
        /// <param name="newSuperior"></param>
        public void ModRdn(string dn, string newRdn, string newSuperior)
        {
            BeginEntry(dn);

            WriteAttr("changetype", "modrdn");
            WriteAttr("newrdn", newRdn);
            WriteAttr("deleteoldrdn", "1");

            if (!string.IsNullOrEmpty(newSuperior)) 
            {
                WriteAttr("newsuperior", newSuperior);
            }

            EndEntry();
        }

        /// <summary>
        /// Write a #-notated comment line to the stream.
        /// </summary>
        /// <param name="comment"></param>
        public virtual void WriteComment(string comment)
        {
            _sw.WriteLine("# {0}", comment);
        }

        /// <summary>
        /// Close out an open entry, if any, and write the DN of the new entry to the stream.
        /// </summary>
        /// <param name="dn"></param>
        public virtual void BeginEntry(string dn)
        {
            if (_openEntry)
                EndEntry();
            
            _openEntry = true;

            if (_entryCount++ == 0)
                _started = DateTime.Now;

            WriteAttr("dn", dn);
        }

        /// <summary>
        /// Emit a single hyphen on a line; useful for generating 'changetype: modify' instructions.
        /// </summary>
        public void WriteChangeSeparator()
        {
            if (!_openEntry)
                throw new ApplicationException("No open entry");

            _sw.WriteLine("-");
        }

        /// <summary>
        /// Write a DateTime to the stream in Generalized UTC format.
        /// </summary>
        /// <param name="attrName"></param>
        /// <param name="value"></param>
        public void WriteAttr(string attrName, DateTime value)
        {
            if (!_openEntry)
                throw new ApplicationException("No open entry");

            WriteFolded("{0}: {1}", attrName, value.ToUniversalTime().ToString("yyyyMMddHHmmss'.0Z'"));
        }

        /// <summary>
        /// Write a binary attribute to the stream in Base64 encoding.
        /// </summary>
        /// <param name="attrName"></param>
        /// <param name="value"></param>
        public void WriteAttr(string attrName, byte[] value)
        {
            if (!_openEntry)
                throw new ApplicationException("No open entry");

            WriteFolded("{0}:: {1}", attrName, Convert.ToBase64String(value));
        }

        /// <summary>
        /// Write a string value to the stream, with testing for 7-bit safety.  This will switch to Base64 mode
        /// as necessary.
        /// </summary>
        /// <param name="attrName"></param>
        /// <param name="value"></param>
        public void WriteAttr(string attrName, string value)
        {
            if (!_openEntry)
                throw new ApplicationException("No open entry");

            if (IsSafeString(value))
            {
                WriteFolded("{0}: {1}", attrName, value);
            }
            else
            {
                WriteFolded("{0}:: {1}", attrName, Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(value)));
            }
        }

        /// <summary>
        /// Convenience method that calls String.Format on its arguments.
        /// </summary>
        /// <param name="fmt"></param>
        /// <param name="values"></param>
        protected void WriteFolded(string fmt, params object[] values)
        {
            WriteFolded(String.Format(fmt, values));
        }

        /// <summary>
        /// Write an attribute name and its value to the stream, folding at 76 characters per line,
        /// with a single space on the next line to indicate continuation.
        /// </summary>
        /// <param name="value"></param>
        protected void WriteFolded(string value)
        {
            if (value.Length > 76)
            {
                int lineNum = 1;
                while (value.Length > 0)
                {
                    if (lineNum++ > 1) _sw.Write(" ");

                    if (value.Length > 76)
                    {
                        _sw.WriteLine(value.Substring(0, 76));
                        value = value.Substring(76);
                    }
                    else
                    {
                        _sw.WriteLine(value);
                        value = "";
                    }
                }
            }
            else
            {
                _sw.WriteLine(value);
            }
        }

        /// <summary>
        /// any value &lt;= 127 except NUL, LF, CR,
        /// SPACE, colon (":", ASCII 58 decimal)
        /// and less-than ("&lt;" , ASCII 60 decimal)
        /// </summary>
        /// <param name="c"></param>
        /// <returns></returns>
        protected static bool IsSafeInitChar(char c)
        {
            if (c == '\0' || c == '\n' || c == '\r' || c == ' ' || c == ':' || c == '<')
                return false;

            if ((int)c > 127)
                return false;

            return true;
        }

        /// <summary>
        /// Per http://www.faqs.org/rfcs/rfc2849.html
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        protected static bool IsSafeString(string value)
        {
            char c = value[0];

            if (!IsSafeInitChar(c))
                return false;

            if (value.IndexOf('\n') > -1 || value.IndexOf('\r') > -1 || value.IndexOf('\0') > -1)
                return false;

            for (int i = 0; i < value.Length; i++)
            {
                if ((int)value[i] > 127)
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Write a blank line to mark the end of the current entry
        /// </summary>
        public void EndEntry()
        {
            _sw.WriteLine("");
            _openEntry = false;
        }

        /// <summary>
        /// Close out the stream; optionally write a summary of execution time and entry count.
        /// </summary>
        public void Close()
        {
            if (_sw != null)
            {
                if (_openEntry)
                    EndEntry();

                if (this.WriteSummary && _started != default(DateTime))
                    _sw.WriteLine("# Exported {0} entries; started at {1}; ended at {2}", 
                        _entryCount, _started, DateTime.Now);

                if (_ownsStream)
                    _sw.Dispose();

                _sw = null;
            }
        }

        #region IDisposable Members

        /// <summary>
        /// Just close the output stream (if this LdifWriter owns it) without adding a newline or summary.
        /// </summary>
        public void Dispose()
        {
            if (_ownsStream && _sw != null) _sw.Dispose();
        }

        #endregion
    }
}
